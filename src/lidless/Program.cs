using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using Microsoft.Win32;

namespace Lidless;

internal sealed class Preferences
{
    public int AutoOffMinutes { get; set; }
    public int BatteryCutoff { get; set; } = 20;
    public bool BatteryGuard { get; set; } = true;
    public bool ThermalGuard { get; set; } = true;
    public int ThermalCutoffC { get; set; } = 90;
    public bool AutoAgentMode { get; set; }
    public int AgentStopDelayMinutes { get; set; } = 5;
}

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--watch" && int.TryParse(args[1], out var parent))
        {
            Watch(parent, args[2]);
            return;
        }
        using var mutex = new Mutex(true, "Local\\lidless-tray", out var first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        try { RecoveryStore.Restore(); }
        catch (Exception error)
        {
            MessageBox.Show($"Could not restore the previous lid setting: {error.Message}", "lidless recovery", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        Application.Run(new TrayApp());
    }

    private static void Watch(int parent, string file)
    {
        // Separate process survives a tray crash or force-quit.
        try { Process.GetProcessById(parent).WaitForExit(); }
        catch (ArgumentException) { }
        catch { return; }
        if (file != RecoveryStore.FilePath) return;
        try { RecoveryStore.Restore(); }
        catch { /* Keep recovery.json for the next app launch. */ }
    }
}

internal sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon icon;
    private readonly System.Windows.Forms.Timer tick;
    private readonly Preferences prefs;
    private readonly string prefsPath = Path.Combine(RecoveryStore.DirectoryPath, "settings.json");
    private DateTimeOffset? deadline;
    private bool enabled;
    private bool manualRequested;
    private readonly AgentActivityMonitor agents = new();
    private readonly ThermalMonitor thermal = new();
    private DateTimeOffset lastAgentCheck = DateTimeOffset.MinValue;
    private DateTimeOffset lastThermalCheck = DateTimeOffset.MinValue;
    private string? activeAgent;
    private DateTimeOffset? agentGraceUntil;
    private float? cpuTemperature;
    private bool tooHot;
    private string? powerError;
    private DateTimeOffset lastPowerAttempt = DateTimeOffset.MinValue;
    private readonly List<(ContextMenuStrip Menu, DateTimeOffset RetiredAt)> retiredMenus = new();

    internal TrayApp()
    {
        prefs = LoadPreferences();
        using var iconStream = typeof(TrayApp).Assembly.GetManifestResourceStream("Lidless.tray.ico");
        icon = new NotifyIcon { Icon = iconStream is null ? SystemIcons.Application : new Icon(iconStream), Text = "lidless", Visible = true };
        tick = new System.Windows.Forms.Timer { Interval = 1000 };
        tick.Tick += (_, _) => Tick();
        tick.Start();
        PollThermal();
        RefreshMenu();
        if (prefs.AutoAgentMode) ReconcileAuto();
    }

    private Preferences LoadPreferences()
    {
        try { return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(prefsPath)) ?? new Preferences(); }
        catch { return new Preferences(); }
    }

    private void SavePreferences()
    {
        Directory.CreateDirectory(RecoveryStore.DirectoryPath);
        File.WriteAllText(prefsPath, JsonSerializer.Serialize(prefs));
    }

    private void RefreshMenu()
    {
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false
        };
        GlassMenuStyle.Apply(menu);
        menu.Items.Add(new ToolStripMenuItem("lidless") { Enabled = false, Font = new Font("Segoe UI Semibold", 10f) });
        var agentKeepingAwake = prefs.AutoAgentMode && (activeAgent is not null || agentGraceUntil > DateTimeOffset.UtcNow);
        var status = enabled
            ? manualRequested && agentKeepingAwake ? "Active · manual + agent" :
              manualRequested ? "Active · manual" : activeAgent is not null ? $"Active · {activeAgent}" : "Active · agent delay"
            : powerError is not null ? "Power setting unavailable" :
              prefs.ThermalGuard && tooHot ? "Paused · high CPU temperature" :
              prefs.BatteryGuard && BatteryIsLow() ? "Paused · low battery" :
              "Normal lid behavior";
        menu.Items.Add(new ToolStripMenuItem(status) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem($"{(manualRequested ? "✓" : "  ")}  Keep awake (manual)", null, (_, _) => Toggle()));
        menu.Items.Add(new ToolStripMenuItem($"{(prefs.AutoAgentMode ? "✓" : "  ")}  Auto agent detection", null, (_, _) =>
        {
            prefs.AutoAgentMode = !prefs.AutoAgentMode;
            SavePreferences();
            if (prefs.AutoAgentMode) ReconcileAuto();
            else
            {
                activeAgent = null;
                agentGraceUntil = null;
                ReconcilePower();
            }
            RefreshMenu();
        }));
        if (prefs.AutoAgentMode)
            menu.Items.Add(new ToolStripMenuItem(activeAgent is null ? "No active local agent" :
                $"Detected: {activeAgent}") { Enabled = false });
        var agentDelay = new ToolStripMenuItem("After agent stops");
        foreach (var minutes in new[] { 0, 1, 5, 10, 15, 30, 60 })
        {
            var chosen = minutes;
            agentDelay.DropDownItems.Add(new ToolStripMenuItem($"{(prefs.AgentStopDelayMinutes == minutes ? "✓" : "  ")}  {(minutes == 0 ? "Immediately" : $"{minutes} minutes")}", null, (_, _) =>
            {
                prefs.AgentStopDelayMinutes = chosen;
                SavePreferences();
                if (agentGraceUntil is not null) agentGraceUntil = DateTimeOffset.UtcNow.AddMinutes(chosen);
                ReconcilePower();
            }));
        }
        GlassMenuStyle.Apply(agentDelay.DropDown);
        menu.Items.Add(agentDelay);
        if (prefs.AutoAgentMode && activeAgent is null && agentGraceUntil > DateTimeOffset.UtcNow)
            menu.Items.Add(new ToolStripMenuItem($"Normal lid in {Math.Ceiling((agentGraceUntil.Value - DateTimeOffset.UtcNow).TotalMinutes)} min") { Enabled = false });
        var battery = SystemInformation.PowerStatus;
        var batteryText = battery.BatteryLifePercent >= 0
            ? $"Battery: {battery.BatteryLifePercent:P0}" : "Battery: unavailable";
        menu.Items.Add(new ToolStripMenuItem($"{batteryText} · {(battery.PowerLineStatus == PowerLineStatus.Online ? "Charging" : "On battery")}") { Enabled = false });
        if (deadline is { } end)
            menu.Items.Add(new ToolStripMenuItem($"Auto-off: {Math.Max(0, (int)(end - DateTimeOffset.Now).TotalMinutes + 1)} min left") { Enabled = false });
        var durations = new ToolStripMenuItem("Auto-off timer");
        foreach (var minutes in new[] { 0, 15, 30, 60, 120, 240 })
        {
            var chosen = minutes;
            durations.DropDownItems.Add(new ToolStripMenuItem($"{(prefs.AutoOffMinutes == minutes ? "✓" : "  ")}  {(minutes == 0 ? "No limit" : $"{minutes} minutes")}", null, (_, _) =>
            {
                prefs.AutoOffMinutes = chosen;
                SavePreferences();
                if (chosen > 0) manualRequested = true;
                deadline = manualRequested && chosen > 0 ? DateTimeOffset.Now.AddMinutes(chosen) : null;
                ReconcilePower(true);
            }));
        }
        if (durations.DropDown is ToolStripDropDownMenu submenu)
            submenu.ShowImageMargin = false;
        GlassMenuStyle.Apply(durations.DropDown);
        menu.Items.Add(durations);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem($"{(prefs.BatteryGuard ? "✓" : "  ")}  Battery guard (20%)", null, (_, _) =>
        {
            prefs.BatteryGuard = !prefs.BatteryGuard;
            SavePreferences();
            ReconcilePower();
        }));
        menu.Items.Add(new ToolStripMenuItem($"{(prefs.ThermalGuard ? "✓" : "  ")}  Thermal guard (90°C)", null, (_, _) =>
        {
            prefs.ThermalGuard = !prefs.ThermalGuard;
            SavePreferences();
            ReconcilePower();
        }));
        menu.Items.Add(new ToolStripMenuItem(cpuTemperature is { } temp
            ? $"CPU: {temp:0}°C" : "CPU sensor unavailable") { Enabled = false });
        menu.Items.Add(new ToolStripMenuItem($"{(IsLaunchAtLoginEnabled() ? "✓" : "  ")}  Launch on startup", null, (_, _) =>
        {
            try
            {
                using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)
                    ?? throw new InvalidOperationException("Startup registry key unavailable.");
                if (run.GetValue("lidless") is null)
                    run.SetValue("lidless", $"\"{Environment.ProcessPath}\"");
                else
                    run.DeleteValue("lidless", false);
                RefreshMenu();
            }
            catch (Exception error) { MessageBox.Show(error.Message, "lidless", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Quit", null, (_, _) => ExitThread()));
        var old = icon.ContextMenuStrip;
        icon.ContextMenuStrip = menu;
        // Windows can still be opening or closing a menu after NotifyIcon swaps it.
        // Keep old instances alive until the tray has finished that interaction.
        if (old is not null) retiredMenus.Add((old, DateTimeOffset.UtcNow));
        icon.Text = enabled ? "lidless — active" : "lidless — normal lid behavior";
    }

    private static bool IsLaunchAtLoginEnabled()
    {
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return run?.GetValue("lidless") is not null;
    }

    private void Toggle()
    {
        manualRequested = !manualRequested;
        deadline = manualRequested && prefs.AutoOffMinutes > 0
            ? DateTimeOffset.Now.AddMinutes(prefs.AutoOffMinutes) : null;
        ReconcilePower(true);
    }

    private void Enable(bool showError)
    {
        lastPowerAttempt = DateTimeOffset.UtcNow;
        try
        {
            var scheme = PowerPolicy.ActiveScheme();
            var (ac, dc) = PowerPolicy.Read(scheme);
            RecoveryStore.Save(new Recovery(scheme, ac, dc));
            try
            {
                PowerPolicy.Write(scheme, 0, 0);
                SleepInhibitor.Enable();
            }
            catch
            {
                SleepInhibitor.Disable();
                RecoveryStore.Restore();
                throw;
            }
            enabled = true;
            powerError = null;
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.");
            Process.Start(new ProcessStartInfo(exe, $"--watch {Environment.ProcessId} \"{RecoveryStore.FilePath}\"")
            { UseShellExecute = false, CreateNoWindow = true });
            RefreshMenu();
        }
        catch (Exception error)
        {
            SleepInhibitor.Disable();
            try { RecoveryStore.Restore(); } catch { }
            enabled = false;
            powerError = error.Message;
            if (showError)
                MessageBox.Show($"Could not change the lid action: {error.Message}", "lidless", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RefreshMenu();
        }
    }

    private void Disable()
    {
        try { RecoveryStore.Restore(); }
        catch (Exception error)
        {
            MessageBox.Show($"Could not restore the lid action: {error.Message}", "lidless", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        enabled = false;
        SleepInhibitor.Disable();
        RefreshMenu();
    }

    private void Tick()
    {
        for (var i = retiredMenus.Count - 1; i >= 0; i--)
        {
            var (menu, retiredAt) = retiredMenus[i];
            if (menu.Visible || DateTimeOffset.UtcNow - retiredAt < TimeSpan.FromMinutes(1)) continue;
            retiredMenus.RemoveAt(i);
            menu.Dispose();
        }
        if (prefs.AutoAgentMode && DateTimeOffset.UtcNow - lastAgentCheck > TimeSpan.FromSeconds(15))
            ReconcileAuto();
        if (DateTimeOffset.UtcNow - lastThermalCheck > TimeSpan.FromSeconds(15))
        {
            PollThermal();
            ReconcilePower();
        }
        if (deadline <= DateTimeOffset.Now)
        {
            manualRequested = false;
            deadline = null;
            ReconcilePower();
        }
        else if (enabled && prefs.BatteryGuard && BatteryIsLow())
            ReconcilePower();
        if (agentGraceUntil is not null && agentGraceUntil <= DateTimeOffset.UtcNow)
        {
            agentGraceUntil = null;
            ReconcilePower();
        }
    }

    private bool BatteryIsLow()
    {
        var battery = SystemInformation.PowerStatus;
        return battery.PowerLineStatus == PowerLineStatus.Offline &&
            battery.BatteryLifePercent >= 0 &&
            battery.BatteryLifePercent <= prefs.BatteryCutoff / 100f;
    }

    private void ReconcileAuto()
    {
        lastAgentCheck = DateTimeOffset.UtcNow;
        var previousAgent = activeAgent;
        activeAgent = agents.Detect();
        if (activeAgent is not null) agentGraceUntil = null;
        else if (previousAgent is not null && prefs.AgentStopDelayMinutes > 0)
            agentGraceUntil = DateTimeOffset.UtcNow.AddMinutes(prefs.AgentStopDelayMinutes);
        ReconcilePower();
    }

    private void PollThermal()
    {
        lastThermalCheck = DateTimeOffset.UtcNow;
        cpuTemperature = thermal.ReadCpuCelsius();
        if (cpuTemperature is { } temp)
        {
            if (temp >= prefs.ThermalCutoffC) tooHot = true;
            else if (temp <= prefs.ThermalCutoffC - 10) tooHot = false;
        }
        else tooHot = false;
    }

    private void ReconcilePower(bool showError = false)
    {
        var wanted = manualRequested || prefs.AutoAgentMode &&
            (activeAgent is not null || agentGraceUntil > DateTimeOffset.UtcNow);
        var guarded = prefs.BatteryGuard && BatteryIsLow() || prefs.ThermalGuard && tooHot;
        if (!wanted) powerError = null;
        if (wanted && !guarded && !enabled &&
            (showError || powerError is null || DateTimeOffset.UtcNow - lastPowerAttempt > TimeSpan.FromMinutes(1)))
            Enable(showError);
        else if ((!wanted || guarded) && enabled) Disable();
        else RefreshMenu();
    }

    protected override void ExitThreadCore()
    {
        if (enabled) Disable();
        thermal.Dispose();
        tick.Dispose();
        icon.Visible = false;
        icon.Dispose();
        foreach (var (menu, _) in retiredMenus) menu.Dispose();
        retiredMenus.Clear();
        base.ExitThreadCore();
    }
}

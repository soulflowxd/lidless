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
    private Form? window;

    internal TrayApp()
    {
        prefs = LoadPreferences();
        using var iconStream = typeof(TrayApp).Assembly.GetManifestResourceStream("Lidless.lidless.ico");
        icon = new NotifyIcon { Icon = iconStream is null ? SystemIcons.Application : new Icon(iconStream), Text = "lidless", Visible = true };
        icon.DoubleClick += (_, _) => ShowWindow();
        icon.Click += (_, _) => ShowWindow();
        tick = new System.Windows.Forms.Timer { Interval = 1000 };
        tick.Tick += (_, _) => Tick();
        tick.Start();
        RefreshMenu();
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
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Open lidless", null, (_, _) => ShowWindow()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(enabled ? "Keep awake: On" : "Keep awake: Off", null, (_, _) => Toggle()) { Checked = enabled });
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
            durations.DropDownItems.Add(new ToolStripMenuItem(minutes == 0 ? "No limit" : $"{minutes} minutes", null, (_, _) =>
            {
                prefs.AutoOffMinutes = chosen;
                SavePreferences();
                if (chosen > 0 && !enabled) Enable();
                deadline = enabled && chosen > 0 ? DateTimeOffset.Now.AddMinutes(chosen) : null;
                RefreshMenu();
            }) { Checked = prefs.AutoOffMinutes == minutes });
        }
        menu.Items.Add(durations);
        menu.Items.Add(new ToolStripMenuItem("Battery guard (20%)", null, (_, _) =>
        {
            prefs.BatteryGuard = !prefs.BatteryGuard;
            SavePreferences();
            Tick();
        }) { Checked = prefs.BatteryGuard });
        menu.Items.Add(new ToolStripMenuItem("Launch at login", null, (_, _) =>
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
        }) { Checked = IsLaunchAtLoginEnabled() });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("About lidless", null, (_, _) => MessageBox.Show(
            "lidless keeps Windows running when you close the lid by temporarily setting the active power plan's lid action to Do nothing.\n\nKeep the laptop ventilated. Closing the lid does not lock Windows; use Win+L if needed.\n\nBased on Lidless by Nghia Luong (MIT).",
            "lidless", MessageBoxButtons.OK, MessageBoxIcon.Information)));
        menu.Items.Add(new ToolStripMenuItem("Quit", null, (_, _) => ExitThread()));
        var old = icon.ContextMenuStrip;
        icon.ContextMenuStrip = menu;
        old?.Dispose();
        icon.Text = enabled ? "lidless — keep awake on" : "lidless — keep awake off";
        if (window is { IsDisposed: false }) UpdateWindow();
    }

    private Label? statusLabel;
    private Button? toggleButton;
    private ComboBox? timerBox;
    private CheckBox? guardBox;
    private CheckBox? loginBox;

    private void ShowWindow()
    {
        if (window is { IsDisposed: false }) { window.Show(); window.Activate(); return; }
        window = new Form
        {
            Text = "lidless", Size = new Size(390, 350), MinimumSize = new Size(390, 350),
            MaximumSize = new Size(390, 350), StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false,
            BackColor = Color.FromArgb(22, 24, 36), ForeColor = Color.White,
            Font = new Font("Segoe UI", 10)
        };
        var title = new Label { Text = "lidless", Font = new Font("Segoe UI", 21, FontStyle.Bold),
            Location = new Point(24, 20), Size = new Size(320, 45) };
        statusLabel = new Label { Location = new Point(26, 74), Size = new Size(320, 28) };
        toggleButton = new Button { Location = new Point(24, 110), Size = new Size(335, 42),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(93, 75, 235), ForeColor = Color.White };
        toggleButton.Click += (_, _) => Toggle();
        var timerLabel = new Label { Text = "Auto-off timer", Location = new Point(26, 175), Size = new Size(160, 25) };
        timerBox = new ComboBox { Location = new Point(195, 172), Size = new Size(164, 28), DropDownStyle = ComboBoxStyle.DropDownList };
        timerBox.Items.AddRange(new object[] { "No limit", "15 minutes", "30 minutes", "1 hour", "2 hours", "4 hours" });
        timerBox.SelectedIndexChanged += (_, _) =>
        {
            if (timerBox.SelectedIndex < 0) return;
            prefs.AutoOffMinutes = new[] { 0, 15, 30, 60, 120, 240 }[timerBox.SelectedIndex];
            SavePreferences();
            if (prefs.AutoOffMinutes > 0 && !enabled) Enable();
            deadline = enabled && prefs.AutoOffMinutes > 0 ? DateTimeOffset.Now.AddMinutes(prefs.AutoOffMinutes) : null;
            RefreshMenu();
        };
        guardBox = new CheckBox { Text = "Battery guard (20%)", Location = new Point(26, 220), Size = new Size(300, 27) };
        guardBox.CheckedChanged += (_, _) => { prefs.BatteryGuard = guardBox.Checked; SavePreferences(); Tick(); RefreshMenu(); };
        loginBox = new CheckBox { Text = "Launch at login", Location = new Point(26, 255), Size = new Size(300, 27) };
        loginBox.CheckedChanged += (_, _) => SetLaunchAtLogin(loginBox.Checked);
        window.Controls.AddRange(new Control[] { title, statusLabel, toggleButton, timerLabel, timerBox, guardBox, loginBox });
        UpdateWindow();
        window.Show();
        window.Activate();
    }

    private void UpdateWindow()
    {
        if (window is null || window.IsDisposed) return;
        if (statusLabel is not null) statusLabel.Text = enabled ? "Your laptop stays running with the lid closed" : "Normal lid behavior is active";
        if (toggleButton is not null) toggleButton.Text = enabled ? "Turn off keep awake" : "Keep awake with lid closed";
        if (timerBox is not null) timerBox.SelectedIndex = Array.IndexOf(new[] { 0, 15, 30, 60, 120, 240 }, prefs.AutoOffMinutes);
        if (guardBox is not null) guardBox.Checked = prefs.BatteryGuard;
        if (loginBox is not null) loginBox.Checked = IsLaunchAtLoginEnabled();
    }

    private static void SetLaunchAtLogin(bool selected)
    {
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (run is null) return;
        if (selected) run.SetValue("lidless", $"\"{Environment.ProcessPath}\"");
        else run.DeleteValue("lidless", false);
    }

    private static bool IsLaunchAtLoginEnabled()
    {
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return run?.GetValue("lidless") is not null;
    }

    private void Toggle()
    {
        if (enabled) Disable(); else Enable();
    }

    private void Enable()
    {
        try
        {
            var battery = SystemInformation.PowerStatus;
            if (prefs.BatteryGuard && battery.PowerLineStatus == PowerLineStatus.Offline &&
                battery.BatteryLifePercent >= 0 && battery.BatteryLifePercent <= prefs.BatteryCutoff / 100f)
            {
                MessageBox.Show("Battery is below the cutoff. Connect power before enabling lidless.", "lidless", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var scheme = PowerPolicy.ActiveScheme();
            var (ac, dc) = PowerPolicy.Read(scheme);
            RecoveryStore.Save(new Recovery(scheme, ac, dc));
            try { PowerPolicy.Write(scheme, 0, 0); }
            catch { RecoveryStore.Restore(); throw; }
            enabled = true;
            deadline = prefs.AutoOffMinutes > 0 ? DateTimeOffset.Now.AddMinutes(prefs.AutoOffMinutes) : null;
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.");
            Process.Start(new ProcessStartInfo(exe, $"--watch {Environment.ProcessId} \"{RecoveryStore.FilePath}\"")
            { UseShellExecute = false, CreateNoWindow = true });
            RefreshMenu();
        }
        catch (Exception error)
        {
            try { RecoveryStore.Restore(); } catch { }
            enabled = false;
            deadline = null;
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
        deadline = null;
        RefreshMenu();
    }

    private void Tick()
    {
        if (!enabled) return;
        var battery = SystemInformation.PowerStatus;
        if (deadline <= DateTimeOffset.Now ||
            (prefs.BatteryGuard && battery.PowerLineStatus == PowerLineStatus.Offline &&
             battery.BatteryLifePercent >= 0 && battery.BatteryLifePercent <= prefs.BatteryCutoff / 100f))
            Disable();
    }

    protected override void ExitThreadCore()
    {
        if (enabled) Disable();
        tick.Dispose();
        icon.Visible = false;
        icon.Dispose();
        base.ExitThreadCore();
    }
}

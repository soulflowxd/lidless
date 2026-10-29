using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Lidless;

// A convenience input filter, not a Windows security boundary. Synthetic agent input
// is allowed through; Ctrl+Alt+Del and higher-integrity applications can bypass it.
internal sealed class InputGuard : IDisposable
{
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    private readonly HookProc keyboardProc;
    private readonly HookProc mouseProc;
    private IntPtr keyboardHook;
    private IntPtr mouseHook;
    private bool controlDown;
    private bool altDown;
    private bool shiftDown;
    private bool unlockRequested;

    internal bool Active { get; private set; }
    internal IntPtr PasswordDialogHandle { get; set; }

    internal InputGuard()
    {
        keyboardProc = OnKeyboard;
        mouseProc = OnMouse;
    }

    internal void Start()
    {
        if (Active) return;
        var module = GetModuleHandle(null);
        keyboardHook = SetWindowsHookEx(13, keyboardProc, module, 0);
        if (keyboardHook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not install the keyboard filter.");
        mouseHook = SetWindowsHookEx(14, mouseProc, module, 0);
        if (mouseHook == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            Stop();
            throw new Win32Exception(error, "Could not install the mouse filter.");
        }
        Active = true;
    }

    internal bool ConsumeUnlockRequest()
    {
        var requested = unlockRequested;
        unlockRequested = false;
        return requested;
    }

    internal void Stop()
    {
        Active = false;
        PasswordDialogHandle = IntPtr.Zero;
        unlockRequested = false;
        controlDown = altDown = shiftDown = false;
        if (keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(keyboardHook);
        if (mouseHook != IntPtr.Zero) UnhookWindowsHookEx(mouseHook);
        keyboardHook = mouseHook = IntPtr.Zero;
    }

    private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || !Active) return CallNextHookEx(keyboardHook, code, message, data);
        var key = Marshal.PtrToStructure<KeyboardEvent>(data);
        if ((key.Flags & 0x10) != 0) return CallNextHookEx(keyboardHook, code, message, data);
        var down = message == (IntPtr)0x100 || message == (IntPtr)0x104;
        var up = message == (IntPtr)0x101 || message == (IntPtr)0x105;
        if (down || up)
        {
            var pressed = down;
            if (key.VirtualKey is 0x11 or 0xA2 or 0xA3) controlDown = pressed;
            if (key.VirtualKey is 0x12 or 0xA4 or 0xA5) altDown = pressed;
            if (key.VirtualKey is 0x10 or 0xA0 or 0xA1) shiftDown = pressed;
            if (down && key.VirtualKey == 0x55 && controlDown && altDown && shiftDown)
                unlockRequested = true; // Ctrl+Alt+Shift+U
        }
        var typingPassword = PasswordDialogHandle != IntPtr.Zero &&
            GetForegroundWindow() == PasswordDialogHandle;
        if (typingPassword && key.VirtualKey is not (0x09 or 0x12 or 0x5B or 0x5C or 0x73))
            return CallNextHookEx(keyboardHook, code, message, data);
        return (IntPtr)1;
    }

    private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || !Active) return CallNextHookEx(mouseHook, code, message, data);
        var mouse = Marshal.PtrToStructure<MouseEvent>(data);
        return (mouse.Flags & 1) != 0 ? CallNextHookEx(mouseHook, code, message, data) : (IntPtr)1;
    }

    public void Dispose() => Stop();

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardEvent
    {
        internal uint VirtualKey, ScanCode, Flags, Time;
        internal IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseEvent
    {
        internal int X, Y;
        internal uint MouseData, Flags, Time;
        internal IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int type, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? module);
}

internal static class GuardPassword
{
    internal static (string Salt, string Hash) Create(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 200_000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    internal static bool Verify(string password, string? saltText, string? hashText)
    {
        if (saltText is null || hashText is null) return false;
        try
        {
            var salt = Convert.FromBase64String(saltText);
            var saved = Convert.FromBase64String(hashText);
            if (salt.Length != 16 || saved.Length != 32) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 200_000, HashAlgorithmName.SHA256, saved.Length);
            return CryptographicOperations.FixedTimeEquals(actual, saved);
        }
        catch (FormatException) { return false; }
    }
}

internal static class PasswordPrompt
{
    internal static string? Show(string title, string instruction, InputGuard? guard = null,
        Func<string, bool>? accept = null, string failure = "Wrong password")
    {
        using var form = new Form
        {
            Text = title, ClientSize = new Size(360, 150), StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            ShowInTaskbar = false, TopMost = true
        };
        var label = new Label { Text = instruction, Location = new Point(18, 16), Size = new Size(324, 34) };
        var input = new TextBox { Location = new Point(18, 53), Size = new Size(324, 26), UseSystemPasswordChar = true };
        var error = new Label { Location = new Point(18, 84), Size = new Size(324, 20), ForeColor = Color.DarkRed };
        var ok = new Button { Text = "Continue", Location = new Point(159, 111), Size = new Size(88, 28) };
        var cancel = new Button { Text = "Cancel", Location = new Point(254, 111), Size = new Size(88, 28), DialogResult = DialogResult.Cancel };
        string? result = null;
        ok.Click += (_, _) =>
        {
            if (accept is not null && !accept(input.Text))
            {
                error.Text = failure;
                input.SelectAll();
                return;
            }
            result = input.Text;
            input.Clear();
            form.DialogResult = DialogResult.OK;
        };
        form.Controls.AddRange(new Control[] { label, input, error, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.Shown += (_, _) => { if (guard is not null) guard.PasswordDialogHandle = form.Handle; input.Focus(); };
        form.FormClosed += (_, _) => { if (guard is not null) guard.PasswordDialogHandle = IntPtr.Zero; };
        return form.ShowDialog() == DialogResult.OK ? result : null;
    }
}

internal static class InputGuardRecovery
{
    private static readonly string Marker = Path.Combine(RecoveryStore.DirectoryPath, "input-guard-active");

    internal static void Mark()
    {
        Directory.CreateDirectory(RecoveryStore.DirectoryPath);
        File.WriteAllText(Marker, "active");
    }

    internal static void Clear()
    {
        if (File.Exists(Marker)) File.Delete(Marker);
    }

    internal static void LockIfMarked()
    {
        if (!File.Exists(Marker)) return;
        if (LockWorkStation()) Clear();
    }

    internal static bool Lock() => LockWorkStation();

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();
}

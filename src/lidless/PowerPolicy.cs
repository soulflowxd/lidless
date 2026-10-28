using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Lidless;

internal static class PowerPolicy
{
    // Windows power setting: Power buttons and lid > Lid close action (0 = do nothing).
    private static readonly Guid Buttons = new("4f971e89-eebd-4455-a8de-9e59040e7347");
    private static readonly Guid LidAction = new("5ca83367-6e45-459f-a27b-476b1d01c936");

    [DllImport("powrprof.dll", SetLastError = true)] private static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);
    [DllImport("powrprof.dll", SetLastError = true)] private static extern uint PowerReadACValueIndex(IntPtr root, ref Guid scheme, ref Guid subgroup, ref Guid setting, out uint value);
    [DllImport("powrprof.dll", SetLastError = true)] private static extern uint PowerReadDCValueIndex(IntPtr root, ref Guid scheme, ref Guid subgroup, ref Guid setting, out uint value);
    [DllImport("powrprof.dll", SetLastError = true)] private static extern uint PowerWriteACValueIndex(IntPtr root, ref Guid scheme, ref Guid subgroup, ref Guid setting, uint value);
    [DllImport("powrprof.dll", SetLastError = true)] private static extern uint PowerWriteDCValueIndex(IntPtr root, ref Guid scheme, ref Guid subgroup, ref Guid setting, uint value);
    [DllImport("powrprof.dll", SetLastError = true)] private static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);

    private static void Check(uint result)
    {
        if (result != 0) throw new Win32Exception((int)result);
    }

    internal static Guid ActiveScheme()
    {
        Check(PowerGetActiveScheme(IntPtr.Zero, out var pointer));
        try { return Marshal.PtrToStructure<Guid>(pointer); }
        finally { LocalFree(pointer); }
    }

    internal static (uint ac, uint dc) Read(Guid scheme)
    {
        var buttons = Buttons;
        var lid = LidAction;
        Check(PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref buttons, ref lid, out var ac));
        Check(PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref buttons, ref lid, out var dc));
        return (ac, dc);
    }

    internal static void Write(Guid scheme, uint ac, uint dc, bool activate = true)
    {
        var buttons = Buttons;
        var lid = LidAction;
        Check(PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref buttons, ref lid, ac));
        Check(PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref buttons, ref lid, dc));
        if (activate) Check(PowerSetActiveScheme(IntPtr.Zero, ref scheme));
    }
}

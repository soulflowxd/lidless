using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Lidless;

internal static class SleepInhibitor
{
    private const uint Continuous = 0x80000000;
    private const uint SystemRequired = 0x00000001;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint flags);

    internal static void Enable()
    {
        // Keep the computer working while allowing its display to turn off.
        if (SetThreadExecutionState(Continuous | SystemRequired) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static void Disable() => SetThreadExecutionState(Continuous);
}

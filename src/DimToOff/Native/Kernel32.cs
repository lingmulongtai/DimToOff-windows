using System.Runtime.InteropServices;

namespace DimToOff.Native;

internal static class Kernel32
{
    [Flags]
    public enum ExecutionState : uint
    {
        None = 0x00000000,
        Continuous = 0x80000000,
        SystemRequired = 0x00000001,
        DisplayRequired = 0x00000002,
        UserPresent = 0x00000004
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);
}

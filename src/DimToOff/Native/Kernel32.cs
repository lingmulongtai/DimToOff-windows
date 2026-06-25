using System.Runtime.InteropServices;

namespace DimToOff.Native;

internal static class Kernel32
{
    [Flags]
    public enum ExecutionState : uint
    {
        Continuous = 0x80000000,
        SystemRequired = 0x00000001
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);
}

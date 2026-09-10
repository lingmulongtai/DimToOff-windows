using System.Runtime.InteropServices;

namespace DimToOff.Native;

internal static class PowrProf
{
    /// <summary>Information level for the system-wide EXECUTION_STATE flags.</summary>
    public const int SystemExecutionState = 16;

    public delegate void EffectivePowerModeCallback(int mode, nint context);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    public static extern int PowerRegisterForEffectivePowerModeNotifications(
        uint version,
        EffectivePowerModeCallback callback,
        nint context,
        out nint registrationHandle);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    public static extern int PowerUnregisterFromEffectivePowerModeNotifications(nint registrationHandle);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    public static extern int CallNtPowerInformation(
        int informationLevel,
        nint inputBuffer,
        uint inputBufferSize,
        out uint outputBuffer,
        uint outputBufferSize);
}

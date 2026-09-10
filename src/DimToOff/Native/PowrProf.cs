using System.Runtime.InteropServices;

namespace DimToOff.Native;

internal static class PowrProf
{
    public delegate void EffectivePowerModeCallback(int mode, nint context);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    public static extern int PowerRegisterForEffectivePowerModeNotifications(
        uint version,
        EffectivePowerModeCallback callback,
        nint context,
        out nint registrationHandle);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    public static extern int PowerUnregisterFromEffectivePowerModeNotifications(nint registrationHandle);
}

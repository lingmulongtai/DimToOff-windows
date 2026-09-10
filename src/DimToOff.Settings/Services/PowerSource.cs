using System.Runtime.InteropServices;

namespace DimToOff.Settings.Services;

/// <summary>Tells whether the PC is running from the wall or from its battery.</summary>
internal static class PowerSource
{
    private const byte AcLineOffline = 0;

    public static bool IsOnBattery() =>
        GetSystemPowerStatus(out SystemPowerStatus status) && status.AcLineStatus == AcLineOffline;

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}

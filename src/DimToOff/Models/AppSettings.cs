namespace DimToOff.Models;

internal sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public bool BrightnessBlackoutEnabled { get; set; } = true;
    public int OffThreshold { get; set; } = 1;
    public int DebounceMs { get; set; } = 800;
    public int CooldownMs { get; set; } = 1500;
    public int IgnoreInputMs { get; set; } = 300;
    public int BrightnessSaveStableMs { get; set; } = 2500;
    public int FadeToBlackMs { get; set; } = 280;
    public string DisplayOffMode { get; set; } = "Blackout";
    public string RestoreMode { get; set; } = "LastUsableWithMinimum";
    public int MinimumRestoreBrightness { get; set; } = 30;
    public int DefaultRestoreBrightness { get; set; } = 50;
    public bool StartWithWindows { get; set; }
    public bool ShowErrorNotifications { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;
    public int UpdateCheckIntervalHours { get; set; } = 24;
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }
    public string LastNotifiedUpdateVersion { get; set; } = string.Empty;
    public bool PreserveBrightnessOnPowerModeChange { get; set; } = true;
    public int BrightnessGuardWindowMs { get; set; } = 12000;
    public int BrightnessGuardTolerancePercent { get; set; } = 2;
    public bool ScreenSaverGuardEnabled { get; set; } = true;
    public string ScreenSaverGuardScope { get; set; } = "WhileBlanked";
    public bool ScreenSaverGuardSuspendsWindowsScreenSaver { get; set; } = true;
    public bool IdleBlackoutEnabled { get; set; } = true;
    public int IdleTimeoutPluggedInSeconds { get; set; } = 600;
    public int IdleTimeoutOnBatterySeconds { get; set; } = 300;
    public bool IdleRespectAppDisplayRequests { get; set; } = true;
    public bool IdleSkipWhileFullscreenApp { get; set; } = true;
    public bool IdleSkipWhenExternalMonitorConnected { get; set; }
}

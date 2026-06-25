namespace DimToOff.Models;

internal sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public int OffThreshold { get; set; } = 1;
    public int DebounceMs { get; set; } = 800;
    public int CooldownMs { get; set; } = 1500;
    public int IgnoreInputMs { get; set; } = 300;
    public string DisplayOffMode { get; set; } = "Blackout";
    public string RestoreMode { get; set; } = "LastUsableWithMinimum";
    public int MinimumRestoreBrightness { get; set; } = 30;
    public int DefaultRestoreBrightness { get; set; } = 50;
    public bool StartWithWindows { get; set; }
    public bool ShowErrorNotifications { get; set; } = true;
    public bool DisableWhileFullscreen { get; set; }
    public bool DisableWhenExternalMonitorConnected { get; set; }
}

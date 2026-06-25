namespace DimToOff.Models;

internal enum AppState
{
    Idle,
    PendingDisplayOff,
    DisplayOffByApp,
    RestoringBrightness,
    Cooldown
}

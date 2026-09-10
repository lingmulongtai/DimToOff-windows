namespace DimToOff.Models;

/// <summary>What made DimToOff blank the screen. It decides what waking has to undo.</summary>
internal enum BlackoutTrigger
{
    /// <summary>The user dialed brightness down to the off threshold.</summary>
    Brightness,

    /// <summary>The user was away for longer than the configured idle timeout.</summary>
    Idle,

    /// <summary>The user asked for it from the tray menu.</summary>
    Manual
}

using System.Runtime.InteropServices;

namespace DimToOff.Native;

internal static class Shell32
{
    /// <summary>
    /// Machine states reported by <see cref="SHQueryUserNotificationState"/>.
    /// Only used to decide whether a fullscreen or presentation app is on screen.
    /// </summary>
    public enum UserNotificationState
    {
        NotPresent = 1,
        Busy = 2,
        RunningDirect3DFullScreen = 3,
        PresentationMode = 4,
        AcceptsNotifications = 5,
        QuietTime = 6,
        App = 7
    }

    [DllImport("shell32.dll")]
    public static extern int SHQueryUserNotificationState(out UserNotificationState state);
}

using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using DimToOff.Native;

namespace DimToOff.Services;

/// <summary>
/// Keeps screen savers off the screen while DimToOff owns it, in two independent halves.
///
/// While the guard is active the Windows screen saver is not allowed to start. While the
/// black overlay is also up, a sweep watches for anything that still puts a full-screen
/// window in front of it. That second half is what catches the burn-in savers OEMs ship with
/// OLED laptops, such as ASUS OLED Care: they are ordinary always-on-top windows rather than
/// real screen savers, so no Windows setting stops them. Lighting the panel up to scroll a
/// picture across it is exactly what a blacked-out screen is trying to avoid, and it leaves
/// the blackout with nothing to show for itself.
///
/// Everything this changes is put back the moment the guard stands down, so quitting
/// DimToOff hands the OEM behavior straight back.
/// </summary>
internal sealed class ScreenSaverGuardService : IDisposable
{
    private const int SweepIntervalMs = 1000;

    /// <summary>How long the same intruder is left unlogged after the first report.</summary>
    private const int RepeatLogQuietMs = 60_000;

    /// <summary>Windows gives every real screen saver this class name.</summary>
    private const string WindowsScreenSaverClass = "WindowsScreenSaverClass";

    /// <summary>Slack for window borders when deciding whether a window covers a monitor.</summary>
    private const int FullscreenSlackPixels = 2;

    private readonly LogService log;
    private readonly System.Windows.Forms.Timer sweepTimer;
    private readonly int ownProcessId = Environment.ProcessId;
    private bool suspendsWindowsScreenSaver = true;
    private bool windowsScreenSaverSuspended;
    private string lastReportedIntruder = string.Empty;
    private long lastReportedTick;
    private bool active;
    private bool disposed;

    /// <summary>
    /// Raised on the UI thread when something else has taken the screen, with a short
    /// description of what it was. The owner decides how to take the screen back.
    /// </summary>
    public event EventHandler<string>? ScreenTaken;

    public ScreenSaverGuardService(LogService log)
    {
        this.log = log;
        sweepTimer = new System.Windows.Forms.Timer
        {
            Interval = SweepIntervalMs
        };
        sweepTimer.Tick += OnSweepTick;
    }

    public bool IsActive => active;

    /// <summary>
    /// Whether turning the guard on also switches the Windows screen saver off. The previous
    /// value is remembered and restored, so a user who does run a screen saver keeps it.
    /// </summary>
    public bool SuspendsWindowsScreenSaver
    {
        get => suspendsWindowsScreenSaver;
        set
        {
            if (suspendsWindowsScreenSaver == value)
            {
                return;
            }

            suspendsWindowsScreenSaver = value;

            if (!active)
            {
                return;
            }

            if (value)
            {
                SuspendWindowsScreenSaver();
            }
            else
            {
                ResumeWindowsScreenSaver();
            }
        }
    }

    /// <summary>Turns the guard on, which keeps the Windows screen saver from starting.</summary>
    public void SetActive(bool value)
    {
        if (disposed || active == value)
        {
            return;
        }

        active = value;

        if (value)
        {
            if (suspendsWindowsScreenSaver)
            {
                SuspendWindowsScreenSaver();
            }

            log.Info("Screen saver guard on");
            return;
        }

        SetWatchingBlackout(false);
        ResumeWindowsScreenSaver();
        log.Info("Screen saver guard off");
    }

    /// <summary>
    /// Starts and stops the sweep that watches for something taking the screen. It only makes
    /// sense while the black overlay is up, since that is the only time there is a screen to
    /// take back; with the panel powered off there is no window of ours to put back in front.
    /// </summary>
    public void SetWatchingBlackout(bool value)
    {
        if (disposed || sweepTimer.Enabled == value)
        {
            return;
        }

        if (value)
        {
            lastReportedIntruder = string.Empty;
            sweepTimer.Start();
            return;
        }

        sweepTimer.Stop();
    }

    private void OnSweepTick(object? sender, EventArgs e)
    {
        if (!active || disposed)
        {
            return;
        }

        try
        {
            string? intruder = FindIntruder();
            if (intruder is null)
            {
                lastReportedIntruder = string.Empty;
                return;
            }

            ReportIntruder(intruder);
        }
        catch (Exception ex)
        {
            log.Error("Screen saver guard sweep failed", ex);
        }
    }

    /// <summary>
    /// Names whatever has taken the screen from DimToOff, or null when nothing has.
    /// Only full-screen windows belonging to another process count, so notifications and
    /// other passing windows are left alone.
    /// </summary>
    private string? FindIntruder()
    {
        if (User32.SystemParametersInfo(NativeConstants.SPI_GETSCREENSAVERRUNNING, 0, out int running, 0) &&
            running != 0)
        {
            return "the Windows screen saver";
        }

        nint foreground = User32.GetForegroundWindow();
        if (foreground == nint.Zero || IsOwnWindow(foreground))
        {
            return null;
        }

        string className = GetClassName(foreground);
        if (string.Equals(className, WindowsScreenSaverClass, StringComparison.Ordinal))
        {
            return "a screen saver";
        }

        return CoversAScreen(foreground) ? DescribeWindow(foreground, className) : null;
    }

    private void ReportIntruder(string intruder)
    {
        long now = Environment.TickCount64;
        bool isRepeat = string.Equals(intruder, lastReportedIntruder, StringComparison.Ordinal) &&
            now - lastReportedTick < RepeatLogQuietMs;

        if (!isRepeat)
        {
            log.Info($"Taking the screen back from {intruder}");
            lastReportedTick = now;
        }

        lastReportedIntruder = intruder;
        ScreenTaken?.Invoke(this, intruder);
    }

    private bool IsOwnWindow(nint window)
    {
        User32.GetWindowThreadProcessId(window, out uint processId);
        return processId == (uint)ownProcessId;
    }

    private static bool CoversAScreen(nint window)
    {
        if (!User32.GetWindowRect(window, out User32.Rect rect))
        {
            return false;
        }

        foreach (Screen screen in Screen.AllScreens)
        {
            System.Drawing.Rectangle bounds = screen.Bounds;
            if (rect.Left <= bounds.Left + FullscreenSlackPixels &&
                rect.Top <= bounds.Top + FullscreenSlackPixels &&
                rect.Right >= bounds.Right - FullscreenSlackPixels &&
                rect.Bottom >= bounds.Bottom - FullscreenSlackPixels)
            {
                return true;
            }
        }

        return false;
    }

    private static string DescribeWindow(nint window, string className)
    {
        string? processName = GetProcessName(window);
        if (string.IsNullOrEmpty(processName))
        {
            return string.IsNullOrEmpty(className) ? "a full screen window" : $"a {className} window";
        }

        return string.IsNullOrEmpty(className) ? processName : $"{processName} ({className})";
    }

    private static string? GetProcessName(nint window)
    {
        try
        {
            User32.GetWindowThreadProcessId(window, out uint processId);
            if (processId == 0)
            {
                return null;
            }

            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static string GetClassName(nint window)
    {
        var buffer = new StringBuilder(256);
        int length = User32.GetClassName(window, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString(0, length) : string.Empty;
    }

    private void SuspendWindowsScreenSaver()
    {
        if (windowsScreenSaverSuspended)
        {
            return;
        }

        if (!User32.SystemParametersInfo(NativeConstants.SPI_GETSCREENSAVEACTIVE, 0, out int wasActive, 0))
        {
            log.Error("Failed to read whether the Windows screen saver is enabled");
            return;
        }

        if (wasActive == 0)
        {
            return;
        }

        if (!User32.SystemParametersInfo(
                NativeConstants.SPI_SETSCREENSAVEACTIVE,
                0,
                nint.Zero,
                NativeConstants.SPIF_SENDCHANGE))
        {
            log.Error("Failed to switch the Windows screen saver off");
            return;
        }

        windowsScreenSaverSuspended = true;
        log.Info("Windows screen saver suspended");
    }

    private void ResumeWindowsScreenSaver()
    {
        if (!windowsScreenSaverSuspended)
        {
            return;
        }

        windowsScreenSaverSuspended = false;

        if (!User32.SystemParametersInfo(
                NativeConstants.SPI_SETSCREENSAVEACTIVE,
                1,
                nint.Zero,
                NativeConstants.SPIF_SENDCHANGE))
        {
            log.Error("Failed to switch the Windows screen saver back on");
            return;
        }

        log.Info("Windows screen saver restored");
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        active = false;
        sweepTimer.Tick -= OnSweepTick;
        sweepTimer.Stop();
        sweepTimer.Dispose();
        ResumeWindowsScreenSaver();
    }
}

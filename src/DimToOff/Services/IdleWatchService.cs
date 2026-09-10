using System.Windows.Forms;
using DimToOff.Native;

namespace DimToOff.Services;

/// <summary>
/// Reports how long the user has been away from the keyboard, mouse, and touchpad.
/// Only the timestamp of the last input is read; nothing about the input itself is available.
/// </summary>
internal sealed class IdleWatchService : IDisposable
{
    private const int IdlePollIntervalMs = 1000;
    private const int WakePollIntervalMs = 100;

    private readonly LogService log;
    private readonly System.Windows.Forms.Timer timer;
    private bool disposed;

    /// <summary>Raised on the UI thread with the time since the last user input.</summary>
    public event EventHandler<TimeSpan>? Tick;

    public IdleWatchService(LogService log)
    {
        this.log = log;
        timer = new System.Windows.Forms.Timer
        {
            Interval = IdlePollIntervalMs
        };
        timer.Tick += OnTimerTick;
    }

    public void Start()
    {
        if (disposed || timer.Enabled)
        {
            return;
        }

        timer.Start();
        log.Info("Idle watch started");
    }

    public void Stop()
    {
        if (!timer.Enabled)
        {
            return;
        }

        timer.Stop();
        log.Info("Idle watch stopped");
    }

    /// <summary>
    /// Polls quickly while the screen is blanked so that the first touch of the keyboard
    /// or touchpad brings the desktop back without a visible delay.
    /// </summary>
    public void SetWakeWatchEnabled(bool enabled)
    {
        int interval = enabled ? WakePollIntervalMs : IdlePollIntervalMs;
        if (timer.Interval == interval)
        {
            return;
        }

        timer.Interval = interval;
    }

    public static TimeSpan GetIdleTime()
    {
        var info = new User32.LastInputInfo
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<User32.LastInputInfo>()
        };

        if (!User32.GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        uint elapsed = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(elapsed);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        Tick?.Invoke(this, GetIdleTime());
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        timer.Tick -= OnTimerTick;
        timer.Stop();
        timer.Dispose();
    }
}

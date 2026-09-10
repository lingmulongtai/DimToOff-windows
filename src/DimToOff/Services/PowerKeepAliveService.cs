using DimToOff.Native;

namespace DimToOff.Services;

internal enum KeepAliveMode
{
    /// <summary>Windows applies its own display and sleep timeouts.</summary>
    Off,

    /// <summary>Windows never blanks the display or sleeps on its own.</summary>
    DisplayAndSystem,

    /// <summary>Windows may blank the display, but never sleeps on its own.</summary>
    SystemOnly
}

/// <summary>
/// Keeps Windows out of its own display-off and sleep timeouts while DimToOff owns the screen.
/// The execution state is pulsed instead of held with ES_CONTINUOUS, so the system-wide
/// execution state keeps reflecting other applications only and can still be queried.
/// </summary>
internal sealed class PowerKeepAliveService : IDisposable
{
    /// <summary>Comfortably shorter than the one minute minimum Windows display timeout.</summary>
    private const int PulseIntervalMs = 25_000;

    private readonly LogService log;
    private readonly System.Threading.Timer timer;
    private readonly object gate = new();
    private KeepAliveMode mode = KeepAliveMode.Off;
    private bool disposed;

    public PowerKeepAliveService(LogService log)
    {
        this.log = log;
        timer = new System.Threading.Timer(_ => Pulse(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public KeepAliveMode Mode
    {
        get
        {
            lock (gate)
            {
                return mode;
            }
        }
    }

    public void SetMode(KeepAliveMode value)
    {
        lock (gate)
        {
            if (disposed || mode == value)
            {
                return;
            }

            mode = value;
            timer.Change(
                value == KeepAliveMode.Off ? Timeout.Infinite : 0,
                value == KeepAliveMode.Off ? Timeout.Infinite : PulseIntervalMs);
        }

        log.Info($"Power keep-alive mode: {value}");
    }

    /// <summary>
    /// True when another application asks Windows to keep the display on, for example a
    /// media player during playback. DimToOff never holds a continuous display request,
    /// so anything reported here belongs to someone else.
    /// </summary>
    public bool IsDisplayRequestedByAnotherApp()
    {
        try
        {
            int status = PowrProf.CallNtPowerInformation(
                PowrProf.SystemExecutionState,
                nint.Zero,
                0,
                out uint state,
                sizeof(uint));

            if (status != 0)
            {
                return false;
            }

            return ((Kernel32.ExecutionState)state & Kernel32.ExecutionState.DisplayRequired) != 0;
        }
        catch (Exception ex)
        {
            log.Error("Failed to read the system execution state", ex);
            return false;
        }
    }

    private void Pulse()
    {
        KeepAliveMode current = Mode;
        if (current == KeepAliveMode.Off)
        {
            return;
        }

        Kernel32.ExecutionState flags = Kernel32.ExecutionState.SystemRequired;
        if (current == KeepAliveMode.DisplayAndSystem)
        {
            flags |= Kernel32.ExecutionState.DisplayRequired;
        }

        if (Kernel32.SetThreadExecutionState(flags) == Kernel32.ExecutionState.None)
        {
            log.Error("Failed to reset the Windows idle timers");
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            mode = KeepAliveMode.Off;
        }

        timer.Dispose();
        Kernel32.SetThreadExecutionState(Kernel32.ExecutionState.Continuous);
    }
}

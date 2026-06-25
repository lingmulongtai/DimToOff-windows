using System.Management;

namespace DimToOff.Services;

internal sealed class BrightnessService : IDisposable
{
    private readonly LogService log;
    private readonly System.Threading.Timer pollingTimer;
    private ManagementEventWatcher? watcher;
    private int? lastReportedBrightness;
    private bool disposed;

    public event EventHandler<int>? BrightnessChanged;

    public BrightnessService(LogService log)
    {
        this.log = log;
        pollingTimer = new System.Threading.Timer(PollBrightness, null, Timeout.Infinite, Timeout.Infinite);
    }

    public int? GetCurrentBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\wmi",
                "SELECT * FROM WmiMonitorBrightness WHERE Active = TRUE");

            foreach (ManagementObject monitor in searcher.Get())
            {
                if (monitor["CurrentBrightness"] is byte brightness)
                {
                    return brightness;
                }

                if (monitor["CurrentBrightness"] is uint value)
                {
                    return (int)value;
                }
            }
        }
        catch (Exception ex)
        {
            log.Error("Failed to get current brightness", ex);
        }

        return null;
    }

    public void SetBrightness(int brightness)
    {
        int clamped = Math.Clamp(brightness, 0, 100);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\wmi",
                "SELECT * FROM WmiMonitorBrightnessMethods");

            foreach (ManagementObject monitor in searcher.Get())
            {
                monitor.InvokeMethod("WmiSetBrightness", new object[] { 0, clamped });
            }

            log.Info($"Brightness restore requested: {clamped}%");
        }
        catch (Exception ex)
        {
            log.Error("Failed to set brightness", ex);
        }
    }

    public void StartWatching()
    {
        int? current = GetCurrentBrightness();
        if (current.HasValue)
        {
            lastReportedBrightness = current.Value;
            log.Info($"Current brightness: {current.Value}%");
        }

        try
        {
            var scope = new ManagementScope("root\\wmi");
            var query = new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent");
            watcher = new ManagementEventWatcher(scope, query);
            watcher.EventArrived += OnWmiBrightnessChanged;
            watcher.Start();
            log.Info("WMI brightness watcher started");
        }
        catch (Exception ex)
        {
            log.Error("WMI brightness watcher unavailable. Polling mode active", ex);
        }
        finally
        {
            pollingTimer.Change(1000, 1000);
        }
    }

    public void StopWatching()
    {
        pollingTimer.Change(Timeout.Infinite, Timeout.Infinite);

        if (watcher is null)
        {
            return;
        }

        try
        {
            watcher.EventArrived -= OnWmiBrightnessChanged;
            watcher.Stop();
            watcher.Dispose();
            log.Info("WMI brightness watcher stopped");
        }
        catch (Exception ex)
        {
            log.Error("Failed to stop WMI brightness watcher", ex);
        }
        finally
        {
            watcher = null;
        }
    }

    private void OnWmiBrightnessChanged(object sender, EventArrivedEventArgs e)
    {
        try
        {
            object? value = e.NewEvent.Properties["Brightness"]?.Value;
            if (TryConvertBrightness(value, out int brightness))
            {
                PublishBrightness(brightness, "WMI event");
            }
        }
        catch (Exception ex)
        {
            log.Error("Failed to handle WMI brightness event", ex);
        }
    }

    private void PollBrightness(object? state)
    {
        int? current = GetCurrentBrightness();
        if (current.HasValue)
        {
            PublishBrightness(current.Value, "poll");
        }
    }

    private void PublishBrightness(int brightness, string source)
    {
        brightness = Math.Clamp(brightness, 0, 100);
        if (lastReportedBrightness == brightness)
        {
            return;
        }

        lastReportedBrightness = brightness;
        log.Info($"Brightness changed via {source}: {brightness}%");
        BrightnessChanged?.Invoke(this, brightness);
    }

    private static bool TryConvertBrightness(object? value, out int brightness)
    {
        brightness = 0;

        switch (value)
        {
            case byte byteValue:
                brightness = byteValue;
                return true;
            case uint uintValue:
                brightness = (int)uintValue;
                return true;
            case int intValue:
                brightness = intValue;
                return true;
            default:
                return false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        StopWatching();
        pollingTimer.Dispose();
    }
}

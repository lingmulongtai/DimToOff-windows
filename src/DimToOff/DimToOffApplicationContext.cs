using System.Drawing;
using System.Windows.Forms;
using DimToOff.Models;
using DimToOff.Services;
using DimToOff.UI;

namespace DimToOff;

internal sealed class DimToOffApplicationContext : ApplicationContext
{
    private readonly LogService log;
    private readonly SettingsService settingsService;
    private readonly AppSettings settings;
    private readonly BrightnessService brightnessService;
    private readonly DisplayPowerService displayPowerService;
    private readonly InputHookService inputHookService;
    private readonly TrayIconManager trayIconManager;
    private readonly Form messageWindow;
    private readonly object stateLock = new();
    private CancellationTokenSource? debounceCts;
    private AppState state = AppState.Idle;
    private int lastUsableBrightness;
    private DateTimeOffset lastOffTime;
    private bool disposed;

    public DimToOffApplicationContext()
    {
        log = new LogService();
        settingsService = new SettingsService(log);
        settings = settingsService.Load();
        lastUsableBrightness = settings.DefaultRestoreBrightness;

        brightnessService = new BrightnessService(log);
        displayPowerService = new DisplayPowerService(log);
        inputHookService = new InputHookService(log);
        trayIconManager = new TrayIconManager(settings, settingsService);
        messageWindow = new HiddenMessageWindow();
        messageWindow.CreateControl();

        brightnessService.BrightnessChanged += OnBrightnessChanged;
        inputHookService.UserInputDetected += OnUserInputDetected;
        trayIconManager.TurnOffDisplayRequested += (_, _) => TurnDisplayOffByApp();
        trayIconManager.RestoreBrightnessRequested += async (_, _) => await RestoreBrightnessAfterWakeAsync();
        trayIconManager.ExitRequested += (_, _) => ExitThread();

        log.Info("DimToOff started");
        InitializeBrightness();
        brightnessService.StartWatching();
    }

    private void InitializeBrightness()
    {
        int? current = brightnessService.GetCurrentBrightness();
        if (current.HasValue && current.Value > settings.OffThreshold)
        {
            lastUsableBrightness = current.Value;
            log.Info($"Initial last usable brightness: {lastUsableBrightness}%");
        }
        else
        {
            log.Info($"Initial brightness is unavailable or below threshold. Default restore brightness will be used: {lastUsableBrightness}%");
        }
    }

    private void OnBrightnessChanged(object? sender, int brightness)
    {
        lock (stateLock)
        {
            if (!settings.Enabled)
            {
                return;
            }

            if (state is AppState.Cooldown or AppState.RestoringBrightness or AppState.DisplayOffByApp)
            {
                return;
            }

            if (brightness > settings.OffThreshold)
            {
                lastUsableBrightness = brightness;
                CancelPendingDisplayOff();
                state = AppState.Idle;
                return;
            }

            if (brightness <= settings.OffThreshold && state == AppState.Idle)
            {
                state = AppState.PendingDisplayOff;
                StartDebounceTimer();
            }
        }
    }

    private void StartDebounceTimer()
    {
        CancelPendingDisplayOff();
        debounceCts = new CancellationTokenSource();
        CancellationToken token = debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(settings.DebounceMs, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                int? current = brightnessService.GetCurrentBrightness();
                if (current.HasValue && current.Value <= settings.OffThreshold)
                {
                    PostToUiThread(TurnDisplayOffByApp);
                    return;
                }

                lock (stateLock)
                {
                    state = AppState.Idle;
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                log.Error("Debounce timer failed", ex);
                lock (stateLock)
                {
                    state = AppState.Idle;
                }
            }
        });
    }

    private void TurnDisplayOffByApp()
    {
        lock (stateLock)
        {
            if (!settings.Enabled)
            {
                state = AppState.Idle;
                return;
            }

            if (state is AppState.DisplayOffByApp or AppState.RestoringBrightness or AppState.Cooldown)
            {
                return;
            }

            state = AppState.DisplayOffByApp;
            lastOffTime = DateTimeOffset.Now;
        }

        CancelPendingDisplayOff();
        inputHookService.Start();
        displayPowerService.TurnOffDisplay();
    }

    private void OnUserInputDetected(object? sender, EventArgs e)
    {
        lock (stateLock)
        {
            if (state != AppState.DisplayOffByApp)
            {
                return;
            }

            if ((DateTimeOffset.Now - lastOffTime).TotalMilliseconds < settings.IgnoreInputMs)
            {
                return;
            }

            state = AppState.RestoringBrightness;
        }

        PostToUiThread(async () => await RestoreBrightnessAfterWakeAsync());
    }

    private async Task RestoreBrightnessAfterWakeAsync()
    {
        lock (stateLock)
        {
            if (state != AppState.RestoringBrightness)
            {
                state = AppState.RestoringBrightness;
            }
        }

        try
        {
            await Task.Delay(200);
            int target = CalculateRestoreBrightness();
            brightnessService.SetBrightness(target);
            inputHookService.Stop();

            lock (stateLock)
            {
                state = AppState.Cooldown;
            }

            await Task.Delay(settings.CooldownMs);

            lock (stateLock)
            {
                state = AppState.Idle;
            }
        }
        catch (Exception ex)
        {
            log.Error("Failed to restore brightness", ex);
            trayIconManager.ShowError("DimToOff", "Failed to restore brightness. See the log for details.");

            lock (stateLock)
            {
                state = AppState.Idle;
            }
        }
    }

    private int CalculateRestoreBrightness()
    {
        int target = lastUsableBrightness;

        if (target <= settings.OffThreshold)
        {
            target = settings.DefaultRestoreBrightness;
        }

        if (string.Equals(settings.RestoreMode, "LastUsableWithMinimum", StringComparison.OrdinalIgnoreCase))
        {
            target = Math.Max(target, settings.MinimumRestoreBrightness);
        }

        return Math.Clamp(target, 0, 100);
    }

    private void CancelPendingDisplayOff()
    {
        debounceCts?.Cancel();
        debounceCts?.Dispose();
        debounceCts = null;
    }

    private void PostToUiThread(Action action)
    {
        if (messageWindow.IsHandleCreated && messageWindow.InvokeRequired)
        {
            messageWindow.BeginInvoke(action);
            return;
        }

        action();
    }

    protected override void ExitThreadCore()
    {
        Dispose();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (disposing)
        {
            log.Info("DimToOff stopping");
            CancelPendingDisplayOff();
            brightnessService.BrightnessChanged -= OnBrightnessChanged;
            inputHookService.UserInputDetected -= OnUserInputDetected;
            brightnessService.Dispose();
            inputHookService.Dispose();
            trayIconManager.Dispose();
            messageWindow.Dispose();
            log.Info("DimToOff stopped");
        }

        base.Dispose(disposing);
    }

    private sealed class HiddenMessageWindow : Form
    {
        public HiddenMessageWindow()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(1, 1);
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }
    }
}

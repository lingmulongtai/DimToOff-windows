using System.Diagnostics;
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
    private readonly StartupService startupService;
    private readonly AppSettings settings;
    private readonly BrightnessService brightnessService;
    private readonly DisplayPowerService displayPowerService;
    private readonly InputHookService inputHookService;
    private readonly BlackoutService blackoutService;
    private readonly UiCommandService uiCommandService;
    private readonly TrayIconManager trayIconManager;
    private readonly Form messageWindow;
    private readonly int uiThreadId;
    private readonly object stateLock = new();
    private CancellationTokenSource? debounceCts;
    private CancellationTokenSource? brightnessSaveCts;
    private AppState state = AppState.Idle;
    private int lastUsableBrightness;
    private DateTimeOffset lastOffTime;
    private DateTimeOffset suppressAutoOffUntil = DateTimeOffset.MinValue;
    private bool disposed;

    public DimToOffApplicationContext()
    {
        uiThreadId = Environment.CurrentManagedThreadId;
        log = new LogService();
        settingsService = new SettingsService(log);
        startupService = new StartupService(log);
        settings = settingsService.Load();
        settings.StartWithWindows = startupService.IsEnabled();
        settingsService.Save(settings);
        lastUsableBrightness = settings.DefaultRestoreBrightness;

        brightnessService = new BrightnessService(log);
        displayPowerService = new DisplayPowerService(log);
        inputHookService = new InputHookService(log);
        blackoutService = new BlackoutService(log);
        uiCommandService = new UiCommandService(log);
        trayIconManager = new TrayIconManager(settings, settingsService);
        messageWindow = new HiddenMessageWindow();
        _ = messageWindow.Handle;

        brightnessService.BrightnessChanged += OnBrightnessChanged;
        inputHookService.UserInputDetected += OnUserInputDetected;
        blackoutService.UserInputDetected += OnUserInputDetected;
        trayIconManager.SettingsRequested += (_, _) => ShowSettings();
        trayIconManager.TrayMenuRequested += (_, _) => ShowTrayMenu();
        uiCommandService.CommandReceived += OnUiCommandReceived;

        log.Info("DimToOff started");
        InitializeBrightness();
        uiCommandService.Start();
        brightnessService.StartWatching();
    }

    private void InitializeBrightness()
    {
        int? current = brightnessService.GetCurrentBrightness();
        if (current.HasValue && IsComfortableBrightness(current.Value))
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
        bool restoreBecauseBrightnessReturned = false;

        lock (stateLock)
        {
            if (!settings.Enabled)
            {
                return;
            }

            if (state == AppState.DisplayOffByApp && brightness > settings.OffThreshold)
            {
                state = AppState.RestoringBrightness;
                restoreBecauseBrightnessReturned = true;
            }
            else if (state is AppState.Cooldown or AppState.RestoringBrightness)
            {
                return;
            }
            else if (brightness > settings.OffThreshold)
            {
                if (IsComfortableBrightness(brightness))
                {
                    ScheduleLastUsableBrightnessCommit(brightness);
                }
                else
                {
                    CancelPendingBrightnessSave();
                }

                CancelPendingDisplayOff();
                state = AppState.Idle;
                return;
            }
            else if (brightness <= settings.OffThreshold && state == AppState.Idle)
            {
                CancelPendingBrightnessSave();
                if (DateTimeOffset.Now < suppressAutoOffUntil)
                {
                    log.Info("Auto-off ignored during restore safety window");
                    return;
                }

                state = AppState.PendingDisplayOff;
                StartDebounceTimer();
            }
        }

        if (restoreBecauseBrightnessReturned)
        {
            log.Info($"Brightness returned to {brightness}%, hiding blackout");
            QueueToUiThread(async () => await RestoreFromBrightnessChangeAsync());
        }
    }

    private void ShowSettings()
    {
        LaunchWinUiSurface("--settings", reloadSettingsOnExit: true);
    }

    private void ShowTrayMenu()
    {
        LaunchWinUiSurface("--tray-menu", reloadSettingsOnExit: false);
    }

    private void LaunchWinUiSurface(string mode, bool reloadSettingsOnExit)
    {
        string? settingsAppPath = ResolveSettingsAppPath();
        if (settingsAppPath is null)
        {
            trayIconManager.ShowError("DimToOff", "The WinUI settings app could not be found. Build or publish the full solution.");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = settingsAppPath,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(mode);
            startInfo.ArgumentList.Add("--main-exe");
            startInfo.ArgumentList.Add(Application.ExecutablePath);
            startInfo.ArgumentList.Add("--pipe");
            startInfo.ArgumentList.Add(UiCommandService.PipeName);

            Process? process = Process.Start(startInfo);
            if (process is not null && reloadSettingsOnExit)
            {
                process.EnableRaisingEvents = true;
                process.Exited += (_, _) => QueueToUiThread(ReloadSettingsFromDisk);
            }
        }
        catch (Exception ex)
        {
            log.Error("Failed to launch WinUI surface", ex);
            trayIconManager.ShowError("DimToOff", "Failed to open the WinUI interface. See the log for details.");
        }
    }

    private static string? ResolveSettingsAppPath()
    {
        string sameFolderPath = Path.Combine(AppContext.BaseDirectory, "DimToOff.Settings.exe");
        if (File.Exists(sameFolderPath))
        {
            return sameFolderPath;
        }

#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        string projectOutputPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DimToOff.Settings",
            "bin",
            configuration,
            "net8.0-windows10.0.19041.0",
            "DimToOff.Settings.exe"));

        if (File.Exists(projectOutputPath))
        {
            return projectOutputPath;
        }

        string ridOutputPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DimToOff.Settings",
            "bin",
            configuration,
            "net8.0-windows10.0.19041.0",
            "win-x64",
            "DimToOff.Settings.exe"));

        return File.Exists(ridOutputPath) ? ridOutputPath : null;
    }

    private void OnUiCommandReceived(object? sender, string command)
    {
        QueueToUiThread(() => HandleUiCommand(command));
    }

    private void HandleUiCommand(string command)
    {
        string[] parts = command.Split('=', 2, StringSplitOptions.TrimEntries);
        string name = parts[0].ToLowerInvariant();
        string value = parts.Length > 1 ? parts[1] : string.Empty;

        switch (name)
        {
            case "blank":
                TurnDisplayOffByApp();
                break;
            case "restore":
                _ = RestoreBrightnessAfterWakeAsync();
                break;
            case "settings":
                ShowSettings();
                break;
            case "reload-settings":
                ReloadSettingsFromDisk();
                break;
            case "set-enabled" when bool.TryParse(value, out bool enabled):
                settings.Enabled = enabled;
                settingsService.Save(settings);
                trayIconManager.RefreshSettings();
                break;
            case "set-startup" when bool.TryParse(value, out bool startWithWindows):
                startupService.SetEnabled(startWithWindows);
                settings.StartWithWindows = startupService.IsEnabled();
                settingsService.Save(settings);
                trayIconManager.RefreshSettings();
                break;
            case "about":
                OpenGitHub();
                break;
            case "exit":
                ExitThread();
                break;
            default:
                log.Info($"Unknown UI command ignored: {command}");
                break;
        }
    }

    private void ReloadSettingsFromDisk()
    {
        AppSettings updated = settingsService.Load();
        updated.StartWithWindows = startupService.IsEnabled();
        ApplySettings(updated);
        settingsService.Save(settings);
        trayIconManager.RefreshSettings();
        log.Info("Settings reloaded from WinUI");
    }

    private void ApplySettings(AppSettings updated)
    {
        settings.Enabled = updated.Enabled;
        settings.OffThreshold = updated.OffThreshold;
        settings.DebounceMs = updated.DebounceMs;
        settings.CooldownMs = updated.CooldownMs;
        settings.IgnoreInputMs = updated.IgnoreInputMs;
        settings.BrightnessSaveStableMs = updated.BrightnessSaveStableMs;
        settings.FadeToBlackMs = updated.FadeToBlackMs;
        settings.DisplayOffMode = updated.DisplayOffMode;
        settings.RestoreMode = updated.RestoreMode;
        settings.MinimumRestoreBrightness = updated.MinimumRestoreBrightness;
        settings.DefaultRestoreBrightness = updated.DefaultRestoreBrightness;
        settings.StartWithWindows = updated.StartWithWindows;
        settings.ShowErrorNotifications = updated.ShowErrorNotifications;
        settings.DisableWhileFullscreen = updated.DisableWhileFullscreen;
        settings.DisableWhenExternalMonitorConnected = updated.DisableWhenExternalMonitorConnected;
    }

    private static void OpenGitHub()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/lingmulongtai/DimToOff-windows",
            UseShellExecute = true
        });
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
                    PostToUiThread(() => TurnDisplayOffByApp(force: false));
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

    private void TurnDisplayOffByApp(bool force = true)
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
        CancelPendingBrightnessSave();
        displayPowerService.PreventSystemSleepWhileDisplayIsBlanked();

        if (UseBlackoutMode())
        {
            blackoutService.Show(settings.FadeToBlackMs);
        }
        else
        {
            inputHookService.Start();
            displayPowerService.TurnOffDisplay();
        }
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

        QueueToUiThread(async () => await RestoreBrightnessAfterWakeAsync());
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
            if (UseBlackoutMode())
            {
                blackoutService.Hide();
                await Task.Delay(100);
            }
            else
            {
                inputHookService.Stop();
                displayPowerService.TurnOnDisplay();
                await Task.Delay(700);
            }

            int target = CalculateRestoreBrightness();
            await Task.Run(() => brightnessService.SetBrightness(target));
            displayPowerService.AllowNormalSleepPolicy();

            lock (stateLock)
            {
                state = AppState.Cooldown;
                suppressAutoOffUntil = DateTimeOffset.Now.AddSeconds(5);
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
            blackoutService.Hide();
            displayPowerService.AllowNormalSleepPolicy();
            trayIconManager.ShowError("DimToOff", "Failed to restore brightness. See the log for details.");

            lock (stateLock)
            {
                suppressAutoOffUntil = DateTimeOffset.Now.AddSeconds(10);
                state = AppState.Idle;
            }
        }
    }

    private async Task RestoreFromBrightnessChangeAsync()
    {
        try
        {
            if (!UseBlackoutMode())
            {
                inputHookService.Stop();
            }

            blackoutService.Hide();
            displayPowerService.AllowNormalSleepPolicy();

            lock (stateLock)
            {
                state = AppState.Cooldown;
                suppressAutoOffUntil = DateTimeOffset.Now.AddSeconds(2);
            }

            await Task.Delay(settings.CooldownMs);

            lock (stateLock)
            {
                state = AppState.Idle;
            }
        }
        catch (Exception ex)
        {
            log.Error("Failed to hide blackout after brightness returned", ex);
            blackoutService.Hide();
            displayPowerService.AllowNormalSleepPolicy();

            lock (stateLock)
            {
                suppressAutoOffUntil = DateTimeOffset.Now.AddSeconds(10);
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

    private bool IsComfortableBrightness(int brightness) =>
        brightness >= Math.Max(settings.OffThreshold + 1, settings.MinimumRestoreBrightness);

    private bool UseBlackoutMode() =>
        string.Equals(settings.DisplayOffMode, "Blackout", StringComparison.OrdinalIgnoreCase);

    private void CancelPendingDisplayOff()
    {
        debounceCts?.Cancel();
        debounceCts?.Dispose();
        debounceCts = null;
    }

    private void ScheduleLastUsableBrightnessCommit(int brightness)
    {
        CancelPendingBrightnessSave();
        brightnessSaveCts = new CancellationTokenSource();
        CancellationToken token = brightnessSaveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(settings.BrightnessSaveStableMs, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                int? current = brightnessService.GetCurrentBrightness();
                if (!current.HasValue || current.Value != brightness || !IsComfortableBrightness(current.Value))
                {
                    return;
                }

                lock (stateLock)
                {
                    if (state == AppState.Idle)
                    {
                        lastUsableBrightness = brightness;
                        log.Info($"Last usable brightness committed after stable delay: {lastUsableBrightness}%");
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                log.Error("Failed to commit stable brightness", ex);
            }
        });
    }

    private void CancelPendingBrightnessSave()
    {
        brightnessSaveCts?.Cancel();
        brightnessSaveCts?.Dispose();
        brightnessSaveCts = null;
    }

    private void PostToUiThread(Action action)
    {
        if (Environment.CurrentManagedThreadId == uiThreadId)
        {
            action();
            return;
        }

        if (!messageWindow.IsHandleCreated)
        {
            log.Error("UI invoker handle is not available; action was not posted");
            return;
        }

        try
        {
            messageWindow.BeginInvoke(action);
        }
        catch (InvalidOperationException ex)
        {
            log.Error("Failed to post action to UI thread", ex);
        }
    }

    private void QueueToUiThread(Action action)
    {
        if (!messageWindow.IsHandleCreated)
        {
            log.Error("UI invoker handle is not available; action was not queued");
            return;
        }

        try
        {
            messageWindow.BeginInvoke(action);
        }
        catch (InvalidOperationException ex)
        {
            log.Error("Failed to queue action to UI thread", ex);
        }
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
            CancelPendingBrightnessSave();
            brightnessService.BrightnessChanged -= OnBrightnessChanged;
            inputHookService.UserInputDetected -= OnUserInputDetected;
            blackoutService.UserInputDetected -= OnUserInputDetected;
            uiCommandService.CommandReceived -= OnUiCommandReceived;
            uiCommandService.Dispose();
            blackoutService.Dispose();
            displayPowerService.AllowNormalSleepPolicy();
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

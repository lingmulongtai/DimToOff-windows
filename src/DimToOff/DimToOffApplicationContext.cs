using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using DimToOff.Models;
using DimToOff.Native;
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
    private readonly UpdateCheckService updateCheckService;
    private readonly UiCommandService uiCommandService;
    private readonly TrayIconManager trayIconManager;
    private readonly Form messageWindow;
    private readonly int uiThreadId;
    private readonly object stateLock = new();
    private readonly object winUiProcessLock = new();
    private readonly List<Process> winUiProcesses = new();
    private Process? settingsProcess;
    private CancellationTokenSource? debounceCts;
    private CancellationTokenSource? brightnessSaveCts;
    private CancellationTokenSource? updateCheckCts;
    private CancellationTokenSource? brightnessGuardRestoreCts;
    private AppState state = AppState.Idle;
    private int lastUsableBrightness;
    private int lastStableBrightness;
    private int brightnessGuardTarget;
    private DateTimeOffset lastOffTime;
    private DateTimeOffset brightnessGuardArmedUntil = DateTimeOffset.MinValue;
    private DateTimeOffset suppressBrightnessGuardUntil = DateTimeOffset.MinValue;
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
        updateCheckService = new UpdateCheckService(log);
        uiCommandService = new UiCommandService(log);
        trayIconManager = new TrayIconManager(settings, settingsService);
        var hiddenMessageWindow = new HiddenMessageWindow(log);
        hiddenMessageWindow.PowerModeMayHaveChanged += OnPowerModeMayHaveChanged;
        messageWindow = hiddenMessageWindow;
        _ = messageWindow.Handle;

        brightnessService.BrightnessChanged += OnBrightnessChanged;
        inputHookService.UserInputDetected += OnUserInputDetected;
        blackoutService.UserInputDetected += OnUserInputDetected;
        trayIconManager.SettingsRequested += (_, _) => ShowSettings();
        trayIconManager.TrayMenuRequested += (_, _) => ShowTrayMenu();
        trayIconManager.BalloonUrlRequested += OnBalloonUrlRequested;
        uiCommandService.CommandReceived += OnUiCommandReceived;

        log.Info("DimToOff started");
        InitializeBrightness();
        uiCommandService.Start();
        brightnessService.StartWatching();
        RestartUpdateChecks();
    }

    private void InitializeBrightness()
    {
        int? current = brightnessService.GetCurrentBrightness();
        if (current.HasValue && IsComfortableBrightness(current.Value))
        {
            lastUsableBrightness = current.Value;
            lastStableBrightness = current.Value;
            log.Info($"Initial last usable brightness: {lastUsableBrightness}%");
        }
        else if (current.HasValue && IsAboveOffThreshold(current.Value))
        {
            lastStableBrightness = current.Value;
            log.Info($"Initial brightness for power-mode guard: {lastStableBrightness}%");
        }
        else
        {
            lastStableBrightness = settings.DefaultRestoreBrightness;
            log.Info($"Initial brightness is unavailable or below threshold. Default restore brightness will be used: {lastUsableBrightness}%");
        }
    }

    private void OnBrightnessChanged(object? sender, int brightness)
    {
        bool restoreBecauseBrightnessReturned = false;
        int? brightnessGuardRestoreTarget = null;

        lock (stateLock)
        {
            if (!settings.Enabled)
            {
                return;
            }

            if (TryHandlePowerModeBrightnessChangeLocked(brightness, out int guardTarget))
            {
                brightnessGuardRestoreTarget = guardTarget;
            }
            else if (state == AppState.DisplayOffByApp && brightness > settings.OffThreshold)
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
                ScheduleStableBrightnessCommit(brightness);
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

        if (brightnessGuardRestoreTarget.HasValue)
        {
            QueueToUiThread(async () => await RestoreBrightnessForPowerModeGuardAsync(brightnessGuardRestoreTarget.Value));
            return;
        }

        if (restoreBecauseBrightnessReturned)
        {
            log.Info($"Brightness returned to {brightness}%, hiding blackout");
            QueueToUiThread(async () => await RestoreFromBrightnessChangeAsync());
        }
    }

    private void ShowSettings()
    {
        if (TryActivateExistingSettings())
        {
            return;
        }

        LaunchWinUiSurface("--settings", reloadSettingsOnExit: true, trackAsSettings: true);
    }

    private void ShowTrayMenu()
    {
        LaunchWinUiSurface("--tray-menu", reloadSettingsOnExit: false);
    }

    private bool TryActivateExistingSettings()
    {
        Process? process;
        lock (winUiProcessLock)
        {
            process = settingsProcess;
            if (process is null)
            {
                return false;
            }

            if (process.HasExited)
            {
                winUiProcesses.Remove(process);
                settingsProcess = null;
                process.Dispose();
                return false;
            }
        }

        WindowActivationService.TryActivate(process, log);
        return true;
    }

    private void LaunchWinUiSurface(string mode, bool reloadSettingsOnExit, bool trackAsSettings = false)
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
            if (process is not null)
            {
                process.EnableRaisingEvents = true;
                TrackWinUiProcess(process, trackAsSettings);
                if (reloadSettingsOnExit)
                {
                    process.Exited += (_, _) =>
                    {
                        if (!disposed)
                        {
                            QueueToUiThread(ReloadSettingsFromDisk);
                        }
                    };
                }
            }
        }
        catch (Exception ex)
        {
            log.Error("Failed to launch WinUI surface", ex);
            trayIconManager.ShowError("DimToOff", "Failed to open the WinUI interface. See the log for details.");
        }
    }

    private void TrackWinUiProcess(Process process, bool trackAsSettings)
    {
        lock (winUiProcessLock)
        {
            winUiProcesses.Add(process);
            if (trackAsSettings)
            {
                settingsProcess = process;
            }
        }

        process.Exited += (_, _) =>
        {
            bool shouldDispose;
            lock (winUiProcessLock)
            {
                shouldDispose = winUiProcesses.Remove(process);
                if (ReferenceEquals(settingsProcess, process))
                {
                    settingsProcess = null;
                }
            }

            if (shouldDispose)
            {
                process.Dispose();
            }
        };
    }

    private void CloseWinUiSurfaces()
    {
        Process[] processes;
        lock (winUiProcessLock)
        {
            processes = winUiProcesses.ToArray();
            winUiProcesses.Clear();
            settingsProcess = null;
        }

        foreach (Process process in processes)
        {
            try
            {
                if (process.HasExited)
                {
                    process.Dispose();
                    continue;
                }

                if (process.CloseMainWindow() && process.WaitForExit(800))
                {
                    process.Dispose();
                    continue;
                }

                process.Kill(entireProcessTree: true);
                process.WaitForExit(800);
                process.Dispose();
            }
            catch (Exception ex)
            {
                log.Error("Failed to close WinUI surface during exit", ex);
            }
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
            case "check-updates":
                _ = CheckForUpdatesAsync(manual: true);
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
                ShowAbout();
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
        bool updateSettingsChanged =
            settings.CheckForUpdates != updated.CheckForUpdates ||
            settings.UpdateCheckIntervalHours != updated.UpdateCheckIntervalHours;

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
        settings.CheckForUpdates = updated.CheckForUpdates;
        settings.UpdateCheckIntervalHours = updated.UpdateCheckIntervalHours;
        settings.LastUpdateCheckUtc = updated.LastUpdateCheckUtc;
        settings.LastNotifiedUpdateVersion = updated.LastNotifiedUpdateVersion;
        settings.PreserveBrightnessOnPowerModeChange = updated.PreserveBrightnessOnPowerModeChange;
        settings.BrightnessGuardWindowMs = updated.BrightnessGuardWindowMs;
        settings.BrightnessGuardTolerancePercent = updated.BrightnessGuardTolerancePercent;
        settings.DisableWhileFullscreen = updated.DisableWhileFullscreen;
        settings.DisableWhenExternalMonitorConnected = updated.DisableWhenExternalMonitorConnected;

        if (updateSettingsChanged)
        {
            RestartUpdateChecks();
        }
    }

    private void ShowAbout()
    {
        LaunchWinUiSurface("--about", reloadSettingsOnExit: false);
    }

    private void OnPowerModeMayHaveChanged(object? sender, EventArgs e)
    {
        if (!settings.Enabled || !settings.PreserveBrightnessOnPowerModeChange)
        {
            return;
        }

        int? current = brightnessService.GetCurrentBrightness();
        int target = IsAboveOffThreshold(lastStableBrightness)
            ? lastStableBrightness
            : current ?? settings.DefaultRestoreBrightness;
        int? restoreTarget = null;

        if (!IsAboveOffThreshold(target))
        {
            log.Info("Brightness guard was not armed because no usable brightness is known");
            return;
        }

        lock (stateLock)
        {
            if (state is AppState.DisplayOffByApp or AppState.RestoringBrightness)
            {
                return;
            }

            brightnessGuardTarget = Math.Clamp(target, 0, 100);
            brightnessGuardArmedUntil = DateTimeOffset.Now.AddMilliseconds(settings.BrightnessGuardWindowMs);

            if (current.HasValue && TryHandlePowerModeBrightnessChangeLocked(current.Value, out int guardTarget))
            {
                restoreTarget = guardTarget;
            }
        }

        string currentText = current.HasValue ? $"{current.Value}%" : "unknown";
        log.Info($"Brightness guard armed after power mode change. Target={target}%, current={currentText}");

        if (restoreTarget.HasValue)
        {
            QueueToUiThread(async () => await RestoreBrightnessForPowerModeGuardAsync(restoreTarget.Value));
        }
    }

    private void OnBalloonUrlRequested(object? sender, string url)
    {
        OpenUrl(url);
    }

    private void RestartUpdateChecks()
    {
        updateCheckCts?.Cancel();
        updateCheckCts?.Dispose();
        updateCheckCts = null;

        if (!settings.CheckForUpdates)
        {
            return;
        }

        updateCheckCts = new CancellationTokenSource();
        CancellationToken token = updateCheckCts.Token;
        _ = Task.Run(async () => await UpdateCheckLoopAsync(token), token);
    }

    private async Task UpdateCheckLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(GetDelayUntilNextUpdateCheck(), token);

                if (ShouldCheckForUpdates())
                {
                    await CheckForUpdatesAsync(manual: false, token);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            log.Error("Update check loop failed", ex);
        }
    }

    private bool ShouldCheckForUpdates()
    {
        if (!settings.CheckForUpdates)
        {
            return false;
        }

        if (!settings.LastUpdateCheckUtc.HasValue)
        {
            return true;
        }

        int intervalHours = Math.Clamp(settings.UpdateCheckIntervalHours, 1, 168);
        return DateTimeOffset.UtcNow - settings.LastUpdateCheckUtc.Value >= TimeSpan.FromHours(intervalHours);
    }

    private TimeSpan GetDelayUntilNextUpdateCheck()
    {
        if (!settings.LastUpdateCheckUtc.HasValue)
        {
            return TimeSpan.FromSeconds(10);
        }

        int intervalHours = Math.Clamp(settings.UpdateCheckIntervalHours, 1, 168);
        DateTimeOffset nextCheck = settings.LastUpdateCheckUtc.Value.AddHours(intervalHours);
        TimeSpan delay = nextCheck - DateTimeOffset.UtcNow;
        return delay <= TimeSpan.Zero ? TimeSpan.FromSeconds(10) : delay;
    }

    private async Task CheckForUpdatesAsync(bool manual, CancellationToken cancellationToken = default)
    {
        if (!manual && !settings.CheckForUpdates)
        {
            return;
        }

        UpdateCheckResult result = await updateCheckService.CheckAsync(cancellationToken);
        settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;

        if (result.Status == UpdateCheckStatus.UpdateAvailable && result.Update is not null)
        {
            bool alreadyNotified = string.Equals(
                settings.LastNotifiedUpdateVersion,
                result.Update.TagName,
                StringComparison.OrdinalIgnoreCase);

            if (manual || !alreadyNotified)
            {
                QueueToUiThread(() => trayIconManager.ShowUpdateAvailable(result.Update));
                settings.LastNotifiedUpdateVersion = result.Update.TagName;
                log.Info($"Update available: {result.Update.TagName} ({result.Update.ReleaseUrl})");
            }
        }
        else if (manual && result.Status == UpdateCheckStatus.NoUpdate)
        {
            QueueToUiThread(() => trayIconManager.ShowInformation(
                "DimToOff is up to date",
                $"Current version: {UpdateCheckService.GetCurrentVersionText()}"));
            log.Info($"Update check completed: current version {UpdateCheckService.GetCurrentVersionText()} is up to date");
        }
        else if (manual && result.Status == UpdateCheckStatus.Failed)
        {
            QueueToUiThread(() => trayIconManager.ShowError(
                "DimToOff update check failed",
                result.ErrorMessage ?? "Could not check GitHub Releases.",
                force: true));
        }

        settingsService.Save(settings);
        trayIconManager.RefreshSettings();
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private bool TryHandlePowerModeBrightnessChangeLocked(int brightness, out int target)
    {
        target = 0;

        if (!settings.PreserveBrightnessOnPowerModeChange ||
            DateTimeOffset.Now > brightnessGuardArmedUntil ||
            DateTimeOffset.Now < suppressBrightnessGuardUntil ||
            state is AppState.DisplayOffByApp or AppState.RestoringBrightness)
        {
            return false;
        }

        int candidate = brightnessGuardTarget;
        if (!IsAboveOffThreshold(candidate))
        {
            return false;
        }

        int tolerance = Math.Clamp(settings.BrightnessGuardTolerancePercent, 1, 20);
        if (Math.Abs(brightness - candidate) < tolerance)
        {
            return false;
        }

        target = candidate;
        state = AppState.Cooldown;
        brightnessGuardArmedUntil = DateTimeOffset.MinValue;
        suppressBrightnessGuardUntil = DateTimeOffset.Now.AddSeconds(5);
        suppressAutoOffUntil = DateTimeOffset.Now.AddSeconds(5);
        CancelPendingDisplayOff();
        CancelPendingBrightnessSave();
        log.Info($"Brightness guard restoring {brightness}% back to {target}% after power mode change");
        return true;
    }

    private async Task RestoreBrightnessForPowerModeGuardAsync(int target)
    {
        CancelPendingBrightnessGuardRestore();
        var restoreCts = new CancellationTokenSource();
        brightnessGuardRestoreCts = restoreCts;
        CancellationToken token = restoreCts.Token;

        try
        {
            await Task.Delay(350, token);
            await Task.Run(() => brightnessService.SetBrightness(target), token);
            lastStableBrightness = target;
            if (IsComfortableBrightness(target))
            {
                lastUsableBrightness = target;
            }

            await Task.Delay(settings.CooldownMs, token);

            lock (stateLock)
            {
                state = AppState.Idle;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            log.Error("Failed to restore brightness after power mode change", ex);

            lock (stateLock)
            {
                state = AppState.Idle;
            }
        }
        finally
        {
            if (ReferenceEquals(brightnessGuardRestoreCts, restoreCts))
            {
                brightnessGuardRestoreCts = null;
                restoreCts.Dispose();
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

    private bool IsAboveOffThreshold(int brightness) =>
        brightness > settings.OffThreshold;

    private bool UseBlackoutMode() =>
        string.Equals(settings.DisplayOffMode, "Blackout", StringComparison.OrdinalIgnoreCase);

    private void CancelPendingDisplayOff()
    {
        debounceCts?.Cancel();
        debounceCts?.Dispose();
        debounceCts = null;
    }

    private void ScheduleStableBrightnessCommit(int brightness)
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
                if (!current.HasValue || current.Value != brightness || !IsAboveOffThreshold(current.Value))
                {
                    return;
                }

                lock (stateLock)
                {
                    if (state == AppState.Idle)
                    {
                        lastStableBrightness = brightness;
                        if (IsComfortableBrightness(brightness))
                        {
                            lastUsableBrightness = brightness;
                            log.Info($"Last usable brightness committed after stable delay: {lastUsableBrightness}%");
                        }
                        else
                        {
                            log.Info($"Last stable brightness committed for power-mode guard: {lastStableBrightness}%");
                        }
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

    private void CancelPendingBrightnessGuardRestore()
    {
        brightnessGuardRestoreCts?.Cancel();
        brightnessGuardRestoreCts?.Dispose();
        brightnessGuardRestoreCts = null;
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
            CancelPendingBrightnessGuardRestore();
            updateCheckCts?.Cancel();
            updateCheckCts?.Dispose();
            updateCheckCts = null;
            CloseWinUiSurfaces();
            brightnessService.BrightnessChanged -= OnBrightnessChanged;
            inputHookService.UserInputDetected -= OnUserInputDetected;
            blackoutService.UserInputDetected -= OnUserInputDetected;
            trayIconManager.BalloonUrlRequested -= OnBalloonUrlRequested;
            uiCommandService.CommandReceived -= OnUiCommandReceived;
            if (messageWindow is HiddenMessageWindow hiddenMessageWindow)
            {
                hiddenMessageWindow.PowerModeMayHaveChanged -= OnPowerModeMayHaveChanged;
            }
            uiCommandService.Dispose();
            updateCheckService.Dispose();
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
        private static readonly Guid[] PowerSettingGuids =
        [
            new("5D3E9A59-E9D5-4B00-A6BD-FF34FF516548"), // AC/DC power source
            new("245D8541-3943-4422-B025-13A784F679B7"), // active power scheme personality
            new("E00958C0-C213-4ACE-AC77-FECCED2EEEA5")  // battery saver / power saving status
        ];

        private readonly LogService log;
        private readonly List<nint> powerNotificationHandles = [];
        private PowrProf.EffectivePowerModeCallback? effectivePowerModeCallback;
        private nint effectivePowerModeNotificationHandle;
        private int? lastEffectivePowerMode;

        public event EventHandler? PowerModeMayHaveChanged;

        public HiddenMessageWindow(LogService log)
        {
            this.log = log;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(1, 1);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterPowerNotifications();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterPowerNotifications();
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeConstants.WM_POWERBROADCAST &&
                (m.WParam.ToInt32() == NativeConstants.PBT_POWERSETTINGCHANGE ||
                 m.WParam.ToInt32() == NativeConstants.PBT_APMPOWERSTATUSCHANGE ||
                 m.WParam.ToInt32() == NativeConstants.PBT_APMRESUMEAUTOMATIC))
            {
                RaisePowerModeMayHaveChanged();
            }

            base.WndProc(ref m);
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        private void RegisterPowerNotifications()
        {
            foreach (Guid powerSettingGuid in PowerSettingGuids)
            {
                Guid guid = powerSettingGuid;
                nint handle = User32.RegisterPowerSettingNotification(
                    Handle,
                    ref guid,
                    NativeConstants.DEVICE_NOTIFY_WINDOW_HANDLE);

                if (handle == 0)
                {
                    log.Error($"Failed to register power setting notification: {powerSettingGuid}");
                    continue;
                }

                powerNotificationHandles.Add(handle);
            }

            RegisterEffectivePowerModeNotification();
        }

        private void UnregisterPowerNotifications()
        {
            UnregisterEffectivePowerModeNotification();

            foreach (nint handle in powerNotificationHandles)
            {
                User32.UnregisterPowerSettingNotification(handle);
            }

            powerNotificationHandles.Clear();
        }

        private void RegisterEffectivePowerModeNotification()
        {
            effectivePowerModeCallback = OnEffectivePowerModeChanged;

            try
            {
                int result = PowrProf.PowerRegisterForEffectivePowerModeNotifications(
                    NativeConstants.EFFECTIVE_POWER_MODE_V2,
                    effectivePowerModeCallback,
                    0,
                    out effectivePowerModeNotificationHandle);

                if (result != 0)
                {
                    effectivePowerModeNotificationHandle = 0;
                    effectivePowerModeCallback = null;
                    log.Info($"Effective power mode notifications unavailable. Win32 error={result}");
                }
            }
            catch (EntryPointNotFoundException)
            {
                effectivePowerModeCallback = null;
                log.Info("Effective power mode notifications are unavailable on this Windows version");
            }
            catch (DllNotFoundException ex)
            {
                effectivePowerModeCallback = null;
                log.Error("Effective power mode notifications could not be registered", ex);
            }
        }

        private void UnregisterEffectivePowerModeNotification()
        {
            if (effectivePowerModeNotificationHandle != 0)
            {
                PowrProf.PowerUnregisterFromEffectivePowerModeNotifications(effectivePowerModeNotificationHandle);
                effectivePowerModeNotificationHandle = 0;
            }

            effectivePowerModeCallback = null;
            lastEffectivePowerMode = null;
        }

        private void OnEffectivePowerModeChanged(int mode, nint context)
        {
            int? previous = lastEffectivePowerMode;
            lastEffectivePowerMode = mode;

            if (!previous.HasValue || previous.Value == mode)
            {
                return;
            }

            RaisePowerModeMayHaveChanged();
        }

        private void RaisePowerModeMayHaveChanged()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke((MethodInvoker)(() => PowerModeMayHaveChanged?.Invoke(this, EventArgs.Empty)));
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}

using System.Diagnostics;
using DimToOff.Settings.Models;
using DimToOff.Settings.Services;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT.Interop;

namespace DimToOff.Settings;

public sealed partial class MainWindow : Window
{
    private const int WindowWidthDips = 940;
    private const int WindowHeightDips = 820;
    private const int WindowMinimumWidthDips = 880;
    private const int WindowMinimumHeightDips = 620;
    private const int SaveDebounceMs = 400;
    private const int SaveStatusHoldMs = 2200;

    /// <summary>Idle timeouts offered in the dropdowns. Zero means never.</summary>
    private static readonly int[] TimeoutChoices =
        [0, 60, 120, 180, 300, 600, 900, 1200, 1800, 2700, 3600, 7200];

    private readonly SettingsStore settingsStore = new();
    private readonly TrayCommandClient commandClient;
    private readonly string mainExecutablePath;
    private readonly DispatcherQueueTimer saveTimer;
    private readonly DispatcherQueueTimer saveStatusTimer;
    private IDisposable? minimumSizeHook;
    private Storyboard? saveStatusStoryboard;
    private AppSettings settings;
    private bool isLoading = true;
    private bool hasPendingSave;

    public MainWindow(LaunchOptions options)
    {
        InitializeComponent();

        Title = "DimToOff Settings";
        commandClient = new TrayCommandClient(options.PipeName);
        mainExecutablePath = options.MainExecutablePath;
        settings = settingsStore.Load();
        settings.StartWithWindows = StartupRegistration.IsEnabled();

        saveTimer = DispatcherQueue.CreateTimer();
        saveTimer.Interval = TimeSpan.FromMilliseconds(SaveDebounceMs);
        saveTimer.IsRepeating = false;
        saveTimer.Tick += (_, _) => SaveNow();

        saveStatusTimer = DispatcherQueue.CreateTimer();
        saveStatusTimer.Interval = TimeSpan.FromMilliseconds(SaveStatusHoldMs);
        saveStatusTimer.IsRepeating = false;
        saveStatusTimer.Tick += (_, _) => FadeSaveStatus(show: false);

        ConfigureWindow();
        PopulateTimeoutChoices();
        ApplySettingsToControls();
    }

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        try
        {
            SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
        }

        IntPtr windowHandle = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Title = "DimToOff Settings";
        NativeWindow.ResizeForDips(windowHandle, appWindow, WindowWidthDips, WindowHeightDips);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
        }

        minimumSizeHook = NativeWindow.EnforceMinimumSize(
            windowHandle,
            NativeWindow.ToPhysicalPixels(windowHandle, WindowMinimumWidthDips),
            NativeWindow.ToPhysicalPixels(windowHandle, WindowMinimumHeightDips));
        Closed += OnClosed;

        DispatcherQueue.TryEnqueue(() => NativeWindow.BringToFront(windowHandle));
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        // Anything typed a moment ago must not be lost by closing the window.
        if (hasPendingSave)
        {
            saveTimer.Stop();
            SaveNow();
        }

        saveTimer.Stop();
        saveStatusTimer.Stop();
        saveStatusStoryboard?.Stop();
        saveStatusStoryboard = null;
        minimumSizeHook?.Dispose();
        minimumSizeHook = null;
    }

    private void PopulateTimeoutChoices()
    {
        foreach (int seconds in TimeoutChoices)
        {
            PluggedInTimeoutCombo.Items.Add(CreateTimeoutItem(seconds));
            OnBatteryTimeoutCombo.Items.Add(CreateTimeoutItem(seconds));
        }
    }

    private static ComboBoxItem CreateTimeoutItem(int seconds) =>
        new()
        {
            Content = seconds <= 0 ? "Never" : FormatDuration(seconds),
            Tag = seconds
        };

    private static string FormatDuration(int seconds)
    {
        if (seconds < 60)
        {
            return $"{seconds} seconds";
        }

        int minutes = seconds / 60;
        if (minutes < 60)
        {
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }

        int hours = minutes / 60;
        int remainingMinutes = minutes % 60;
        string hourText = hours == 1 ? "1 hour" : $"{hours} hours";
        return remainingMinutes == 0 ? hourText : $"{hourText} {remainingMinutes} min";
    }

    private void ApplySettingsToControls()
    {
        isLoading = true;

        EnabledToggle.IsOn = settings.Enabled;
        StartWithWindowsToggle.IsOn = settings.StartWithWindows;
        ErrorNotificationsToggle.IsOn = settings.ShowErrorNotifications;
        UpdateNotificationsToggle.IsOn = settings.CheckForUpdates;
        PreserveBrightnessToggle.IsOn = settings.PreserveBrightnessOnPowerModeChange;
        BrightnessTriggerToggle.IsOn = settings.BrightnessBlackoutEnabled;
        SetComboValue(DisplayModeCombo, settings.DisplayOffMode);

        IdleBlackoutToggle.IsOn = settings.IdleBlackoutEnabled;
        SetTimeoutValue(PluggedInTimeoutCombo, settings.IdleTimeoutPluggedInSeconds);
        SetTimeoutValue(OnBatteryTimeoutCombo, settings.IdleTimeoutOnBatterySeconds);
        SkipFullscreenToggle.IsOn = settings.IdleSkipWhileFullscreenApp;
        RespectAppRequestsToggle.IsOn = settings.IdleRespectAppDisplayRequests;
        SkipExternalMonitorToggle.IsOn = settings.IdleSkipWhenExternalMonitorConnected;

        OffThresholdBox.Value = settings.OffThreshold;
        FadeToBlackBox.Value = settings.FadeToBlackMs;
        IgnoreInputBox.Value = settings.IgnoreInputMs;
        DebounceBox.Value = settings.DebounceMs;
        CooldownBox.Value = settings.CooldownMs;
        BrightnessStableBox.Value = settings.BrightnessSaveStableMs;
        MinimumRestoreBox.Value = settings.MinimumRestoreBrightness;
        DefaultRestoreBox.Value = settings.DefaultRestoreBrightness;

        SettingsPathText.Text = $"Stored in {settingsStore.SettingsFilePath}";

        isLoading = false;
        UpdateDependentState();
    }

    private AppSettings ReadSettingsFromControls()
    {
        int minimumRestore = NumberValue(MinimumRestoreBox, settings.MinimumRestoreBrightness);
        int defaultRestore = Math.Max(NumberValue(DefaultRestoreBox, settings.DefaultRestoreBrightness), minimumRestore);

        return new AppSettings
        {
            Enabled = EnabledToggle.IsOn,
            StartWithWindows = StartWithWindowsToggle.IsOn,
            ShowErrorNotifications = ErrorNotificationsToggle.IsOn,
            CheckForUpdates = UpdateNotificationsToggle.IsOn,
            UpdateCheckIntervalHours = settings.UpdateCheckIntervalHours,
            LastUpdateCheckUtc = settings.LastUpdateCheckUtc,
            LastNotifiedUpdateVersion = settings.LastNotifiedUpdateVersion,
            PreserveBrightnessOnPowerModeChange = PreserveBrightnessToggle.IsOn,
            BrightnessGuardWindowMs = settings.BrightnessGuardWindowMs,
            BrightnessGuardTolerancePercent = settings.BrightnessGuardTolerancePercent,
            BrightnessBlackoutEnabled = BrightnessTriggerToggle.IsOn,
            DisplayOffMode = GetComboValue(DisplayModeCombo, settings.DisplayOffMode),
            OffThreshold = NumberValue(OffThresholdBox, settings.OffThreshold),
            FadeToBlackMs = NumberValue(FadeToBlackBox, settings.FadeToBlackMs),
            IgnoreInputMs = NumberValue(IgnoreInputBox, settings.IgnoreInputMs),
            DebounceMs = NumberValue(DebounceBox, settings.DebounceMs),
            CooldownMs = NumberValue(CooldownBox, settings.CooldownMs),
            BrightnessSaveStableMs = NumberValue(BrightnessStableBox, settings.BrightnessSaveStableMs),
            RestoreMode = settings.RestoreMode,
            MinimumRestoreBrightness = minimumRestore,
            DefaultRestoreBrightness = defaultRestore,
            IdleBlackoutEnabled = IdleBlackoutToggle.IsOn,
            IdleTimeoutPluggedInSeconds = GetTimeoutValue(PluggedInTimeoutCombo, settings.IdleTimeoutPluggedInSeconds),
            IdleTimeoutOnBatterySeconds = GetTimeoutValue(OnBatteryTimeoutCombo, settings.IdleTimeoutOnBatterySeconds),
            IdleRespectAppDisplayRequests = RespectAppRequestsToggle.IsOn,
            IdleSkipWhileFullscreenApp = SkipFullscreenToggle.IsOn,
            IdleSkipWhenExternalMonitorConnected = SkipExternalMonitorToggle.IsOn
        };
    }

    private void Setting_Changed(object sender, RoutedEventArgs e) => OnSettingEdited();

    private void Choice_Changed(object sender, SelectionChangedEventArgs e) => OnSettingEdited();

    private void Number_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => OnSettingEdited();

    private void OnSettingEdited()
    {
        if (isLoading)
        {
            return;
        }

        UpdateDependentState();
        hasPendingSave = true;
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void SaveNow()
    {
        try
        {
            AppSettings updated = ReadSettingsFromControls();
            settingsStore.Save(updated);
            StartupRegistration.SetEnabled(updated.StartWithWindows, mainExecutablePath);
            settings = updated;
            hasPendingSave = false;
            _ = commandClient.SendAsync("reload-settings");
            ShowSaveStatus("Saved", success: true);
        }
        catch (Exception ex)
        {
            hasPendingSave = false;
            ShowSaveStatus($"Could not save: {ex.Message}", success: false);
        }
    }

    /// <summary>Keeps rows that depend on another switch from looking editable when they are not.</summary>
    private void UpdateDependentState()
    {
        bool blackout = string.Equals(
            GetComboValue(DisplayModeCombo, "Blackout"),
            "Blackout",
            StringComparison.OrdinalIgnoreCase);

        OffThresholdRow.IsEnabled = BrightnessTriggerToggle.IsOn;
        FadeRow.IsEnabled = blackout;
        MonitorPowerInfo.IsOpen = !blackout;

        bool idleEnabled = IdleBlackoutToggle.IsOn;
        PluggedInRow.IsEnabled = idleEnabled;
        OnBatteryRow.IsEnabled = idleEnabled;
        FullscreenRow.IsEnabled = idleEnabled;
        AppRequestRow.IsEnabled = idleEnabled;
        ExternalMonitorRow.IsEnabled = idleEnabled;

        UpdateStatusCard();
    }

    private void UpdateStatusCard()
    {
        bool enabled = EnabledToggle.IsOn;
        StatusTitle.Text = enabled ? "DimToOff is on" : "DimToOff is paused";
        StatusIcon.Glyph = enabled ? "" : "";
        StatusBadge.Opacity = enabled ? 1 : 0.4;
        StatusSummary.Text = BuildStatusSummary(enabled);
    }

    private string BuildStatusSummary(bool enabled)
    {
        if (!enabled)
        {
            return "Windows keeps its own screen timeout and sleep settings.";
        }

        var parts = new List<string>();

        if (IdleBlackoutToggle.IsOn)
        {
            int pluggedIn = GetTimeoutValue(PluggedInTimeoutCombo, settings.IdleTimeoutPluggedInSeconds);
            int onBattery = GetTimeoutValue(OnBatteryTimeoutCombo, settings.IdleTimeoutOnBatterySeconds);

            if (pluggedIn > 0 || onBattery > 0)
            {
                string pluggedInText = pluggedIn > 0 ? FormatDuration(pluggedIn) : "never";
                string onBatteryText = onBattery > 0 ? FormatDuration(onBattery) : "never";
                parts.Add($"Screen off after {pluggedInText} plugged in, {onBatteryText} on battery.");
                parts.Add("Windows will not sleep or blank the screen on its own.");
            }
        }

        if (BrightnessTriggerToggle.IsOn)
        {
            int threshold = NumberValue(OffThresholdBox, settings.OffThreshold);
            parts.Add($"Brightness at or below {threshold}% also blanks the screen.");
        }

        return parts.Count == 0
            ? "No blanking trigger is turned on yet."
            : string.Join(" ", parts);
    }

    private void ShowSaveStatus(string message, bool success)
    {
        SaveStatusText.Text = message;
        SaveStatusIcon.Glyph = success ? "" : "";
        SaveStatusIcon.Foreground = (Brush)Application.Current.Resources[
            success ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush"];

        FadeSaveStatus(show: true);
        saveStatusTimer.Stop();
        saveStatusTimer.Start();
    }

    private void FadeSaveStatus(bool show)
    {
        saveStatusStoryboard?.Stop();

        var animation = new DoubleAnimation
        {
            From = SaveStatusHost.Opacity,
            To = show ? 1 : 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(show ? 160 : 260)),
            EasingFunction = new CubicEase { EasingMode = show ? EasingMode.EaseOut : EasingMode.EaseIn }
        };
        Storyboard.SetTarget(animation, SaveStatusHost);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        saveStatusStoryboard = storyboard;
        storyboard.Begin();
    }

    private void SectionNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        string section = item.Tag?.ToString() ?? "General";
        GeneralPanel.Visibility = section == "General" ? Visibility.Visible : Visibility.Collapsed;
        ScreenOffPanel.Visibility = section == "ScreenOff" ? Visibility.Visible : Visibility.Collapsed;
        AwayPanel.Visibility = section == "Away" ? Visibility.Visible : Visibility.Collapsed;
        AdvancedPanel.Visibility = section == "Advanced" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new AppSettings
        {
            StartWithWindows = settings.StartWithWindows,
            UpdateCheckIntervalHours = settings.UpdateCheckIntervalHours,
            LastUpdateCheckUtc = settings.LastUpdateCheckUtc,
            LastNotifiedUpdateVersion = settings.LastNotifiedUpdateVersion
        };

        settings = defaults;
        ApplySettingsToControls();
        SaveNow();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void GitHubButton_Click(object sender, RoutedEventArgs e)
    {
        OpenPath("https://github.com/lingmulongtai/DimToOff-windows");
    }

    private void LogsButton_Click(object sender, RoutedEventArgs e)
    {
        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DimToOff",
            "logs");
        Directory.CreateDirectory(logDirectory);
        OpenPath(logDirectory);
    }

    private static void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            saveTimer.Stop();
            SaveNow();

            await Task.Delay(100);
            await commandClient.SendAsync("check-updates");
            ShowSaveStatus("Checking for updates", success: true);
        }
        catch (Exception ex)
        {
            ShowSaveStatus($"Could not check updates: {ex.Message}", success: false);
        }
    }

    private static int NumberValue(NumberBox numberBox, int fallback)
    {
        double value = numberBox.Value;
        return double.IsNaN(value) ? fallback : (int)Math.Round(value);
    }

    private static void SetComboValue(ComboBox comboBox, string value)
    {
        foreach (object item in comboBox.Items)
        {
            if (item is ComboBoxItem comboBoxItem &&
                string.Equals(comboBoxItem.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = comboBoxItem;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static string GetComboValue(ComboBox comboBox, string fallback)
    {
        if (comboBox.SelectedItem is ComboBoxItem comboBoxItem && comboBoxItem.Tag is not null)
        {
            return comboBoxItem.Tag.ToString() ?? fallback;
        }

        return fallback;
    }

    /// <summary>Selects a timeout, adding an entry first when the settings file holds a custom value.</summary>
    private static void SetTimeoutValue(ComboBox comboBox, int seconds)
    {
        foreach (object item in comboBox.Items)
        {
            if (item is ComboBoxItem comboBoxItem && comboBoxItem.Tag is int value && value == seconds)
            {
                comboBox.SelectedItem = comboBoxItem;
                return;
            }
        }

        ComboBoxItem custom = CreateTimeoutItem(seconds);
        comboBox.Items.Insert(0, custom);
        comboBox.SelectedItem = custom;
    }

    private static int GetTimeoutValue(ComboBox comboBox, int fallback)
    {
        if (comboBox.SelectedItem is ComboBoxItem comboBoxItem && comboBoxItem.Tag is int value)
        {
            return value;
        }

        return fallback;
    }
}

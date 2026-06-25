using System.Diagnostics;
using DimToOff.Settings.Models;
using DimToOff.Settings.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT.Interop;

namespace DimToOff.Settings;

public sealed partial class MainWindow : Window
{
    private const int SettingsWindowHeightDips = 820;
    private const int SettingsWindowMinimumWidthDips = 744;
    private const int SettingsWindowMinimumHeightDips = 620;

    private readonly SettingsStore settingsStore = new();
    private readonly TrayCommandClient commandClient;
    private readonly string mainExecutablePath;
    private IDisposable? minimumSizeHook;
    private CancellationTokenSource? saveInfoDismissCts;
    private Storyboard? saveInfoStoryboard;
    private int saveInfoAnimationVersion;
    private AppSettings settings;

    public MainWindow(LaunchOptions options)
    {
        InitializeComponent();

        Title = "DimToOff Settings";
        commandClient = new TrayCommandClient(options.PipeName);
        mainExecutablePath = options.MainExecutablePath;
        settings = settingsStore.Load();
        settings.StartWithWindows = StartupRegistration.IsEnabled();

        ConfigureWindow();
        ApplySettingsToControls();
        UpdateModeState();
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
        NativeWindow.ResizeForDips(windowHandle, appWindow, SettingsWindowMinimumWidthDips, SettingsWindowHeightDips);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
        }

        minimumSizeHook = NativeWindow.EnforceMinimumSize(
            windowHandle,
            NativeWindow.ToPhysicalPixels(windowHandle, SettingsWindowMinimumWidthDips),
            NativeWindow.ToPhysicalPixels(windowHandle, SettingsWindowMinimumHeightDips));
        Closed += OnClosed;

        DispatcherQueue.TryEnqueue(() => NativeWindow.BringToFront(windowHandle));
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        saveInfoDismissCts?.Cancel();
        saveInfoDismissCts?.Dispose();
        saveInfoDismissCts = null;
        saveInfoStoryboard?.Stop();
        saveInfoStoryboard = null;
        minimumSizeHook?.Dispose();
        minimumSizeHook = null;
    }

    private void ApplySettingsToControls()
    {
        EnabledToggle.IsOn = settings.Enabled;
        StartWithWindowsToggle.IsOn = settings.StartWithWindows;
        ErrorNotificationsToggle.IsOn = settings.ShowErrorNotifications;
        SetComboValue(DisplayModeCombo, settings.DisplayOffMode);

        OffThresholdBox.Value = settings.OffThreshold;
        FadeToBlackBox.Value = settings.FadeToBlackMs;
        IgnoreInputBox.Value = settings.IgnoreInputMs;
        DebounceBox.Value = settings.DebounceMs;
        CooldownBox.Value = settings.CooldownMs;
        BrightnessStableBox.Value = settings.BrightnessSaveStableMs;
        MinimumRestoreBox.Value = settings.MinimumRestoreBrightness;
        DefaultRestoreBox.Value = settings.DefaultRestoreBrightness;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AppSettings updated = ReadSettingsFromControls();
            settingsStore.Save(updated);
            StartupRegistration.SetEnabled(updated.StartWithWindows, mainExecutablePath);
            _ = commandClient.SendAsync("reload-settings");
            settings = updated;

            ShowSaveStatus("Saved", string.Empty, InfoBarSeverity.Success, autoDismiss: true);
        }
        catch (Exception ex)
        {
            ShowSaveStatus("Could not save settings", ex.Message, InfoBarSeverity.Error, autoDismiss: false);
        }
    }

    private void ShowSaveStatus(string title, string message, InfoBarSeverity severity, bool autoDismiss)
    {
        saveInfoDismissCts?.Cancel();
        saveInfoDismissCts?.Dispose();
        saveInfoDismissCts = null;

        SaveInfoHost.Width = severity == InfoBarSeverity.Success ? 180 : 380;
        SaveInfoBar.Title = title;
        SaveInfoBar.Message = message;
        SaveInfoBar.Severity = severity;
        SaveInfoBar.IsOpen = true;
        ShowSaveStatusAnimated();

        if (!autoDismiss)
        {
            return;
        }

        var dismissCts = new CancellationTokenSource();
        saveInfoDismissCts = dismissCts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2400, dismissCts.Token);
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (!dismissCts.IsCancellationRequested &&
                        ReferenceEquals(saveInfoDismissCts, dismissCts))
                    {
                        saveInfoDismissCts = null;
                        dismissCts.Dispose();
                        HideSaveStatusAnimated();
                    }
                });
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    private void ShowSaveStatusAnimated()
    {
        int version = ++saveInfoAnimationVersion;
        saveInfoStoryboard?.Stop();

        SaveInfoHost.Visibility = Visibility.Visible;
        SaveInfoHost.Opacity = 0;
        SaveInfoTransform.TranslateX = 18;
        SaveInfoTransform.TranslateY = -6;

        saveInfoStoryboard = CreateSaveInfoStoryboard(
            opacityFrom: 0,
            opacityTo: 1,
            xFrom: 18,
            xTo: 0,
            yFrom: -6,
            yTo: 0,
            durationMs: 180,
            EasingMode.EaseOut);
        saveInfoStoryboard.Completed += (_, _) =>
        {
            if (version == saveInfoAnimationVersion)
            {
                SaveInfoHost.Opacity = 1;
                SaveInfoTransform.TranslateX = 0;
                SaveInfoTransform.TranslateY = 0;
            }
        };
        saveInfoStoryboard.Begin();
    }

    private void HideSaveStatusAnimated()
    {
        int version = ++saveInfoAnimationVersion;
        saveInfoStoryboard?.Stop();

        saveInfoStoryboard = CreateSaveInfoStoryboard(
            opacityFrom: SaveInfoHost.Opacity,
            opacityTo: 0,
            xFrom: SaveInfoTransform.TranslateX,
            xTo: 18,
            yFrom: SaveInfoTransform.TranslateY,
            yTo: -6,
            durationMs: 140,
            EasingMode.EaseIn);
        saveInfoStoryboard.Completed += (_, _) =>
        {
            if (version == saveInfoAnimationVersion)
            {
                SaveInfoBar.IsOpen = false;
                SaveInfoHost.Visibility = Visibility.Collapsed;
                SaveInfoHost.Opacity = 0;
                SaveInfoTransform.TranslateX = 18;
                SaveInfoTransform.TranslateY = -6;
            }
        };
        saveInfoStoryboard.Begin();
    }

    private Storyboard CreateSaveInfoStoryboard(
        double opacityFrom,
        double opacityTo,
        double xFrom,
        double xTo,
        double yFrom,
        double yTo,
        int durationMs,
        EasingMode easingMode)
    {
        var storyboard = new Storyboard();
        var easing = new CubicEase { EasingMode = easingMode };
        AddDoubleAnimation(storyboard, SaveInfoHost, "Opacity", opacityFrom, opacityTo, durationMs, easing, dependent: false);
        AddDoubleAnimation(storyboard, SaveInfoTransform, "TranslateX", xFrom, xTo, durationMs, easing, dependent: true);
        AddDoubleAnimation(storyboard, SaveInfoTransform, "TranslateY", yFrom, yTo, durationMs, easing, dependent: true);
        return storyboard;
    }

    private static void AddDoubleAnimation(
        Storyboard storyboard,
        DependencyObject target,
        string property,
        double from,
        double to,
        int durationMs,
        EasingFunctionBase easing,
        bool dependent)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EasingFunction = easing,
            EnableDependentAnimation = dependent
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void GitHubButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/lingmulongtai/DimToOff-windows",
            UseShellExecute = true
        });
    }

    private void DisplayModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateModeState();
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
            DisableWhileFullscreen = settings.DisableWhileFullscreen,
            DisableWhenExternalMonitorConnected = settings.DisableWhenExternalMonitorConnected
        };
    }

    private void UpdateModeState()
    {
        bool blackout = string.Equals(GetComboValue(DisplayModeCombo, "Blackout"), "Blackout", StringComparison.OrdinalIgnoreCase);
        FadeToBlackBox.IsEnabled = blackout;
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
        if (comboBox.SelectedItem is ComboBoxItem comboBoxItem &&
            comboBoxItem.Tag is not null)
        {
            return comboBoxItem.Tag.ToString() ?? fallback;
        }

        return fallback;
    }

}

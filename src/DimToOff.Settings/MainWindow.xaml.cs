using System.Diagnostics;
using DimToOff.Settings.Models;
using DimToOff.Settings.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace DimToOff.Settings;

public sealed partial class MainWindow : Window
{
    private const int SettingsWindowHeightDips = 820;
    private const int SettingsWindowMinimumWidthDips = 760;
    private const int SettingsWindowOuterPaddingDips = 56;

    private readonly SettingsStore settingsStore = new();
    private readonly TrayCommandClient commandClient;
    private readonly string mainExecutablePath;
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
        UpdateStatus();
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
        int windowWidthDips = Math.Max(
            SettingsWindowMinimumWidthDips,
            (int)Math.Ceiling(ContentColumn.Width + SettingsWindowOuterPaddingDips));
        NativeWindow.ResizeForDips(windowHandle, appWindow, windowWidthDips, SettingsWindowHeightDips);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
        }

        DispatcherQueue.TryEnqueue(() => NativeWindow.BringToFront(windowHandle));
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

            SaveInfoBar.Title = "Saved";
            SaveInfoBar.Message = "DimToOff will use these settings immediately after the tray app reloads them.";
            SaveInfoBar.Severity = InfoBarSeverity.Success;
            SaveInfoBar.IsOpen = true;
            UpdateStatus();
        }
        catch (Exception ex)
        {
            SaveInfoBar.Title = "Could not save settings";
            SaveInfoBar.Message = ex.Message;
            SaveInfoBar.Severity = InfoBarSeverity.Error;
            SaveInfoBar.IsOpen = true;
        }
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

    private void EnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        UpdateStatus();
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

    private void UpdateStatus()
    {
        StatusText.Text = EnabledToggle.IsOn ? "Enabled" : "Paused";
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

using DimToOff.Settings.Models;
using DimToOff.Settings.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace DimToOff.Settings;

public sealed partial class TrayMenuWindow : Window
{
    private const int MenuWidth = 300;
    private const int MenuHeight = 476;

    /// <summary>
    /// Idle timeouts the quick menu offers. A shorter list than the settings window, because
    /// the point here is one click from the taskbar rather than every value there is.
    /// </summary>
    private static readonly int[] TimeoutChoices =
        [0, 60, 120, 300, 600, 900, 1800, 3600];

    private readonly TrayCommandClient commandClient;
    private readonly SettingsStore settingsStore = new();
    private readonly string mainExecutablePath;
    private OutsideClickDismissService? outsideClickDismissService;
    private AppSettings settings = new();
    private bool activatedOnce;

    public TrayMenuWindow(LaunchOptions options)
    {
        InitializeComponent();

        commandClient = new TrayCommandClient(options.PipeName);
        mainExecutablePath = options.MainExecutablePath;
        ConfigureWindow();
        BuildTimeoutChoices();
        ApplyCurrentState();
    }

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(MenuRoot);

        IntPtr windowHandle = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Title = "DimToOff";
        NativeWindow.MakeBorderlessToolWindow(windowHandle);
        NativeWindow.MoveNearCursorForDips(windowHandle, appWindow, MenuWidth, MenuHeight);
        NativeWindow.ApplyRoundedRegionForDips(windowHandle, MenuWidth, MenuHeight, 8);
        outsideClickDismissService = new OutsideClickDismissService(
            windowHandle,
            () => DispatcherQueue.TryEnqueue(Close));
        outsideClickDismissService.Start();

        Activated += OnActivated;
        Closed += OnClosed;
    }

    private void ApplyCurrentState()
    {
        settings = settingsStore.Load();
        settings.StartWithWindows = StartupRegistration.IsEnabled();
        UpdateCheckMarks();
    }

    private void BuildTimeoutChoices()
    {
        foreach (int seconds in TimeoutChoices)
        {
            TimeoutChoiceList.Children.Add(CreateTimeoutChoice(seconds));
        }
    }

    /// <summary>One row of the timeout page: a check mark when it is the value in use.</summary>
    private Button CreateTimeoutChoice(int seconds)
    {
        var check = new FontIcon
        {
            Glyph = "",
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Visibility = Visibility.Collapsed
        };

        var label = new TextBlock
        {
            Text = seconds <= 0 ? "Never" : FormatDuration(seconds),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 1);

        var layout = new Grid { ColumnSpacing = 10 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.Children.Add(check);
        layout.Children.Add(label);

        var button = new Button
        {
            Style = (Style)MenuRoot.Resources["MenuButtonStyle"],
            Content = layout,
            Tag = seconds
        };
        button.Click += TimeoutChoice_Click;
        return button;
    }

    private void TimeoutButton_Click(object sender, RoutedEventArgs e)
    {
        MainPage.Visibility = Visibility.Collapsed;
        TimeoutPage.Visibility = Visibility.Visible;
    }

    private void TimeoutBackButton_Click(object sender, RoutedEventArgs e)
    {
        TimeoutPage.Visibility = Visibility.Collapsed;
        MainPage.Visibility = Visibility.Visible;
    }

    private async void TimeoutChoice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int seconds })
        {
            return;
        }

        SetCurrentTimeout(seconds);
        settingsStore.Save(settings);
        UpdateCheckMarks();
        TimeoutBackButton_Click(sender, e);
        await commandClient.SendAsync("reload-settings");
    }

    /// <summary>
    /// The quick menu edits the timeout for the power source in use right now. The other one
    /// stays where the settings window left it.
    /// </summary>
    private int GetCurrentTimeout() =>
        PowerSource.IsOnBattery()
            ? settings.IdleTimeoutOnBatterySeconds
            : settings.IdleTimeoutPluggedInSeconds;

    private void SetCurrentTimeout(int seconds)
    {
        if (PowerSource.IsOnBattery())
        {
            settings.IdleTimeoutOnBatterySeconds = seconds;
            return;
        }

        settings.IdleTimeoutPluggedInSeconds = seconds;
    }

    private async void IdleBlackoutButton_Click(object sender, RoutedEventArgs e)
    {
        settings.IdleBlackoutEnabled = !settings.IdleBlackoutEnabled;
        settingsStore.Save(settings);
        UpdateCheckMarks();
        await commandClient.SendAsync("reload-settings");
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated && activatedOnce)
        {
            Close();
            return;
        }

        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            activatedOnce = true;
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        outsideClickDismissService?.Dispose();
        outsideClickDismissService = null;
    }

    private async void EnabledButton_Click(object sender, RoutedEventArgs e)
    {
        settings.Enabled = !settings.Enabled;
        settingsStore.Save(settings);
        UpdateCheckMarks();
        await commandClient.SendAsync($"set-enabled={settings.Enabled}");
    }

    private async void StartWithWindowsButton_Click(object sender, RoutedEventArgs e)
    {
        settings.StartWithWindows = !settings.StartWithWindows;
        StartupRegistration.SetEnabled(settings.StartWithWindows, mainExecutablePath);
        settingsStore.Save(settings);
        UpdateCheckMarks();
        await commandClient.SendAsync($"set-startup={settings.StartWithWindows}");
    }

    private async void BlankButton_Click(object sender, RoutedEventArgs e)
    {
        await commandClient.SendAsync("blank");
        Close();
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        await commandClient.SendAsync("restore");
        Close();
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await commandClient.SendAsync("settings");
        Close();
    }

    private async void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        await commandClient.SendAsync("about");
        Close();
    }

    private async void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        await commandClient.SendAsync("exit");
        Close();
    }

    private void UpdateCheckMarks()
    {
        EnabledCheck.Visibility = settings.Enabled ? Visibility.Visible : Visibility.Collapsed;
        IdleBlackoutCheck.Visibility = settings.IdleBlackoutEnabled ? Visibility.Visible : Visibility.Collapsed;
        StartWithWindowsCheck.Visibility = settings.StartWithWindows ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = BuildStatusText();
        UpdateTimeoutRow();
    }

    private void UpdateTimeoutRow()
    {
        int current = GetCurrentTimeout();
        bool onBattery = PowerSource.IsOnBattery();

        TimeoutButton.IsEnabled = settings.Enabled && settings.IdleBlackoutEnabled;
        TimeoutValueText.Text = current <= 0 ? "Never" : FormatDuration(current);
        TimeoutPageSubtitle.Text = onBattery ? "On battery" : "Plugged in";

        foreach (Button choice in TimeoutChoiceList.Children.OfType<Button>())
        {
            if (choice is { Tag: int seconds, Content: Grid layout } &&
                layout.Children.FirstOrDefault() is FontIcon check)
            {
                check.Visibility = seconds == current ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private string BuildStatusText()
    {
        if (!settings.Enabled)
        {
            return "Paused";
        }

        int timeoutSeconds = PowerSource.IsOnBattery()
            ? settings.IdleTimeoutOnBatterySeconds
            : settings.IdleTimeoutPluggedInSeconds;

        if (!settings.IdleBlackoutEnabled || timeoutSeconds <= 0)
        {
            return settings.BrightnessBlackoutEnabled
                ? "Blanks at minimum brightness"
                : "No blanking trigger is on";
        }

        return $"Screen off after {FormatDuration(timeoutSeconds)} idle";
    }

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
}

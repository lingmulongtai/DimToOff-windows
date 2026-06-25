using System.Diagnostics;
using DimToOff.Settings.Models;
using DimToOff.Settings.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace DimToOff.Settings;

public sealed partial class TrayMenuWindow : Window
{
    private const int MenuWidth = 286;
    private const int MenuHeight = 334;

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

    private void GitHubButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/lingmulongtai/DimToOff-windows",
            UseShellExecute = true
        });
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
        StartWithWindowsCheck.Visibility = settings.StartWithWindows ? Visibility.Visible : Visibility.Collapsed;
    }
}

using System.Diagnostics;
using DimToOff.Settings.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace DimToOff.Settings;

public sealed partial class AboutWindow : Window
{
    private const int WindowWidthDips = 560;
    private const int WindowHeightDips = 700;
    private const int WindowMinimumWidthDips = 560;
    private const int WindowMinimumHeightDips = 620;

    private IDisposable? minimumSizeHook;

    public AboutWindow(LaunchOptions options)
    {
        InitializeComponent();

        Title = "About DimToOff";
        ConfigureWindow();
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
        appWindow.Title = "About DimToOff";
        NativeWindow.ResizeForDips(windowHandle, appWindow, WindowWidthDips, WindowHeightDips);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
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
        minimumSizeHook?.Dispose();
        minimumSizeHook = null;
    }

    private void GitHubButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/lingmulongtai/DimToOff-windows",
            UseShellExecute = true
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

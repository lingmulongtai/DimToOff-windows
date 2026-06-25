using Microsoft.UI.Xaml;
using DimToOff.Settings.Services;

namespace DimToOff.Settings;

public partial class App : Application
{
    private Window? window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        LaunchOptions options = LaunchOptions.Parse();
        window = options.Mode == LaunchMode.TrayMenu
            ? new TrayMenuWindow(options)
            : new MainWindow(options);
        window.Activate();
    }
}

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
        window = options.Mode switch
        {
            LaunchMode.TrayMenu => new TrayMenuWindow(options),
            LaunchMode.About => new AboutWindow(options),
            _ => new MainWindow(options)
        };
        window.Activate();
    }
}

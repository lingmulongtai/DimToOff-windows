using System.Diagnostics;

namespace DimToOff.Settings.Services;

public enum LaunchMode
{
    Settings,
    TrayMenu,
    About
}

public sealed class LaunchOptions
{
    public LaunchMode Mode { get; init; } = LaunchMode.Settings;
    public string MainExecutablePath { get; init; } = ResolveMainExecutablePath([]);
    public string PipeName { get; init; } = "DimToOff.Command";

    public static LaunchOptions Parse()
    {
        string[] args = Environment.GetCommandLineArgs();
        var mode = LaunchMode.Settings;
        string pipeName = "DimToOff.Command";

        for (int index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--tray-menu", StringComparison.OrdinalIgnoreCase))
            {
                mode = LaunchMode.TrayMenu;
            }
            else if (string.Equals(args[index], "--settings", StringComparison.OrdinalIgnoreCase))
            {
                mode = LaunchMode.Settings;
            }
            else if (string.Equals(args[index], "--about", StringComparison.OrdinalIgnoreCase))
            {
                mode = LaunchMode.About;
            }
            else if (string.Equals(args[index], "--pipe", StringComparison.OrdinalIgnoreCase) &&
                     index + 1 < args.Length &&
                     !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                pipeName = args[index + 1];
            }
        }

        return new LaunchOptions
        {
            Mode = mode,
            PipeName = pipeName,
            MainExecutablePath = ResolveMainExecutablePath(args)
        };
    }

    private static string ResolveMainExecutablePath(string[] args)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--main-exe", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(args[index + 1]))
            {
                return args[index + 1];
            }
        }

        string sameFolderPath = Path.Combine(AppContext.BaseDirectory, "DimToOff.exe");
        if (File.Exists(sameFolderPath))
        {
            return sameFolderPath;
        }

        return Process.GetCurrentProcess().MainModule?.FileName ?? sameFolderPath;
    }
}

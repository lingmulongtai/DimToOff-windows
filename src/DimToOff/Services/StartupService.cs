using Microsoft.Win32;
using System.Windows.Forms;

namespace DimToOff.Services;

internal sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DimToOff";
    private readonly LogService log;

    public StartupService(LogService log)
    {
        this.log = log;
    }

    public bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            log.Error("Failed to read startup registration", ex);
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled)
            {
                string executablePath = Application.ExecutablePath;
                key.SetValue(ValueName, $"\"{executablePath}\"");
                log.Info($"Startup registration enabled: {executablePath}");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                log.Info("Startup registration disabled");
            }
        }
        catch (Exception ex)
        {
            log.Error("Failed to update startup registration", ex);
        }
    }
}

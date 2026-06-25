using System.Text.Json;
using DimToOff.Models;

namespace DimToOff.Services;

internal sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly LogService log;
    private readonly string settingsFilePath;

    public SettingsService(LogService log)
    {
        this.log = log;
        string settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DimToOff");
        Directory.CreateDirectory(settingsDirectory);
        settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(settingsFilePath))
            {
                var defaults = new AppSettings();
                Save(defaults);
                return defaults;
            }

            string json = File.ReadAllText(settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            log.Error("Failed to load settings. Defaults will be used", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(settingsFilePath, json);
        }
        catch (Exception ex)
        {
            log.Error("Failed to save settings", ex);
        }
    }
}

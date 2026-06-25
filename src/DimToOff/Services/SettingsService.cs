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
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            Normalize(settings);
            Save(settings);
            return settings;
        }
        catch (Exception ex)
        {
            log.Error("Failed to load settings. Defaults will be used", ex);
            var settings = new AppSettings();
            Normalize(settings);
            return settings;
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Normalize(settings);
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(settingsFilePath, json);
        }
        catch (Exception ex)
        {
            log.Error("Failed to save settings", ex);
        }
    }

    private static void Normalize(AppSettings settings)
    {
        settings.OffThreshold = Math.Clamp(settings.OffThreshold, 0, 10);
        settings.DebounceMs = Math.Clamp(settings.DebounceMs, 300, 2000);
        settings.CooldownMs = Math.Clamp(settings.CooldownMs, 1500, 5000);
        settings.IgnoreInputMs = Math.Clamp(settings.IgnoreInputMs, 300, 2000);
        if (!string.Equals(settings.DisplayOffMode, "Blackout", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(settings.DisplayOffMode, "MonitorPower", StringComparison.OrdinalIgnoreCase))
        {
            settings.DisplayOffMode = "Blackout";
        }

        settings.MinimumRestoreBrightness = Math.Clamp(settings.MinimumRestoreBrightness, 30, 100);
        settings.DefaultRestoreBrightness = Math.Clamp(
            Math.Max(settings.DefaultRestoreBrightness, settings.MinimumRestoreBrightness),
            30,
            100);
    }
}

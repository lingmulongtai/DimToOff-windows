using System.Text.Json;
using DimToOff.Settings.Models;

namespace DimToOff.Settings.Services;

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public SettingsStore()
    {
        string settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DimToOff");
        Directory.CreateDirectory(settingsDirectory);
        SettingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public string SettingsFilePath { get; }

    public AppSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        string json = File.ReadAllText(SettingsFilePath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        Normalize(settings);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        Normalize(settings);
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }

    private static void Normalize(AppSettings settings)
    {
        settings.OffThreshold = Math.Clamp(settings.OffThreshold, 0, 10);
        settings.DebounceMs = Math.Clamp(settings.DebounceMs, 300, 2000);
        settings.CooldownMs = Math.Clamp(settings.CooldownMs, 1500, 5000);
        settings.IgnoreInputMs = Math.Clamp(settings.IgnoreInputMs, 300, 2000);
        settings.BrightnessSaveStableMs = Math.Clamp(settings.BrightnessSaveStableMs, 1000, 10000);
        settings.FadeToBlackMs = Math.Clamp(settings.FadeToBlackMs, 0, 1200);

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

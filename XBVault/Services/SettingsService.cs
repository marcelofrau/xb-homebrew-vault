using System;
using System.IO;
using System.Text.Json;
using XBVault.Models;

namespace XBVault.Services;

public static class SettingsService
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XBVault");

    private static readonly string SettingsPath = Path.Combine(AppDataDir, "settings.json");

    private static AppSettings? _current;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Current
    {
        get
        {
            if (_current is null)
                Load();
            return _current!;
        }
    }

    public static void Load()
    {
        if (!Directory.Exists(AppDataDir))
            Directory.CreateDirectory(AppDataDir);

        if (File.Exists(SettingsPath))
        {
            try
            {
                var json = File.ReadAllText(SettingsPath);
                _current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                Logger.Debug($"Settings loaded from {SettingsPath} ({json.Length} bytes)");
                return;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to deserialize settings from {SettingsPath}, falling back to defaults");
            }
        }
        else
        {
            Logger.Debug($"No settings file at {SettingsPath}, using defaults");
        }

        _current = new AppSettings();
    }

    public static void Reset()
    {
        _current = new AppSettings();
        Save();
        Logger.Info("Settings reset to defaults");
    }

    public static void Save()
    {
        if (!Directory.Exists(AppDataDir))
            Directory.CreateDirectory(AppDataDir);

        var json = JsonSerializer.Serialize(_current ?? new AppSettings(), _jsonOptions);

        File.WriteAllText(SettingsPath, json);
        Logger.Info($"Settings saved to {SettingsPath} ({json.Length} bytes)");
    }
}

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
                return;
            }
            catch
            {
            }
        }

        _current = new AppSettings();
    }

    public static void Save()
    {
        if (!Directory.Exists(AppDataDir))
            Directory.CreateDirectory(AppDataDir);

        var json = JsonSerializer.Serialize(_current ?? new AppSettings(), new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(SettingsPath, json);
    }
}

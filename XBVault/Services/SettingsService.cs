#nullable enable
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
    private static readonly object _gate = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Current
    {
        get
        {
            if (_current is null)
                lock (_gate)
                {
                    if (_current is null)
                        Load();
                }
            return _current!;
        }
    }

    public static void Load()
    {
        lock (_gate)
        {
            if (!Directory.Exists(AppDataDir))
                Directory.CreateDirectory(AppDataDir);

            if (File.Exists(SettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsPath);
                    _current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

                    // Validate XboxConnection — reset if corrupt
                    var conn = _current.XboxConnection;
                    if (conn.IsConfigured && (string.IsNullOrWhiteSpace(conn.Address) || conn.Port < 1 || conn.Port > 65535))
                    {
                        Logger.Error($"Settings: corrupt XboxConnection (Address='{conn.Address}', Port={conn.Port}) — resetting");
                        _current.XboxConnection = new Models.XboxConnection();
                    }

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
    }

    public static void Reset()
    {
        lock (_gate)
        {
            _current = new AppSettings();
            Save();
            Logger.Info("Settings reset to defaults");
        }
    }

    public static void Save()
    {
        lock (_gate)
        {
            if (!Directory.Exists(AppDataDir))
                Directory.CreateDirectory(AppDataDir);

            var json = JsonSerializer.Serialize(_current ?? new AppSettings(), _jsonOptions);

            File.WriteAllText(SettingsPath, json);
            Logger.Info($"Settings saved to {SettingsPath} ({json.Length} bytes)");
        }
    }

    internal static void SaveTo(string path, AppSettings settings)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(path, json);
    }

    internal static AppSettings LoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new AppSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}

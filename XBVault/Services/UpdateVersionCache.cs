using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace XBVault.Services;

public class CachedUpdate
{
    public string CatalogVersion { get; set; } = string.Empty;
    public string InstalledVersion { get; set; } = string.Empty;
}

public class UpdateVersionCache
{
    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XBVault", "cache");

    private static readonly string CacheFilePath = Path.Combine(CacheRoot, "update-versions.json");
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly string _cacheFilePath;
    private Dictionary<string, CachedUpdate> _cache = [];

    public UpdateVersionCache(string? cacheFilePath = null)
    {
        _cacheFilePath = cacheFilePath ?? CacheFilePath;
        Load();
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
            {
                _cache = [];
                return;
            }
            var json = File.ReadAllText(_cacheFilePath);
            _cache = JsonSerializer.Deserialize<Dictionary<string, CachedUpdate>>(json) ?? [];
        }
        catch
        {
            _cache = [];
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_cache, JsonOpts);
            File.WriteAllText(_cacheFilePath, json);
        }
        catch
        {
        }
    }

    public void RecordUpdate(string itemName, string catalogVersion, string installedVersion)
    {
        _cache[itemName] = new CachedUpdate
        {
            CatalogVersion = catalogVersion,
            InstalledVersion = installedVersion
        };
        Save();
    }

    public bool TryGetSuppressed(string itemName, string catalogVersion, string installedVersion)
    {
        if (_cache.TryGetValue(itemName, out var entry))
        {
            return entry.CatalogVersion == catalogVersion && entry.InstalledVersion == installedVersion;
        }
        return false;
    }
}

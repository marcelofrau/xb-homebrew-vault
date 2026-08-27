#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace XBVault.Services;

/// <summary>
/// Persists user-authored per-machine catalog overrides to a local JSON file
/// (<c>%APPDATA%/XBVault/local-overrides.json</c>). Created automatically after
/// an install when the installed package's real identity does not match the
/// catalog entry by any heuristic, so the installed↔catalog match stops guessing.
/// Local overrides take priority over the shipped (embedded + remote) overrides.
/// </summary>
public sealed class LocalOverrideService : IDisposable
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XBVault");

    private static readonly string DefaultPath = Path.Combine(AppDataDir, "local-overrides.json");

    private readonly object _gate = new();
    private readonly Dictionary<string, string> _byName = new(StringComparer.OrdinalIgnoreCase);
    private string _path;
    private bool _disposed;

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public LocalOverrideService() : this(DefaultPath) { }

    public LocalOverrideService(string path)
    {
        _path = path;
    }

    public int Count
    {
        get { lock (_gate) return _byName.Count; }
    }

    public IReadOnlyList<LocalOverrideEntry> Entries
    {
        get
        {
            lock (_gate)
                return _byName
                    .Select(kv => new LocalOverrideEntry(kv.Key, kv.Value))
                    .OrderBy(e => e.PackageName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }
    }

    public void Load()
    {
        lock (_gate)
        {
            _byName.Clear();
            if (!File.Exists(_path))
            {
                Logger.Debug($"LocalOverrideService: no file at {_path}, starting empty");
                return;
            }

            try
            {
                var json = File.ReadAllText(_path);
                var doc = JsonSerializer.Deserialize<LocalOverrideFile>(json, _jsonOptions);
                foreach (var entry in doc?.Overrides ?? [])
                {
                    if (string.IsNullOrWhiteSpace(entry.PackageName) || string.IsNullOrWhiteSpace(entry.CatalogId))
                        continue;
                    _byName[entry.PackageName.Trim()] = entry.CatalogId.Trim();
                }
                Logger.Debug($"LocalOverrideService: loaded {_byName.Count} override(s) from {_path}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"LocalOverrideService: failed to parse {_path}, ignoring");
                _byName.Clear();
            }
        }
    }

    public void Save()
    {
        lock (_gate)
        {
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var file = new LocalOverrideFile
                {
                    Overrides = _byName
                        .Select(kv => new LocalOverrideEntry(kv.Key, kv.Value))
                        .OrderBy(e => e.PackageName, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
                File.WriteAllText(_path, JsonSerializer.Serialize(file, _jsonOptions));
                Logger.Debug($"LocalOverrideService: saved {_byName.Count} override(s) to {_path}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"LocalOverrideService: failed to save to {_path}");
            }
        }
    }

    public bool TryGetCatalogIdByName(string name, out string? catalogId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            catalogId = null;
            return false;
        }

        lock (_gate)
            return _byName.TryGetValue(name.Trim(), out catalogId);
    }

    public void AddOrUpdate(string name, string catalogId)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(catalogId))
            return;

        var exists = false;
        lock (_gate)
        {
            exists = _byName.ContainsKey(name.Trim());
            _byName[name.Trim()] = catalogId.Trim();
        }

        if (exists)
            Logger.Info($"LocalOverrideService: updated {name.Trim()} → {catalogId.Trim()}");
        else
            Logger.Info($"LocalOverrideService: added {name.Trim()} → {catalogId.Trim()}");
        Save();
    }

    public bool Remove(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        bool removed;
        lock (_gate)
            removed = _byName.Remove(name.Trim());

        if (removed)
        {
            Logger.Info($"LocalOverrideService: removed {name.Trim()}");
            Save();
        }
        return removed;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    private sealed class LocalOverrideFile
    {
        public List<LocalOverrideEntry> Overrides { get; set; } = [];
    }
}

public record LocalOverrideEntry(string PackageName, string CatalogId);

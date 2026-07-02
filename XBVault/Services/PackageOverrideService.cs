using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform;

namespace XBVault.Services;

public sealed class PackageOverrideService : IDisposable
{
    private const string GitHubRawUrl =
        "https://raw.githubusercontent.com/marcelofrau/xb-homebrew-vault/main/XBVault/Assets/package-overrides.json";

    private const string EmbeddedAssetPath = "avares://XBVault/Assets/package-overrides.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private Dictionary<string, string> _byPfn = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _byName = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _imageByPfn = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _imageByName = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public PackageOverrideService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.Add("User-Agent", "XB Homebrew Vault");
    }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            LoadEmbedded();
            Logger.Debug("PackageOverrideService: embedded overrides loaded");
        }
        catch (Exception ex)
        {
            Logger.Warn($"PackageOverrideService: embedded load failed: {ex.Message}");
        }

        _ = FetchRemoteAsync();
    }

    private void LoadEmbedded()
    {
        var uri = new Uri(EmbeddedAssetPath);
        using var stream = AssetLoader.Open(uri);
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        ParseAndMerge(json);
    }

    private async Task FetchRemoteAsync()
    {
        try
        {
            Logger.Debug("PackageOverrideService: fetching remote overrides...");
            var response = await _http.GetAsync(GitHubRawUrl);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Debug($"PackageOverrideService: remote returned {response.StatusCode}, using embedded only");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
            {
                Logger.Debug("PackageOverrideService: remote empty, using embedded only");
                return;
            }

            var localCount = _byPfn.Count + _byName.Count;
            ParseAndMerge(json);
            Logger.Info($"PackageOverrideService: remote overrides merged ({_byPfn.Count + _byName.Count} total, +{_byPfn.Count + _byName.Count - localCount} new)");
        }
        catch (HttpRequestException ex)
        {
            Logger.Debug($"PackageOverrideService: remote fetch failed ({ex.Message}), using embedded only");
        }
        catch (TaskCanceledException)
        {
            Logger.Debug("PackageOverrideService: remote fetch timed out, using embedded only");
        }
        catch (Exception ex)
        {
            Logger.Warn($"PackageOverrideService: remote fetch error: {ex.Message}");
        }
    }

    private void ParseAndMerge(string json)
    {
        var data = JsonSerializer.Deserialize<PackageOverrideData>(json, JsonOptions);
        if (data is null) return;

        if (data.PackageFamilyNameOverrides is not null)
        {
            foreach (var entry in data.PackageFamilyNameOverrides)
            {
                var key = entry.PackageFamilyName?.Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (!string.IsNullOrWhiteSpace(entry.CatalogId))
                    _byPfn[key] = entry.CatalogId.Trim();

                if (!string.IsNullOrWhiteSpace(entry.ImageUrl))
                    _imageByPfn[key] = entry.ImageUrl.Trim();
            }
        }

        if (data.PackageNameOverrides is not null)
        {
            foreach (var entry in data.PackageNameOverrides)
            {
                var key = entry.PackageName?.Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (!string.IsNullOrWhiteSpace(entry.CatalogId))
                    _byName[key] = entry.CatalogId.Trim();

                if (!string.IsNullOrWhiteSpace(entry.ImageUrl))
                    _imageByName[key] = entry.ImageUrl.Trim();
            }
        }
    }

    public bool TryGetCatalogId(string packageFamilyName, out string? catalogId)
    {
        return _byPfn.TryGetValue(packageFamilyName, out catalogId);
    }

    public bool TryGetCatalogIdByName(string packageName, out string? catalogId)
    {
        return _byName.TryGetValue(packageName, out catalogId);
    }

    public bool TryGetImageUrl(string packageFamilyName, out string? imageUrl)
    {
        return _imageByPfn.TryGetValue(packageFamilyName, out imageUrl);
    }

    public bool TryGetImageUrlByName(string packageName, out string? imageUrl)
    {
        return _imageByName.TryGetValue(packageName, out imageUrl);
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}

internal sealed class PackageOverrideData
{
    public List<PackageOverrideEntry>? PackageFamilyNameOverrides { get; set; }
    public List<PackageOverrideEntry>? PackageNameOverrides { get; set; }
}

internal sealed class PackageOverrideEntry
{
    public string PackageFamilyName { get; set; } = "";
    public string PackageName { get; set; } = "";
    public string CatalogId { get; set; } = "";
    public string? ImageUrl { get; set; }
}

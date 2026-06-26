using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XBVault.Helpers;
using XBVault.Models;

namespace XBVault.Services;

/// <summary>
/// Fetches catalog from JSON API. Falls back to stale cache on failure.
/// Cache: 6 hours TTL, persisted to disk.
/// </summary>
public partial class CatalogApiService
{
    private const string JsonApiUrl = "https://emulationrevival.github.io/api/catalog.json";
    private const int CacheTtlHours = 6;
    private const int ExpectedSchemaVersion = 1;

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XBVault", "cache");

    private static readonly string CachePath = Path.Combine(CacheDir, "catalog-api.json");

    // Regex for identifying dependency packages
    [GeneratedRegex(@"microsoft\.|vclibs|\.net\.core|ui\.xaml|windowsappsdk|directx|webview2",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DepPattern();

    private readonly HttpClient _http;

    public CatalogApiService()
    {
        _http = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", $"XB Homebrew Vault/{BuildInfo.Version}");
    }

    /// <summary>
    /// Fetch catalog items from JSON API. Returns cached data if API fails.
    /// </summary>
    public async Task<List<CatalogItem>> FetchCatalogAsync(
        bool forceRefresh = false,
        IProgress<(string Status, double Progress)>? progress = null)
    {
        Logger.Debug($"CatalogApiService.FetchCatalogAsync(forceRefresh={forceRefresh})");

        // Check cache first (unless forced refresh)
        if (!forceRefresh)
        {
            var cached = LoadFromCache();
            if (cached is not null && cached.Count > 0)
            {
                progress?.Report(("Loaded from cache", 1.0));
                Logger.Info($"Catalog loaded from cache: {cached.Count} items");
                return cached;
            }
        }

        // Fetch from JSON API
        progress?.Report(("Fetching catalog...", 0.1));
        var items = await TryFetchJsonApiAsync(progress);

        if (items is not null && items.Count > 0)
        {
            progress?.Report(($"Complete: {items.Count} items", 1.0));
            return items;
        }

        // JSON API failed — try stale cache as fallback
        Logger.Warn("JSON API failed, trying stale cache");
        progress?.Report(("Using cached catalog...", 0.5));
        var stale = LoadFromCache(ignoreTtl: true);
        if (stale is not null && stale.Count > 0)
        {
            Logger.Warn($"Using stale cache: {stale.Count} items");
            progress?.Report(($"Using cached catalog ({stale.Count} items)", 1.0));
            return stale;
        }

        Logger.Error("Catalog fetch failed — no cache available");
        progress?.Report(("Failed to load catalog", 1.0));
        return [];
    }

    private async Task<List<CatalogItem>?> TryFetchJsonApiAsync(
        IProgress<(string Status, double Progress)>? progress)
    {
        try
        {
            progress?.Report(("Downloading JSON catalog...", 0.2));
            Logger.Debug($"Fetching: {JsonApiUrl}");

            var response = await _http.GetAsync(JsonApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"JSON API returned {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            Logger.Debug($"JSON response: {json.Length} bytes");

            progress?.Report(("Parsing catalog...", 0.5));
            var apiResponse = JsonSerializer.Deserialize<CatalogApiResponse>(json);

            if (apiResponse is null)
            {
                Logger.Warn("Failed to deserialize JSON API response");
                return null;
            }

            // Schema version check (warn but continue)
            if (apiResponse.SchemaVersion != ExpectedSchemaVersion)
            {
                Logger.Warn($"Schema version mismatch: expected {ExpectedSchemaVersion}, got {apiResponse.SchemaVersion}. Attempting to parse anyway.");
            }

            Logger.Info($"JSON API: {apiResponse.Items.Count} items, generated at {apiResponse.GeneratedAt:u}");

            progress?.Report(("Processing items...", 0.7));
            var items = apiResponse.Items
                .Select(ConvertToCatalogItem)
                .ToList();

            // Classify downloads
            foreach (var item in items)
            {
                ClassifyDownloads(item);
            }

            progress?.Report(("Saving to cache...", 0.9));
            SaveToCache(items, "json_api");

            return items;
        }
        catch (HttpRequestException ex)
        {
            Logger.Warn($"JSON API network error: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            Logger.Warn($"JSON API parse error: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected error fetching JSON API");
            return null;
        }
    }

    private static CatalogItem ConvertToCatalogItem(CatalogApiItem api)
    {
        // Find primary download URL (first non-dependency, or first available)
        var mainDownload = api.Downloads.FirstOrDefault();
        var downloadUrl = mainDownload?.Url ?? api.DownloadUrl;

        // Extract developer, porter, and maintainer from contributors
        string? developer = null;
        string? uwpPortBy = null;
        string? maintainedBy = null;

        if (api.Contributors is not null)
        {
            developer = api.Contributors.Developers?.FirstOrDefault()?.Name;
            uwpPortBy = api.Contributors.Porters?.FirstOrDefault()?.Name;
            maintainedBy = api.Contributors.Maintainers?.FirstOrDefault()?.Name;
        }

        return new CatalogItem
        {
            Id = api.Id,
            Name = api.Title,
            Description = api.Description,
            Version = api.Version,
            ReleaseDate = api.ReleaseDate,
            Developer = developer,
            UwpPortBy = uwpPortBy,
            MaintainedBy = maintainedBy,
            Category = api.Category,
            Compatibility = api.Compatibility,
            IsExperimental = api.IsExperimental,
            Requirements = api.Requirements,
            Features = api.Features,

            // Legacy fields for compatibility
            DownloadUrl = downloadUrl,
            SourceUrl = api.SourceCodeUrl,
            ImageUrl = api.ImageUrl,

            // New fields
            Url = api.PageUrl,
            SourceCodeUrl = api.SourceCodeUrl,
            SetupGuideUrl = api.SetupGuideUrl,
            TutorialUrl = api.TutorialUrl,
            ReleaseNotesUrl = api.ReleaseNotesUrl,
            Contributors = api.AllContributors,
            Downloads = api.Downloads
        };
    }



    private static void ClassifyDownloads(CatalogItem item)
    {
        foreach (var download in item.Downloads)
        {
            download.DownloadType = ClassifyDownload(download);
        }

        // If no main package identified, mark first as main
        if (item.Downloads.Count > 0 &&
            !item.Downloads.Any(d => d.DownloadType == DownloadType.MainPackage))
        {
            var first = item.Downloads.First(d => d.DownloadType != DownloadType.Dependency);
            if (first is not null)
                first.DownloadType = DownloadType.MainPackage;
            else
                item.Downloads[0].DownloadType = DownloadType.MainPackage;
        }
    }

    private static DownloadType ClassifyDownload(DownloadAsset download)
    {
        var url = download.Url.ToLowerInvariant();
        var label = (download.Label ?? string.Empty).ToLowerInvariant();

        // Check for dependency patterns in label
        if (label.Contains("dependency") || label.Contains("dep"))
            return DownloadType.Dependency;

        // Check for mod link
        if (label.Contains("mod link") || label == "mod link")
            return DownloadType.ModLink;

        // Check for dependency patterns in URL
        if (DepPattern().IsMatch(url) || DepPattern().IsMatch(label))
            return DownloadType.Dependency;

        // Check for browse/mod links (not direct downloads)
        if (!url.EndsWith(".appx") && !url.EndsWith(".msix") &&
            !url.EndsWith(".zip") && !url.EndsWith(".msixbundle") &&
            !url.EndsWith(".appxbundle"))
        {
            if (url.Contains("/mod") || label.Contains("mod"))
                return DownloadType.ModLink;

            if (url.Contains("moddb.com"))
                return DownloadType.ModLink;

            if (url.Contains("github.com") && !url.Contains("/releases/"))
                return DownloadType.BrowseLink;

            return DownloadType.BrowseLink;
        }

        return DownloadType.MainPackage;
    }

    private static List<CatalogItem>? LoadFromCache(bool ignoreTtl = false)
    {
        try
        {
            if (!File.Exists(CachePath))
            {
                Logger.Debug("No cache file found");
                return null;
            }

            var json = File.ReadAllText(CachePath);
            var cache = JsonSerializer.Deserialize<CatalogCache>(json);

            if (cache is null || cache.Data is null)
            {
                Logger.Debug("Cache file empty or invalid");
                return null;
            }

            // Check TTL unless ignoreTtl (stale fallback)
            if (!ignoreTtl)
            {
                var age = DateTime.UtcNow - cache.FetchedAt;
                if (age.TotalHours > CacheTtlHours)
                {
                    Logger.Debug($"Cache expired: {age.TotalHours:F1}h old (TTL: {CacheTtlHours}h)");
                    return null;
                }

                Logger.Debug($"Cache valid: {age.TotalMinutes:F0}min old, source={cache.Source}");
            }
            else
            {
                var age = DateTime.UtcNow - cache.FetchedAt;
                Logger.Warn($"Using stale cache ({age.TotalHours:F1}h old, source={cache.Source})");
            }

            // Convert API items to CatalogItems
            var items = cache.Data.Items
                .Select(ConvertToCatalogItem)
                .ToList();

            // Re-classify downloads
            foreach (var item in items)
            {
                ClassifyDownloads(item);
            }

            return items;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to load cache: {ex.Message}");
            return null;
        }
    }

    private static void SaveToCache(List<CatalogItem> items, string source)
    {
        try
        {
            if (!Directory.Exists(CacheDir))
                Directory.CreateDirectory(CacheDir);

            // Convert back to API format for storage
            var apiItems = items.Select(item => new CatalogApiItem
            {
                Id = item.Id,
                Title = item.Name,
                Description = item.Description,
                Version = item.Version,
                ReleaseDate = item.ReleaseDate,
                Category = item.Category,
                CategorySlug = item.Category.ToLowerInvariant().Replace(" ", "-"),
                Compatibility = item.Compatibility,
                IsExperimental = item.IsExperimental,
                ImageUrl = item.ImageUrl,
                PageUrl = item.Url,
                DownloadUrl = item.DownloadUrl,
                SourceCodeUrl = item.SourceCodeUrl ?? item.SourceUrl,
                SetupGuideUrl = item.SetupGuideUrl,
                TutorialUrl = item.TutorialUrl,
                ReleaseNotesUrl = item.ReleaseNotesUrl,
                Requirements = item.Requirements,
                Features = item.Features,
                Downloads = item.Downloads,
                Contributors = ReconstructContributorsMap(item.Contributors)
            }).ToList();

            var cache = new CatalogCache
            {
                FetchedAt = DateTime.UtcNow,
                Source = source,
                Data = new CatalogApiResponse
                {
                    SchemaVersion = ExpectedSchemaVersion,
                    GeneratedAt = DateTime.UtcNow,
                    Items = apiItems
                }
            };

            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(CachePath, json);
            Logger.Info($"Cache saved: {items.Count} items to {CachePath}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to save cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear the cache file (for manual refresh)
    /// </summary>
    public static void ClearCache()
    {
        try
        {
            if (File.Exists(CachePath))
            {
                File.Delete(CachePath);
                Logger.Info("Cache cleared");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to clear cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Reconstruct ContributorsMap from flat Contributor list for cache storage
    /// </summary>
    private static ContributorsMap? ReconstructContributorsMap(List<Contributor> contributors)
    {
        if (contributors.Count == 0) return null;

        var groups = contributors
            .Where(c => c.Role is not null)
            .GroupBy(c => c.Role!)
            .ToDictionary(g => g.Key, g => g.ToList());
        var map = new ContributorsMap();

        if (groups.TryGetValue("Developer", out var devs))
            map.Developers = devs.Select(c => new ContributorEntry { Name = c.Name, Github = c.Url }).ToList();

        if (groups.TryGetValue("Porter", out var porters))
            map.Porters = porters.Select(c => new ContributorEntry { Name = c.Name, Github = c.Url }).ToList();

        if (groups.TryGetValue("Maintainer", out var mains))
            map.Maintainers = mains.Select(c => new ContributorEntry { Name = c.Name, Github = c.Url }).ToList();

        if (groups.TryGetValue("Mod Author", out var mods))
            map.ModAuthors = mods.Select(c => new ContributorEntry { Name = c.Name, Github = c.Url }).ToList();

        if (groups.TryGetValue("Prebuilt By", out var prebuilt))
            map.PrebuiltBy = prebuilt.Select(c => new ContributorEntry { Name = c.Name, Github = c.Url }).ToList();

        return map;
    }
}

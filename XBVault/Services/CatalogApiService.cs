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
/// Fetches catalog from JSON API with HTML scraping fallback.
/// Cache: 4 hours TTL, persisted to disk.
/// </summary>
public partial class CatalogApiService
{
    private const string JsonApiUrl = "https://emulationrevival.github.io/api/catalog.json";
    private const int CacheTtlHours = 4;
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
    private readonly EmulationRevivalService _fallbackService;

    public CatalogApiService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", $"XB Homebrew Vault/{BuildInfo.Version}");
        _fallbackService = new EmulationRevivalService();
    }

    /// <summary>
    /// Fetch catalog items. Uses JSON API with HTML fallback.
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

        // Try JSON API first
        progress?.Report(("Fetching catalog...", 0.1));
        var items = await TryFetchJsonApiAsync(progress);

        if (items is not null && items.Count > 0)
        {
            progress?.Report(($"Complete: {items.Count} items", 1.0));
            return items;
        }

        // Fallback to HTML scraping
        Logger.Warn("JSON API failed, falling back to HTML scraping");
        progress?.Report(("Falling back to HTML scraping...", 0.2));
        return await _fallbackService.FetchCatalogAsync(forceRefresh, progress);
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

        // Extract developer and porter from contributors
        string? developer = null;
        string? uwpPortBy = null;

        if (api.Contributors is not null)
        {
            developer = api.Contributors.Developers?.FirstOrDefault()?.Name;
            uwpPortBy = api.Contributors.Porters?.FirstOrDefault()?.Name;
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
            Category = MapCategory(api.CategorySlug),
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

    /// <summary>
    /// Maps category slug to display category
    /// </summary>
    private static string MapCategory(string categorySlug)
    {
        return categorySlug switch
        {
            "emulators" => "Emulator",
            "frontends" => "Frontend",
            "ports" => "GamePort",
            "apps" => "App",
            "experimental-apps" => "Experimental",
            "media-apps" => "Media",
            "utilities" => "Utility",
            "gzdoom-mods" => "GZDoom",
            _ => categorySlug
        };
    }

    private void ClassifyDownloads(CatalogItem item)
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

    private DownloadType ClassifyDownload(DownloadAsset download)
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

    private static List<CatalogItem>? LoadFromCache()
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

            // Check TTL
            var age = DateTime.UtcNow - cache.FetchedAt;
            if (age.TotalHours > CacheTtlHours)
            {
                Logger.Debug($"Cache expired: {age.TotalHours:F1}h old (TTL: {CacheTtlHours}h)");
                return null;
            }

            Logger.Debug($"Cache valid: {age.TotalMinutes:F0}min old, source={cache.Source}");

            // Convert API items to CatalogItems
            var items = cache.Data.Items
                .Select(ConvertToCatalogItem)
                .ToList();

            // Re-classify downloads
            var service = new CatalogApiService();
            foreach (var item in items)
            {
                service.ClassifyDownloads(item);
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
                Downloads = item.Downloads
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
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using XBVault.Helpers;
using XBVault.Models;

namespace XBVault.Services;

public partial class EmulationRevivalService
{
    private const string BaseUrl = "https://emulationrevival.github.io";
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XBVault", "cache", "catalog");

    private static bool IsCacheStale()
    {
        var path = Path.Combine(CacheDir, "catalog.json");
        if (!File.Exists(path)) return true;

        var elapsed = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
        return elapsed.TotalHours > 6;
    }

    private static readonly (string Path, string Category)[] Pages =
    [
        ("/xbox-dev-mode/emulators.html", "Emulator"),
        ("/xbox-dev-mode/frontends.html", "Frontend"),
        ("/xbox-dev-mode/ports.html", "GamePort"),
        ("/xbox-dev-mode/apps.html", "App"),
        ("/xbox-dev-mode/experimental-apps.html", "Experimental"),
        ("/xbox-dev-mode/media-apps.html", "Media"),
        ("/xbox-dev-mode/utilities.html", "Utility")
    ];

    private readonly HttpClient _http;

    public EmulationRevivalService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", $"XB Homebrew Vault/{BuildInfo.Version}");
    }

    public async Task<List<CatalogItem>> FetchCatalogAsync(
        bool forceRefresh = false,
        IProgress<(string Status, double Progress)>? progress = null)
    {
        Logger.Debug($"FetchCatalogAsync(forceRefresh={forceRefresh})");

        if (!forceRefresh)
        {
            var cached = LoadFromCache();
            if (cached.Count > 0)
            {
                if (!IsCacheStale())
                {
                    progress?.Report(("Loaded from cache", 1.0));
                    Logger.Info($"Catalog loaded from cache: {cached.Count} items");
                    Logger.Debug($"Cache file: {CacheDir}");
                    return cached;
                }
                Logger.Debug("Cache expired — fetching from web");
            }
            else
            {
                Logger.Debug("Cache empty or missing — fetching from web");
            }
        }

        progress?.Report(("Downloading catalog...", 0.0));
        var items = new List<CatalogItem>();
        var pageIndex = 0;
        var totalPages = Pages.Length;

        foreach (var (path, category) in Pages)
        {
            try
            {
                var status = $"Scraping {category}...";
                var pct = (double)pageIndex / totalPages;
                progress?.Report((status, pct));
                Logger.Debug($"Scraping {category} from {path}");

                var pageItems = await ScrapePageAsync(path, category);
                Logger.Debug($"  → {pageItems.Count} items from {category}");
                items.AddRange(pageItems);
                pageIndex++;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to scrape {category} at {path}");
                pageIndex++;
            }
        }

        progress?.Report(("Deduplicating...", 0.9));
        var beforeDedup = items.Count;
        items = items.GroupBy(i => i.Name.ToLowerInvariant())
                     .Select(g => g.First())
                     .ToList();
        var dupes = beforeDedup - items.Count;
        if (dupes > 0)
            Logger.Debug($"Removed {dupes} duplicate items");

        progress?.Report(($"Saving {items.Count} items...", 0.95));
        SaveToCache(items);

        progress?.Report(($"Complete: {items.Count} items", 1.0));
        Logger.Info($"Catalog fetched: {items.Count} items across {Pages.Length} categories");
        return items;
    }

    private async Task<List<CatalogItem>> ScrapePageAsync(string path, string category)
    {
        var items = new List<CatalogItem>();
        var url = BaseUrl + path;
        var html = await _http.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var cards = doc.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' card ')]");
        if (cards is null) return items;

        foreach (var card in cards)
        {
            try
            {
                var item = ParseCard(card, category);
                if (item is not null)
                {
                    items.Add(item);
                    Logger.Trace($"Parsed: [{item.Category}] {item.Name} v{item.Version}");
                }
            }
            catch (Exception ex)
            {
                Logger.Trace($"ParseCard failed: {ex.Message}");
            }
        }

        Logger.Debug($"{path}: {items.Count} items parsed");
        return items;
    }

    private static CatalogItem? ParseCard(HtmlNode card, string category)
    {
        var name = card.SelectSingleNode(".//h3")?.InnerText.Trim();
        if (string.IsNullOrWhiteSpace(name)) return null;

        var id = Slugify(name) + "-" + Guid.NewGuid().ToString("N")[..8];

        var descriptionNode = card.SelectSingleNode(".//h3/following-sibling::p[1]");
        var description = descriptionNode?.InnerText.Trim() ?? string.Empty;

        var listNodes = card.SelectNodes(".//li");
        var listItems = listNodes?.Cast<HtmlNode>().ToArray() ?? [];
        string? version = null, releaseDate = null, developer = null;
        string? uwpPortBy = null;
        string? compatibility = null;
        var requirements = new List<string>();
        var features = new List<string>();

        foreach (var li in listItems)
        {
            var text = li.InnerText.Trim();
            if (text.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
                version = text["Version:".Length..].Trim();
            else if (text.StartsWith("Release date:", StringComparison.OrdinalIgnoreCase))
                releaseDate = text["Release date:".Length..].Trim();
            else if (text.StartsWith("Developer:", StringComparison.OrdinalIgnoreCase))
                developer = text["Developer:".Length..].Trim();
            else if (text.StartsWith("UWP Port by:", StringComparison.OrdinalIgnoreCase))
            {
                var link = li.SelectSingleNode(".//a[contains(@class,'contributor-name-link')]");
                uwpPortBy = link?.InnerText.Trim();
            }
            else if (text.StartsWith("Compatibility:", StringComparison.OrdinalIgnoreCase))
                compatibility = text["Compatibility:".Length..].Trim();
            else if (text.StartsWith("Requires:", StringComparison.OrdinalIgnoreCase))
                requirements.AddRange(
                    text["Requires:".Length..].Trim().Split(',', StringSplitOptions.TrimEntries));
            else if (text.StartsWith("Features:", StringComparison.OrdinalIgnoreCase))
                features.AddRange(
                    text["Features:".Length..].Trim().Split(',', StringSplitOptions.TrimEntries));
        }

        var downloadLink = card.SelectSingleNode(".//a[contains(@href,'.appx') or contains(@href,'.msix') or contains(@href,'.zip')]");
        var downloadUrl = downloadLink?.GetAttributeValue("href", string.Empty);
        downloadUrl = string.IsNullOrEmpty(downloadUrl) ? null : downloadUrl;

        var sourceLink = card.SelectSingleNode(".//a[contains(@href,'github.com')]");
        var sourceUrl = sourceLink?.GetAttributeValue("href", string.Empty);
        sourceUrl = string.IsNullOrEmpty(sourceUrl) ? null : sourceUrl;

        var img = card.SelectSingleNode(".//img");
        var imageUrl = img?.GetAttributeValue("src", string.Empty);
        imageUrl = string.IsNullOrEmpty(imageUrl) ? null : imageUrl;

        return new CatalogItem
        {
            Id = id,
            Name = name,
            Description = description,
            Version = version ?? string.Empty,
            ReleaseDate = releaseDate,
            Developer = developer,
            UwpPortBy = uwpPortBy,
            DownloadUrl = downloadUrl is not null ? NormalizeUrl(downloadUrl) : null,
            SourceUrl = sourceUrl is not null ? NormalizeUrl(sourceUrl) : null,
            ImageUrl = imageUrl is not null ? NormalizeUrl(imageUrl) : null,
            Category = category,
            Compatibility = compatibility ?? string.Empty,
            Requirements = requirements,
            Features = features,
            IsExperimental = category == "Experimental"
        };
    }

    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url.StartsWith("//")) return "https:" + url;
        if (url.StartsWith('/')) return BaseUrl + url;
        return url;
    }

    private static string Slugify(string text)
    {
        return Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
    }

    private static List<CatalogItem> LoadFromCache()
    {
        try
        {
            var path = Path.Combine(CacheDir, "catalog.json");
            if (!File.Exists(path)) return [];

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<CatalogItem>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void SaveToCache(List<CatalogItem> items)
    {
        try
        {
            if (!Directory.Exists(CacheDir))
                Directory.CreateDirectory(CacheDir);

            var path = Path.Combine(CacheDir, "catalog.json");
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
        }
        catch
        {
        }
    }
}

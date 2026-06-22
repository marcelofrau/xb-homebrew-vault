using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace XBVault.Models;

/// <summary>
/// Root response from catalog.json API
/// </summary>
public class CatalogApiResponse
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; }

    [JsonPropertyName("items")]
    public List<CatalogApiItem> Items { get; set; } = [];
}

/// <summary>
/// Single item from the JSON catalog API
/// </summary>
public class CatalogApiItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("categorySlug")]
    public string CategorySlug { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("compatibility")]
    public string Compatibility { get; set; } = string.Empty;

    [JsonPropertyName("isExperimental")]
    public bool IsExperimental { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("pageUrl")]
    public string? PageUrl { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("sourceCodeUrl")]
    public string? SourceCodeUrl { get; set; }

    [JsonPropertyName("setupGuideUrl")]
    public string? SetupGuideUrl { get; set; }

    [JsonPropertyName("tutorialUrl")]
    public string? TutorialUrl { get; set; }

    [JsonPropertyName("releaseNotesUrl")]
    public string? ReleaseNotesUrl { get; set; }

    [JsonPropertyName("requirements")]
    public List<string> Requirements { get; set; } = [];

    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = [];

    [JsonPropertyName("contributors")]
    public ContributorsMap? Contributors { get; set; }

    [JsonPropertyName("downloads")]
    public List<DownloadAsset> Downloads { get; set; } = [];

    /// <summary>
    /// Flattens contributors map into single list for UI display
    /// </summary>
    [JsonIgnore]
    public List<Contributor> AllContributors
    {
        get
        {
            if (Contributors is null) return [];

            var all = new List<Contributor>();

            if (Contributors.Developers?.Count > 0)
                all.AddRange(Contributors.Developers.Select(c => new Contributor
                {
                    Name = c.Name,
                    Role = "Developer",
                    Url = c.Github
                }));

            if (Contributors.Porters?.Count > 0)
                all.AddRange(Contributors.Porters.Select(c => new Contributor
                {
                    Name = c.Name,
                    Role = "Porter",
                    Url = c.Github
                }));

            if (Contributors.Maintainers?.Count > 0)
                all.AddRange(Contributors.Maintainers.Select(c => new Contributor
                {
                    Name = c.Name,
                    Role = "Maintainer",
                    Url = c.Github
                }));

            if (Contributors.ModAuthors?.Count > 0)
                all.AddRange(Contributors.ModAuthors.Select(c => new Contributor
                {
                    Name = c.Name,
                    Role = "Mod Author",
                    Url = c.Github
                }));

            if (Contributors.PrebuiltBy?.Count > 0)
                all.AddRange(Contributors.PrebuiltBy.Select(c => new Contributor
                {
                    Name = c.Name,
                    Role = "Prebuilt By",
                    Url = c.Github
                }));

            return all;
        }
    }
}

/// <summary>
/// Contributors map from JSON (developers, porters, maintainers, etc.)
/// </summary>
public class ContributorsMap
{
    [JsonPropertyName("developers")]
    public List<ContributorEntry>? Developers { get; set; }

    [JsonPropertyName("porters")]
    public List<ContributorEntry>? Porters { get; set; }

    [JsonPropertyName("maintainers")]
    public List<ContributorEntry>? Maintainers { get; set; }

    [JsonPropertyName("mod_authors")]
    public List<ContributorEntry>? ModAuthors { get; set; }

    [JsonPropertyName("prebuilt_by")]
    public List<ContributorEntry>? PrebuiltBy { get; set; }
}

/// <summary>
/// Single contributor entry from JSON
/// </summary>
public class ContributorEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("github")]
    public string? Github { get; set; }

    [JsonPropertyName("donations")]
    public List<DonationLink>? Donations { get; set; }
}

/// <summary>
/// Donation link
/// </summary>
public class DonationLink
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Simplified contributor for UI display
/// </summary>
public class Contributor
{
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Url { get; set; }
}

/// <summary>
/// Download asset with type classification
/// </summary>
public class DownloadAsset
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("assetId")]
    public string? AssetId { get; set; }

    /// <summary>
    /// Classified download type (computed, not from JSON)
    /// </summary>
    [JsonIgnore]
    public DownloadType DownloadType { get; set; } = DownloadType.Unknown;
}

/// <summary>
/// Classification of download URLs
/// </summary>
public enum DownloadType
{
    Unknown,
    MainPackage,
    Dependency,
    ModLink,
    BrowseLink
}

/// <summary>
/// Cached catalog with metadata
/// </summary>
public class CatalogCache
{
    [JsonPropertyName("fetchedAt")]
    public DateTime FetchedAt { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "json_api";

    [JsonPropertyName("data")]
    public CatalogApiResponse? Data { get; set; }
}

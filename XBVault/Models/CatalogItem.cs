using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XBVault.Models;

public partial class CatalogItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string? AppId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? ReleaseDate { get; set; }
    public string? Developer { get; set; }
    public string? UwpPortBy { get; set; }
    public string? MaintainedBy { get; set; }
    public string? DownloadUrl { get; set; }
    public string? SourceUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Compatibility { get; set; } = string.Empty;
    public List<string> Requirements { get; set; } = [];
    public List<string> Features { get; set; } = [];
    public bool IsExperimental { get; set; }

    // New fields from JSON API
    public string? Url { get; set; }
    public string? SourceCodeUrl { get; set; }
    public string? SetupGuideUrl { get; set; }
    public string? TutorialUrl { get; set; }
    public string? ReleaseNotesUrl { get; set; }
    public List<Contributor> Contributors { get; set; } = [];
    public List<DownloadAsset> Downloads { get; set; } = [];

    // Computed properties for UI visibility
    [JsonIgnore]
    public bool IsWindowsTool => (DownloadUrl?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ?? false)
                              || Downloads.Any(d => d.Url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

    [JsonIgnore]
    public bool HasContributors => Contributors.Count > 0;

    [JsonIgnore]
    public bool HasLinks => !string.IsNullOrEmpty(SetupGuideUrl)
                         || !string.IsNullOrEmpty(TutorialUrl)
                         || !string.IsNullOrEmpty(ReleaseNotesUrl)
                         || !string.IsNullOrEmpty(SourceCodeUrl)
                         || !string.IsNullOrEmpty(Url);

    [JsonIgnore]
    public bool HasMultipleDownloads => Downloads.Count > 1;

    [JsonIgnore]
    public DownloadAsset? MainDownload => Downloads.FirstOrDefault(d => d.DownloadType == DownloadType.MainPackage)
                                       ?? Downloads.FirstOrDefault();

    [JsonIgnore]
    public List<DownloadAsset> DependencyDownloads => Downloads
        .Where(d => d.DownloadType == DownloadType.Dependency)
        .ToList();

    [ObservableProperty]
    [property: JsonIgnore]
    [NotifyPropertyChangedFor(nameof(IsThumbnailLoading))]
    private Bitmap? _thumbnail;

    [JsonIgnore]
    public bool IsThumbnailLoading => Thumbnail is null;
}

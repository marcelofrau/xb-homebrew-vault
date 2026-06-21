using System.Collections.Generic;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XBVault.Models;

public partial class CatalogItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? ReleaseDate { get; set; }
    public string? Developer { get; set; }
    public string? UwpPortBy { get; set; }
    public string? DownloadUrl { get; set; }
    public string? SourceUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Compatibility { get; set; } = string.Empty;
    public List<string> Requirements { get; set; } = [];
    public List<string> Features { get; set; } = [];
    public bool IsExperimental { get; set; }

    [ObservableProperty]
    [property: JsonIgnore]
    [NotifyPropertyChangedFor(nameof(IsThumbnailLoading))]
    private Bitmap? _thumbnail;

    [JsonIgnore]
    public bool IsThumbnailLoading => Thumbnail is null;
}

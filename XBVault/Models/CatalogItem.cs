using System.Collections.Generic;

namespace XBVault.Models;

public class CatalogItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? ReleaseDate { get; set; }
    public string? Developer { get; set; }
    public string? DownloadUrl { get; set; }
    public string? SourceUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Compatibility { get; set; } = string.Empty;
    public List<string> Requirements { get; set; } = [];
    public List<string> Features { get; set; } = [];
    public bool IsExperimental { get; set; }
}

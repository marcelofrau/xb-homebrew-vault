using System;

namespace XBVault.Models;

public class InstalledPackage
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public long InstalledSizeBytes { get; set; }
    public DateTime? InstallDate { get; set; }
    public string? PackageFamilyName { get; set; }
}

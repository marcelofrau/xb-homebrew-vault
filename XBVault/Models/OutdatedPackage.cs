namespace XBVault.Models;

public sealed class OutdatedPackage
{
    public required InstalledPackage Installed { get; init; }
    public required CatalogItem Catalog { get; init; }
    public Version? InstalledVersion { get; init; }
    public Version? AvailableVersion { get; init; }
    public bool IsCompatible { get; init; } = true;
}

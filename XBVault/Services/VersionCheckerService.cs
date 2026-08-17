#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XBVault.Models;

namespace XBVault.Services;

public class VersionCheckerService
{
    private readonly PackageOverrideService _overrideService;
    private readonly UpdateVersionCache _cache;
    private IReadOnlyList<CatalogItem> _catalog = [];
    private string? _justUpdatedItemName;

    public VersionCheckerService(PackageOverrideService overrideService, UpdateVersionCache? cache = null)
    {
        _overrideService = overrideService;
        _cache = cache ?? new UpdateVersionCache();
    }

    public bool HasCatalog => _catalog.Count > 0;

    public void SetCatalog(IReadOnlyList<CatalogItem> catalog) => _catalog = catalog;

    public void MarkJustUpdated(string name) => _justUpdatedItemName = name;

    public OutdatedPackage? FindOutdated(InstalledPackage pkg, bool ignoreSuppression = false)
    {
        var (match, isOutdated) = FindCatalogMatch(pkg, ignoreSuppression);
        if (match is null || !isOutdated)
            return null;

        return new OutdatedPackage
        {
            Installed = pkg,
            Catalog = match,
            InstalledVersion = Version.TryParse(pkg.Version, out var iv) ? iv : null,
            AvailableVersion = Version.TryParse(match.Version, out var cv) ? cv : null,
            IsCompatible = true
        };
    }

    public (CatalogItem? match, bool isOutdated) FindCatalogMatch(InstalledPackage pkg, bool ignoreSuppression = false)
    {
        if (IsIgnoredForUpdates(pkg))
            return (null, false);

        var match = _catalog.FirstOrDefault(i => IsPackageMatch(i, pkg));
        if (match is null)
            return (null, false);

        var installedVer = pkg.Version ?? string.Empty;
        var catalogVer = match.Version ?? string.Empty;

        // suppress outdated for package just updated — persist to cache
        if (!ignoreSuppression &&
            _justUpdatedItemName is not null &&
            match.Name.Equals(_justUpdatedItemName, StringComparison.OrdinalIgnoreCase))
        {
            _justUpdatedItemName = null;
            _cache.RecordUpdate(match.Name, catalogVer, installedVer);
            return (match, false);
        }

        // persistent cache: same pair of versions = already synced (only for UI badge)
        if (!ignoreSuppression && _cache.TryGetSuppressed(match.Name, catalogVer, installedVer))
            return (match, false);

        var isOutdated = false;
        if (Version.TryParse(installedVer, out var installedV) &&
            Version.TryParse(catalogVer, out var catalogV))
        {
            isOutdated = catalogV > installedV;
        }

        return (match, isOutdated);
    }

    public void RecordUpdate(CatalogItem catalog, InstalledPackage installed)
        => _cache.RecordUpdate(catalog.Name, catalog.Version ?? string.Empty, installed.Version ?? string.Empty);

    public bool IsPackageMatch(CatalogItem catalog, InstalledPackage pkg)
    {
        // E0: Exact matches (highest confidence)
        if (catalog.Name.Equals(pkg.Name, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(pkg.DisplayName) && catalog.Name.Equals(pkg.DisplayName, StringComparison.OrdinalIgnoreCase))
            return true;

        var pfn = !string.IsNullOrEmpty(pkg.PackageFamilyName) ? StripPackageFamilyName(pkg.PackageFamilyName) : null;

        if (pfn is not null && catalog.Name.Equals(pfn, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(catalog.AppId) && pkg.Name.Contains(catalog.AppId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(catalog.Id) && pkg.Name.Contains(catalog.Id, StringComparison.OrdinalIgnoreCase))
            return true;

        // E1: Alphanumeric normalization — strip non-alnum, lowercase
        // Handles "Super Mario Bros Remastered" vs "SuperMarioBrosRemastered" (spaces)
        var catNorm = NormalizeAlnum(catalog.Name);
        if (pfn is not null && catNorm.Equals(NormalizeAlnum(pfn), StringComparison.OrdinalIgnoreCase))
            return true;

        // E2: Download URL filename contains package Name or DisplayName
        // Handles SMBR: downloadUrl "SMBR_1.2.zip" contains Name "SMBR"
        if (DownloadUrlContains(pkg.Name, catalog) ||
            (!string.IsNullOrEmpty(pkg.DisplayName) && DownloadUrlContains(pkg.DisplayName, catalog)))
            return true;

        // E3: Reverse — catalog.AppId contains normalized PFN
        // Handles cases where PFN is abbreviation embedded in appId
        if (!string.IsNullOrEmpty(catalog.AppId) && pfn is not null &&
            catalog.AppId.Contains(pfn, StringComparison.OrdinalIgnoreCase))
            return true;

        // E4: Download URL filename first-token prefix of PFN (or vice versa), minimum 4 chars
        // Handles SMWRP: url firstToken "SMWR" is prefix of PFN "SMWRP"
        if (pfn is not null && DownloadTokenPrefixMatch(pfn, catalog))
            return true;

        // E5: Manual override table (final fallback — zero false positives)
        if (!string.IsNullOrEmpty(pfn) && _overrideService.TryGetCatalogId(pfn, out var overrideId))
        {
            if (catalog.Id.Equals(overrideId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (_overrideService.TryGetCatalogIdByName(pkg.Name, out var overrideIdByName))
        {
            if (catalog.Id.Equals(overrideIdByName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string StripPackageFamilyName(string familyName)
    {
        var idx = familyName.LastIndexOf('_');
        return idx > 0 ? familyName[..idx] : familyName;
    }

    private static bool IsIgnoredForUpdates(InstalledPackage pkg)
    {
        var pfn = pkg.PackageFamilyName;
        if (string.IsNullOrEmpty(pfn))
            return false;

        var ignored = SettingsService.Current?.IgnoredUpdatePackageFamilies;
        return ignored is { Count: > 0 } && ignored.Contains(pfn, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeAlnum(string value)
    {
        return System.Text.RegularExpressions.Regex.Replace(value, "[^a-zA-Z0-9]", "");
    }

    private static bool DownloadUrlContains(string value, CatalogItem catalog)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 3) return false;

        var urls = new[] { catalog.DownloadUrl }
            .Concat(catalog.Downloads.Select(d => d.Url))
            .Where(u => !string.IsNullOrEmpty(u))
            .Distinct();

        foreach (var url in urls)
        {
            var filename = Path.GetFileNameWithoutExtension(url);
            if (filename is not null && filename.Contains(value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool DownloadTokenPrefixMatch(string pfn, CatalogItem catalog)
    {
        var urls = new[] { catalog.DownloadUrl }
            .Concat(catalog.Downloads.Select(d => d.Url))
            .Where(u => !string.IsNullOrEmpty(u))
            .Distinct();

        foreach (var url in urls)
        {
            var filename = Path.GetFileNameWithoutExtension(url);
            if (string.IsNullOrEmpty(filename)) continue;

            var token = filename.Split('_')[0];
            if (token.Length < 4 && pfn.Length < 4) continue;

            if (pfn.StartsWith(token, StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith(pfn, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

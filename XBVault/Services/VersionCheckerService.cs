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

        var effectiveVer = GetEffectiveVersion(match, match.Version ?? string.Empty);
        return new OutdatedPackage
        {
            Installed = pkg,
            Catalog = match,
            InstalledVersion = Version.TryParse(pkg.Version, out var iv) ? iv : null,
            AvailableVersion = Version.TryParse(effectiveVer, out var cv) ? cv : null,
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
        var effectiveVer = GetEffectiveVersion(match, catalogVer);

        // suppress outdated for package just updated — persist to cache
        if (!ignoreSuppression &&
            _justUpdatedItemName is not null &&
            match.Name.Equals(_justUpdatedItemName, StringComparison.OrdinalIgnoreCase))
        {
            _justUpdatedItemName = null;
            _cache.RecordUpdate(match.Name, effectiveVer, installedVer);
            return (match, false);
        }

        // persistent cache: same pair of versions = already synced (only for UI badge)
        if (!ignoreSuppression && _cache.TryGetSuppressed(match.Name, effectiveVer, installedVer))
            return (match, false);

        var isOutdated = false;
        if (Version.TryParse(installedVer, out var installedV) &&
            Version.TryParse(effectiveVer, out var effectiveV))
        {
            isOutdated = effectiveV > installedV;
        }

        return (match, isOutdated);
    }

    public void RecordUpdate(CatalogItem catalog, InstalledPackage installed)
    {
        var catalogVer = catalog.Version ?? string.Empty;
        var effectiveVer = GetEffectiveVersion(catalog, catalogVer);
        _cache.RecordUpdate(catalog.Name, effectiveVer, installed.Version ?? string.Empty);
    }

    private string GetEffectiveVersion(CatalogItem catalog, string catalogVersion)
    {
        if (_overrideService.TryGetPackageVersion(catalog.Id, catalogVersion, out var overrideVer) &&
            !string.IsNullOrWhiteSpace(overrideVer))
        {
            return overrideVer;
        }
        return catalogVersion;
    }

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

        if (!string.IsNullOrEmpty(catalog.AppId) && catalog.AppId.Length * 2 >= pkg.Name.Length && ContainsAsWord(pkg.Name, catalog.AppId))
            return true;

        if (!string.IsNullOrEmpty(catalog.Id) && catalog.Id.Length * 2 >= pkg.Name.Length && ContainsAsWord(pkg.Name, catalog.Id))
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

        // E3: Reverse — catalog.AppId word-contains PFN
        // Handles cases where PFN is abbreviation embedded in appId (e.g. "sm64ex" in "sm64ex.uwp")
        // Uses word-boundary + ratio guard to prevent "recomp" matching "gen1recomp"
        if (!string.IsNullOrEmpty(catalog.AppId) && pfn is not null &&
            pfn.Length * 2 >= catalog.AppId.Length && ContainsAsWord(catalog.AppId, pfn))
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

    public static bool IsPackageMatchBasic(CatalogItem catalog, InstalledPackage pkg)
    {
        // E0: Exact matches (highest confidence)
        if (catalog.Name.Equals(pkg.Name, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(pkg.DisplayName) && catalog.Name.Equals(pkg.DisplayName, StringComparison.OrdinalIgnoreCase))
            return true;

        var pfn = !string.IsNullOrEmpty(pkg.PackageFamilyName) ? StripPackageFamilyName(pkg.PackageFamilyName) : null;

        if (pfn is not null && catalog.Name.Equals(pfn, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(catalog.AppId) && catalog.AppId.Length * 2 >= pkg.Name.Length && ContainsAsWord(pkg.Name, catalog.AppId))
            return true;

        if (!string.IsNullOrEmpty(catalog.Id) && catalog.Id.Length * 2 >= pkg.Name.Length && ContainsAsWord(pkg.Name, catalog.Id))
            return true;

        // E1: Alphanumeric normalization
        var catNorm = NormalizeAlnum(catalog.Name);
        if (pfn is not null && catNorm.Equals(NormalizeAlnum(pfn), StringComparison.OrdinalIgnoreCase))
            return true;

        // E2: Download URL filename contains package Name or DisplayName
        if (DownloadUrlContains(pkg.Name, catalog) ||
            (!string.IsNullOrEmpty(pkg.DisplayName) && DownloadUrlContains(pkg.DisplayName, catalog)))
            return true;

        // E3: Reverse — catalog.AppId word-contains PFN
        if (!string.IsNullOrEmpty(catalog.AppId) && pfn is not null &&
            pfn.Length * 2 >= catalog.AppId.Length && ContainsAsWord(catalog.AppId, pfn))
            return true;

        // E4: Download URL filename first-token prefix of PFN (or vice versa), minimum 4 chars
        if (pfn is not null && DownloadTokenPrefixMatch(pfn, catalog))
            return true;

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
            if (string.IsNullOrEmpty(filename) || filename.Length < 3) continue;

            if (ContainsAsWord(filename, value) &&
                Math.Min(value.Length, filename.Length) >= Math.Max(value.Length, filename.Length) * 0.5)
                return true;
        }

        return false;
    }

    private static bool ContainsAsWord(string text, string word)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word)) return false;
        int idx = 0;
        while (idx <= text.Length - word.Length)
        {
            idx = text.IndexOf(word, idx, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            bool startOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            int endIdx = idx + word.Length;
            bool endOk = endIdx >= text.Length || !char.IsLetterOrDigit(text[endIdx]);
            if (startOk && endOk) return true;
            idx++;
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
            if (token.Length < 4 || pfn.Length < 4) continue;

            if (pfn.StartsWith(token, StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith(pfn, StringComparison.OrdinalIgnoreCase))
            {
                // Reject short prefix matches — "DOOM" prefix must not match "Doom64EXClassicUWP"
                var shorter = Math.Min(token.Length, pfn.Length);
                var longer = Math.Max(token.Length, pfn.Length);
                if (shorter >= longer * 0.5)
                    return true;
            }
        }

        return false;
    }
}

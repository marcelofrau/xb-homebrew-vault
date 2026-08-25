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

        // Override priority: manual mappings bypass algorithmic matching entirely
        var overrideMatch = FindOverrideMatch(pkg);
        var match = overrideMatch ?? _catalog.FirstOrDefault(i => IsPackageMatch(i, pkg));
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

    private CatalogItem? FindOverrideMatch(InstalledPackage pkg)
    {
        var pfn = !string.IsNullOrEmpty(pkg.PackageFamilyName) ? StripPackageFamilyName(pkg.PackageFamilyName) : null;

        if (pfn is not null && _overrideService.TryGetCatalogId(pfn, out var overrideId))
        {
            var match = _catalog.FirstOrDefault(c => c.Id.Equals(overrideId, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        if (_overrideService.TryGetCatalogIdByName(pkg.Name, out var overrideIdByName))
        {
            var match = _catalog.FirstOrDefault(c => c.Id.Equals(overrideIdByName, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        return null;
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

        // E0c: FullName contains catalog name (FullName format: "Name_Version_Arch__hash")
        var fullNameBase = !string.IsNullOrEmpty(pkg.FullName) ? pkg.FullName.Split('_', 2)[0] : null;
        if (!string.IsNullOrEmpty(fullNameBase) && catalog.Name.Equals(fullNameBase, StringComparison.OrdinalIgnoreCase))
            return true;

        // E0d: AppId/Id word-contains — require Id shorter than pkg name
        // Prevents generic id "castlevania" (for Castlevania Revamped) from matching pkg "Castlevania"
        if (!string.IsNullOrEmpty(catalog.AppId) && catalog.AppId.Length < pkg.Name.Length && ContainsAsWord(pkg.Name, catalog.AppId))
            return true;

        if (!string.IsNullOrEmpty(catalog.Id) && catalog.Id.Length < pkg.Name.Length && ContainsAsWord(pkg.Name, catalog.Id))
            return true;

        // E1: Alphanumeric normalization — strip non-alnum, lowercase
        // Handles "Super Mario Bros Remastered" vs "SuperMarioBrosRemastered" (spaces)
        var catNorm = NormalizeAlnum(catalog.Name);
        var pkgNameNorm = NormalizeAlnum(pkg.Name);
        var pkgDisplayNorm = !string.IsNullOrEmpty(pkg.DisplayName) ? NormalizeAlnum(pkg.DisplayName) : null;

        if (catNorm.Equals(pkgNameNorm, StringComparison.OrdinalIgnoreCase))
            return true;

        if (pkgDisplayNorm is not null && catNorm.Equals(pkgDisplayNorm, StringComparison.OrdinalIgnoreCase))
            return true;

        if (pfn is not null && catNorm.Equals(NormalizeAlnum(pfn), StringComparison.OrdinalIgnoreCase))
            return true;

        // E1.1: Normalized prefix/containment for Name/DisplayName
        // SAFE direction only: pkgNameNorm startsWith catNorm (pkg has extra generic text like "Emulator", "UWP")
        // e.g. "DolphinEmulator" startsWith "Dolphin", "ScummVMUWPFrontend" startsWith "ScummVM"
        // REJECTED: catNorm startsWith pkgNameNorm — "CastlevaniaRevamped" startsWith "Castlevania" is a different app
        var pfnNorm = pfn is not null ? NormalizeAlnum(pfn) : null;

        if (catNorm.Length >= 4 && pkgNameNorm.Length >= 4)
        {
            if (StartsWithNorm(pkgNameNorm, catNorm))
                return true;
        }

        if (pkgDisplayNorm is not null && catNorm.Length >= 4 && pkgDisplayNorm.Length >= 4)
        {
            if (StartsWithNorm(pkgDisplayNorm, catNorm))
                return true;
            // Also allow containment when DisplayName is longer (e.g. "SpaceCadetPinballUWP" contains "SpaceCadetPinball")
            if (ContainsWithRatio(pkgDisplayNorm, catNorm, 0.55))
                return true;
        }

        // E1.1p: PFN prefix — bidirectional, PFN is authoritative system identifier
        // "Sonic1" prefix of "Sonic1Decompilation" (catalog has extra "Decompilation")
        // "ScummVMFrontend" startsWith "ScummVM" (catalog is core name)
        if (pfnNorm is not null && catNorm.Length >= 4 && pfnNorm.Length >= 4)
        {
            if (StartsWithNorm(catNorm, pfnNorm) || StartsWithNorm(pfnNorm, catNorm))
                return true;
            if (ContainsWithRatio(catNorm, pfnNorm, 0.55))
                return true;
        }

        // E1.1f: FullName base prefix — safe direction only (pkg has extra text)
        if (!string.IsNullOrEmpty(fullNameBase))
        {
            var fullNameBaseNorm = NormalizeAlnum(fullNameBase);
            if (fullNameBaseNorm.Length >= 4 && StartsWithNorm(fullNameBaseNorm, catNorm))
                return true;
        }

        // E1.2: Strip common suffixes (UWP, Frontend, PC) then recheck E1
        // Handles "dxx_rebirth_uwp" vs "DXX Rebirth", "Space Cadet Pinball UWP" vs "SpaceCadetPinball"
        var pkgNameStripped = StripCommonSuffixes(pkg.Name);
        var pkgDisplayStripped = !string.IsNullOrEmpty(pkg.DisplayName) ? StripCommonSuffixes(pkg.DisplayName) : null;
        var pfnStripped = pfn is not null ? StripCommonSuffixes(pfn) : null;

        if (!ReferenceEquals(pkgNameStripped, pkg.Name))
        {
            var stripped = NormalizeAlnum(pkgNameStripped);
            if (catNorm.Equals(stripped, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (pkgDisplayStripped is not null && !ReferenceEquals(pkgDisplayStripped, pkg.DisplayName))
        {
            var stripped = NormalizeAlnum(pkgDisplayStripped);
            if (catNorm.Equals(stripped, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (pfnStripped is not null && !ReferenceEquals(pfnStripped, pfn))
        {
            var stripped = NormalizeAlnum(pfnStripped);
            if (catNorm.Equals(stripped, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // E2: Download URL filename contains package Name or DisplayName
        // Handles SMBR: downloadUrl "SMBR_1.2.zip" contains Name "SMBR"
        // DisplayName requires >=5 chars to avoid "UWP" matching unrelated URLs (e.g. CVR-UWP-1.0.0.0.zip)
        if (DownloadUrlContains(pkg.Name, catalog) ||
            (!string.IsNullOrEmpty(pkg.DisplayName) && pkg.DisplayName.Length >= 5 && DownloadUrlContains(pkg.DisplayName, catalog)))
            return true;

        // E2.1: Download URL filename first-token starts with package Name (prefix match)
        // Handles SRB2: URL token "SRB2SDL2" startsWith "SRB2"
        if (DownloadFilenameTokenStartsWith(pkg.Name, catalog))
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

        if (!string.IsNullOrEmpty(catalog.AppId) && catalog.AppId.Length < pkg.Name.Length && ContainsAsWord(pkg.Name, catalog.AppId))
            return true;

        if (!string.IsNullOrEmpty(catalog.Id) && catalog.Id.Length < pkg.Name.Length && ContainsAsWord(pkg.Name, catalog.Id))
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

    private static string StripCommonSuffixes(string value)
    {
        const string UWP = "UWP";
        const string Frontend = "Frontend";
        const string PC = "PC";
        var result = value;
        if (result.EndsWith(UWP, StringComparison.OrdinalIgnoreCase) && result.Length > UWP.Length + 2)
            result = result[..^UWP.Length].TrimEnd();
        else if (result.EndsWith(Frontend, StringComparison.OrdinalIgnoreCase) && result.Length > Frontend.Length + 2)
            result = result[..^Frontend.Length].TrimEnd();
        else if (result.EndsWith(PC, StringComparison.OrdinalIgnoreCase) && result.Length > PC.Length + 2)
            result = result[..^PC.Length].TrimEnd();
        return result;
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

    private static bool StartsWithNorm(string text, string prefix)
    {
        return text.Length >= prefix.Length &&
               text.AsSpan(0, prefix.Length).Equals(prefix.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsWithRatio(string text, string contained, double minRatio)
    {
        if (!text.Contains(contained, StringComparison.OrdinalIgnoreCase))
            return false;
        return contained.Length >= text.Length * minRatio;
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

            if (!ContainsAsWord(filename, value))
                continue;

            // Short names (<=5 chars) that pass word boundary are reliable enough
            if (value.Length <= 5)
                return true;

            if (Math.Min(value.Length, filename.Length) >= Math.Max(value.Length, filename.Length) * 0.5)
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

    private static bool DownloadFilenameTokenStartsWith(string pkgName, CatalogItem catalog)
    {
        if (string.IsNullOrEmpty(pkgName) || pkgName.Length < 3) return false;

        var urls = new[] { catalog.DownloadUrl }
            .Concat(catalog.Downloads.Select(d => d.Url))
            .Where(u => !string.IsNullOrEmpty(u))
            .Distinct();

        foreach (var url in urls)
        {
            var filename = Path.GetFileNameWithoutExtension(url);
            if (string.IsNullOrEmpty(filename)) continue;

            var token = filename.Split('_')[0];
            if (token.Length < pkgName.Length) continue;

            if (token.StartsWith(pkgName, StringComparison.OrdinalIgnoreCase))
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

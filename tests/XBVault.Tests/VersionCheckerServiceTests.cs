using XBVault.Models;
using XBVault.Services;

namespace XBVault.Tests;

public class VersionCheckerServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _cachePath;

    public VersionCheckerServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "xbvault-tests", Guid.NewGuid().ToString("N"));
        _cachePath = Path.Combine(_dir, "update-versions.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private VersionCheckerService CreateService(List<CatalogItem> catalog)
    {
        using var overrideService = new PackageOverrideService();
        var checker = new VersionCheckerService(overrideService, new UpdateVersionCache(Path.Combine(_dir, "cache.json")));
        checker.SetCatalog(catalog);
        return checker;
    }

    private VersionCheckerService CreateServiceWithVersionOverrides(List<CatalogItem> catalog, string overridesJson)
    {
        var overrideService = new PackageOverrideService();
        overrideService.ParseAndMerge(overridesJson);
        var checker = new VersionCheckerService(overrideService, new UpdateVersionCache(Path.Combine(_dir, "cache.json")));
        checker.SetCatalog(catalog);
        return checker;
    }

    private static CatalogItem Item(string name, string version, string? appId = null, string? downloadUrl = null)
        => new()
        {
            Id = $"catalog-{name}",
            AppId = appId,
            Name = name,
            Version = version,
            DownloadUrl = downloadUrl,
            Category = "Games"
        };

    private static InstalledPackage Pkg(string name, string version, string? pfn = null)
        => new()
        {
            Name = name,
            Version = version,
            FullName = $"{name}_1.0.0.0_neutral__8wekyb3d8bbwe",
            PackageFamilyName = pfn ?? $"{name}_8wekyb3d8bbwe"
        };

    [Fact]
    public void FindOutdated_CatalogNewer_ReturnsOutdated()
    {
        var checker = CreateService([Item("Sonic", "1.2.0")]);

        var result = checker.FindOutdated(Pkg("sonic", "1.0.0"));

        Assert.NotNull(result);
        Assert.Equal("Sonic", result!.Catalog.Name);
        Assert.Equal(1, result.InstalledVersion!.Major);
        Assert.Equal(1, result.AvailableVersion!.Major);
        Assert.True(result.IsCompatible);
    }

    [Fact]
    public void FindOutdated_SameVersion_ReturnsNull()
    {
        var checker = CreateService([Item("Sonic", "1.0.0")]);

        Assert.Null(checker.FindOutdated(Pkg("sonic", "1.0.0")));
    }

    [Fact]
    public void FindOutdated_InstalledNewer_ReturnsNull()
    {
        var checker = CreateService([Item("Sonic", "1.0.0")]);

        Assert.Null(checker.FindOutdated(Pkg("sonic", "1.2.0")));
    }

    [Fact]
    public void FindOutdated_NoCatalogMatch_ReturnsNull()
    {
        var checker = CreateService([Item("Mario", "1.0.0")]);

        Assert.Null(checker.FindOutdated(Pkg("sonic", "0.5.0")));
    }

    [Fact]
    public void FindOutdated_UnparseableVersion_ReturnsNull()
    {
        var checker = CreateService([Item("Sonic", "latest")]);

        Assert.Null(checker.FindOutdated(Pkg("sonic", "1.0.0")));
    }

    [Fact]
    public void FindOutdated_IgnoredFamily_ReturnsNull()
    {
        var checker = CreateService([Item("Sonic", "1.2.0")]);
        var pkg = Pkg("sonic", "1.0.0");
        SettingsService.Current.IgnoredUpdatePackageFamilies.Add(pkg.PackageFamilyName!);

        try
        {
            Assert.Null(checker.FindOutdated(pkg));

            var (match, isOutdated) = checker.FindCatalogMatch(pkg);
            Assert.Null(match);
            Assert.False(isOutdated);
        }
        finally
        {
            SettingsService.Current.IgnoredUpdatePackageFamilies.Remove(pkg.PackageFamilyName!);
        }
    }

    [Fact]
    public void FindOutdated_OtherFamilyIgnored_StillDetectsCurrent()
    {
        var checker = CreateService([Item("Sonic", "1.2.0")]);
        var pkg = Pkg("sonic", "1.0.0");
        SettingsService.Current.IgnoredUpdatePackageFamilies.Add("other_8wekyb3d8bbwe");

        try
        {
            Assert.NotNull(checker.FindOutdated(pkg));
        }
        finally
        {
            SettingsService.Current.IgnoredUpdatePackageFamilies.Remove("other_8wekyb3d8bbwe");
        }
    }

    [Fact]
    public void FindOutdated_AfterRecordUpdate_OutdatedPairNotSuppressed()
    {
        var checker = CreateService([Item("Sonic", "1.2.0")]);
        var pkg = Pkg("sonic", "1.0.0");

        Assert.NotNull(checker.FindOutdated(pkg));

        checker.RecordUpdate(checker.FindCatalogMatch(pkg).match!, pkg);
        Assert.NotNull(checker.FindOutdated(pkg));
    }

    [Fact]
    public void FindOutdated_AfterRecordUpdate_UpToDatePairSuppressed()
    {
        var checker = CreateService([Item("Sonic", "1.2.0")]);
        var pkg = Pkg("sonic", "1.2.0");

        checker.RecordUpdate(checker.FindCatalogMatch(pkg).match!, pkg);
        Assert.Null(checker.FindOutdated(pkg));
    }

    [Fact]
    public void FindOutdated_JustUpdated_MarksPairRecorded()
    {
        var checker = CreateService([Item("Sonic", "1.2.0")]);
        var pkg = Pkg("sonic", "1.0.0");
        checker.MarkJustUpdated("Sonic");

        Assert.Null(checker.FindOutdated(pkg));

        var (match, _) = checker.FindCatalogMatch(pkg);
        Assert.NotNull(match);
    }

    [Fact]
    public void FindCatalogMatch_EmptyCatalog_ReturnsNoMatch()
    {
        var checker = CreateService([]);

        var (match, isOutdated) = checker.FindCatalogMatch(Pkg("sonic", "1.0.0"));

        Assert.Null(match);
        Assert.False(isOutdated);
    }

    [Fact]
    public void HasCatalog_FalseUntilSet()
    {
        using var overrideService = new PackageOverrideService();
        var checker = new VersionCheckerService(overrideService, new UpdateVersionCache(Path.Combine(_dir, "cache.json")));

        Assert.False(checker.HasCatalog);
        checker.SetCatalog([Item("Sonic", "1.0.0")]);
        Assert.True(checker.HasCatalog);
    }

    [Fact]
    public void FindOutdated_IgnoreSuppression_ReturnsOutdatedAfterRecordUpdate()
    {
        var checker = CreateService([Item("Sonic", "1.2.0")]);
        var pkg = Pkg("sonic", "1.0.0");

        // out-of-date pair: cache must NOT suppress even after RecordUpdate
        checker.RecordUpdate(checker.FindCatalogMatch(pkg).match!, pkg);
        Assert.NotNull(checker.FindOutdated(pkg));
    }

    [Fact]
    public void FindCatalogMatch_IgnoreSuppression_IgnoresJustUpdated()
    {
        var checker = CreateService([Item("Sonic", "1.2.0")]);
        var pkg = Pkg("sonic", "1.0.0");
        checker.MarkJustUpdated("Sonic");

        var (match, isOutdated) = checker.FindCatalogMatch(pkg);
        Assert.NotNull(match);
        Assert.False(isOutdated);

        var (_, rawOutdated) = checker.FindCatalogMatch(pkg, ignoreSuppression: true);
        Assert.True(rawOutdated);
    }

    [Fact]
    public void FindOutdated_IgnoreSuppression_DoesNotRecordCache()
    {
        var checker = CreateService([Item("Sonic", "1.2.0")]);
        var pkg = Pkg("sonic", "1.0.0");

        var result = checker.FindOutdated(pkg, ignoreSuppression: true);

        Assert.NotNull(result);
        Assert.NotNull(checker.FindOutdated(pkg));
    }

    [Fact]
    public void FindOutdated_VersionOverride_SamePackageVersion_NotOutdated()
    {
        var overrides = """{ "VersionOverrides": [{ "CatalogId": "catalog-sonic", "CatalogVersion": "2.9.2", "PackageVersion": "2.9.0.2" }] }""";
        var checker = CreateServiceWithVersionOverrides(
            [Item("sonic", "2.9.2", appId: "catalog-sonic")], overrides);

        var pkg = Pkg("sonic", "2.9.0.2");
        Assert.Null(checker.FindOutdated(pkg));
    }

    [Fact]
    public void FindOutdated_VersionOverride_NewerPackageVersion_Outdated()
    {
        var overrides = """{ "VersionOverrides": [{ "CatalogId": "catalog-sonic", "CatalogVersion": "2.9.2", "PackageVersion": "2.9.0.2" }] }""";
        var checker = CreateServiceWithVersionOverrides(
            [Item("sonic", "2.9.2", appId: "catalog-sonic")], overrides);

        var pkg = Pkg("sonic", "2.9.0.1");
        Assert.NotNull(checker.FindOutdated(pkg));
    }

    [Fact]
    public void FindOutdated_VersionOverride_DifferentCatalogVersion_NoMatch()
    {
        var overrides = """{ "VersionOverrides": [{ "CatalogId": "catalog-sonic", "CatalogVersion": "2.9.2", "PackageVersion": "2.9.0.2" }] }""";
        var checker = CreateServiceWithVersionOverrides(
            [Item("sonic", "3.0.0", appId: "catalog-sonic")], overrides);

        var pkg = Pkg("sonic", "2.9.0.2");
        Assert.NotNull(checker.FindOutdated(pkg));
    }

    [Fact]
    public void FindOutdated_NoOverride_FallsBackToCatalogVersion()
    {
        var overrides = """{ "VersionOverrides": [] }""";
        var checker = CreateServiceWithVersionOverrides(
            [Item("sonic", "1.2.0", appId: "catalog-sonic")], overrides);

        var pkg = Pkg("sonic", "1.0.0");
        Assert.NotNull(checker.FindOutdated(pkg));
    }

    [Fact]
    public void RecordUpdate_UsesOverrideVersionForCache()
    {
        var overrides = """{ "VersionOverrides": [{ "CatalogId": "catalog-sonic", "CatalogVersion": "2.9.2", "PackageVersion": "2.9.0.2" }] }""";
        var checker = CreateServiceWithVersionOverrides(
            [Item("sonic", "2.9.2", appId: "catalog-sonic")], overrides);

        var pkg = Pkg("sonic", "2.9.0.2");
        var match = checker.FindCatalogMatch(pkg).match!;
        checker.RecordUpdate(match, pkg);

        Assert.Null(checker.FindOutdated(pkg));
    }

    #region IsPackageMatch — matching rules E0–E4

    private static InstalledPackage PkgFull(string name, string version, string? displayName = null, string? pfn = null, string? fullName = null)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            Version = version,
            FullName = fullName ?? $"{name}_{version}_neutral__8wekyb3d8bbwe",
            PackageFamilyName = pfn ?? $"{name}_8wekyb3d8bbwe"
        };

    private static CatalogItem Cat(string name, string? id = null, string? appId = null, string? downloadUrl = null)
        => new()
        {
            Id = id ?? $"id-{name}",
            AppId = appId,
            Name = name,
            Version = "1.0.0",
            DownloadUrl = downloadUrl,
            Category = "Emulators"
        };

    private static bool Match(CatalogItem cat, InstalledPackage pkg)
    {
        using var os = new PackageOverrideService();
        var svc = new VersionCheckerService(os, new UpdateVersionCache(Path.Combine(Path.GetTempPath(), "t", Guid.NewGuid().ToString("N") + ".json")));
        svc.SetCatalog([cat]);
        return svc.IsPackageMatch(cat, pkg);
    }

    private static bool MatchWithOverrides(CatalogItem cat, InstalledPackage pkg, string overridesJson)
    {
        var os = new PackageOverrideService();
        os.ParseAndMerge(overridesJson);
        var svc = new VersionCheckerService(os, new UpdateVersionCache(Path.Combine(Path.GetTempPath(), "t", Guid.NewGuid().ToString("N") + ".json")));
        svc.SetCatalog([cat]);
        return svc.IsPackageMatch(cat, pkg);
    }

    // E0: exact name match
    [Theory]
    [InlineData("Dolphin", "Dolphin")]
    [InlineData("Sonic", "sonic")]
    [InlineData("RetroArch", "retroarch")]
    public void E0_ExactNameMatch(string catalogName, string pkgName)
        => Assert.True(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    // E0: exact DisplayName match
    [Fact]
    public void E0_ExactDisplayNameMatch()
        => Assert.True(Match(Cat("Dolphin"), PkgFull("Other", "1.0.0", displayName: "Dolphin")));

    // E0: exact PFN match
    [Fact]
    public void E0_ExactPFNMatch()
        => Assert.True(Match(Cat("RetroArch"), PkgFull("Libretro", "1.0.0", pfn: "RetroArch_8wekyb3d8bbwe")));

    // E0c: FullName base match
    [Fact]
    public void E0c_FullNameBaseMatch()
        => Assert.True(Match(
            Cat("Dolphin"),
            PkgFull("Other", "1.0.0", fullName: "Dolphin_1.1.9.0_x64__hash")));

    // E1: normalized equality
    [Theory]
    [InlineData("SpaceCadetPinball", "Space Cadet Pinball")]
    [InlineData("Maze0x72", "Maze 0x72")]
    [InlineData("SMBR", "S M B R")]
    [InlineData("SuperMarioBrosRemastered", "Super Mario Bros Remastered")]
    public void E1_NormalizedEquality(string catalogName, string pkgName)
        => Assert.True(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    // E1: normalized PFN match
    [Fact]
    public void E1_NormalizedPFNMatch()
        => Assert.True(Match(
            Cat("SuperMarioBrosRemastered"),
            PkgFull("Different", "1.0.0", pfn: "SuperMarioBrosRemastered_8wekyb3d8bbwe")));

    // E1.1: prefix — catalog name is prefix of pkg name (normalized)
    [Theory]
    [InlineData("Dolphin", "Dolphin Emulator")]
    [InlineData("ScummVM", "ScummVM UWP Frontend")]
    [InlineData("Flashback", "Flashback / REminiscence")]
    public void E11_CatalogNamePrefixOfPkgName(string catalogName, string pkgName)
        => Assert.True(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    // E1.1: prefix — pkg name is prefix of catalog name (normalized)
    [Fact]
    public void E11_PkgNamePrefixOfCatalogName()
        => Assert.True(Match(
            Cat("Sonic CD (Decompilation)"),
            PkgFull("Sonic-CD", "1.0.0")));

    // E1.1: prefix — catNorm starts with pfnNorm
    [Theory]
    [InlineData("Sonic 1 Decompilation", "Sonic1_8wekyb3d8bbwe")]
    [InlineData("Sonic 2 Decompilation", "Sonic2_8wekyb3d8bbwe")]
    public void E11_CatNamePrefixOfPFN(string catalogName, string pfn)
        => Assert.True(Match(Cat(catalogName), PkgFull("SomeGame", "1.0.0", pfn: pfn)));

    // E1.1: containment — shorter contained in longer (>=55%)
    [Fact]
    public void E11_ContainmentSpaceCadet()
        => Assert.True(Match(
            Cat("SpaceCadetPinball"),
            PkgFull("Space Cadet Pinball", "1.0.0", displayName: "Space Cadet Pinball UWP")));

    // E1.2: suffix stripping — UWP removed
    [Fact]
    public void E12_SuffixStripUWP()
        => Assert.True(Match(
            Cat("DXX Rebirth"),
            PkgFull("dxx_rebirth_uwp", "1.0.0")));

    // E1.2: suffix stripping — PC removed
    [Fact]
    public void E12_SuffixStripPC()
        => Assert.True(Match(
            Cat("Syphon Filter"),
            PkgFull("Syphon Filter PC", "1.0.0")));

    // E2: download URL filename contains name
    [Fact]
    public void E2_DownloadUrlContainsName()
        => Assert.True(Match(
            Cat("Castlevania Revamped", downloadUrl: "https://github.com/EmulationRevival/emulationrevival-downloads/releases/download/cvr-1.0.0.0/CVR-UWP-1.0.0.0.zip"),
            PkgFull("CVR", "1.0.0")));

    // E2: short name (<=5 chars) passes word boundary even with low ratio
    [Fact]
    public void E2_ShortNameWordBoundary()
        => Assert.True(Match(
            Cat("Test App", downloadUrl: "https://example.com/CVR_v1.zip"),
            PkgFull("CVR", "1.0.0")));

    // Real-world scenario: no match for completely different names
    [Fact]
    public void NoMatch_DifferentNames()
        => Assert.False(Match(Cat("Mario"), PkgFull("Sonic", "1.0.0")));

    // Real-world scenario: no match when catalog empty
    [Fact]
    public void NoMatch_EmptyCatalog()
        => Assert.False(Match(Cat("anything"), PkgFull("Sonic", "1.0.0")));

    #endregion

    #region IsPackageMatch — real-world Emulation Revival scenarios

    [Fact]
    public void Real_DolphinEmulator_MatchesDolphinCatalog()
    {
        var cat = Cat("Dolphin");
        var pkg = PkgFull("Dolphin Emulator", "1.1.9.0",
            displayName: "Dolphin",
            pfn: "3143e227-cbe5-41c4-aaa9-cf40132a1b22_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_dxx_rebirth_uwp_MatchesDXXRebirthCatalog()
    {
        var cat = Cat("DXX Rebirth");
        var pkg = PkgFull("dxx_rebirth_uwp", "1.0.3.0",
            displayName: "dxx_rebirth_uwp",
            pfn: "1494ce6e-5aa3-4283-8fbc-c85f5e7a1995_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_SonicTheHedgehog_MatchesSonic1Decompilation()
    {
        var cat = Cat("Sonic 1 Decompilation");
        var pkg = PkgFull("Sonic the Hedgehog", "1.0.9.0",
            displayName: "Sonic the Hedgehog",
            pfn: "Sonic1_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_SonicTheHedgehog2_MatchesSonic2Decompilation()
    {
        var cat = Cat("Sonic 2 Decompilation");
        var pkg = PkgFull("Sonic the Hedgehog 2", "1.0.9.0",
            displayName: "Sonic the Hedgehog 2",
            pfn: "Sonic2_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_SonicCD_MatchesSonicCDDecompilation()
    {
        var cat = Cat("Sonic CD (Decompilation)");
        var pkg = PkgFull("Sonic-CD", "1.0.21.0",
            displayName: "Sonic CD",
            pfn: "SONICCD_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_SpaceCadetPinball_MatchesCatalog()
    {
        var cat = Cat("SpaceCadetPinball");
        var pkg = PkgFull("Space Cadet Pinball", "1.0.1.33",
            displayName: "Space Cadet Pinball UWP",
            pfn: "Revive.SpaceCadetPinballUWP_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_SyphonFilterPC_MatchesSyphonFilterCatalog()
    {
        var cat = Cat("Syphon Filter");
        var pkg = PkgFull("Syphon Filter PC", "0.1.0.0",
            displayName: "Syphon Filter PC",
            pfn: "SyphonFilterUWP_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_ScummVM_MatchesScummVMFrontend()
    {
        var cat = Cat("ScummVM");
        var pkg = PkgFull("ScummVM UWP Frontend", "1.0.0.0",
            displayName: "ScummVM UWP Frontend",
            pfn: "ScummVMFrontend_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_Castlevania_DoesNotMatchRevamped()
    {
        var cat = Cat("Castlevania Revamped", id: "castlevania",
            downloadUrl: "https://github.com/EmulationRevival/emulationrevival-downloads/releases/download/cvr-1.0.0.0/CVR-UWP-1.0.0.0.zip");
        var pkg = PkgFull("Castlevania", "1.0.7.0",
            displayName: "Castlevania Simons Destiny (powered by GZDoom)",
            pfn: "CastlevaniaSimonsDestiny-powered-by-GZDOOM",
            fullName: "CastlevaniaSimonsDestiny-powered-by-GZDOOM_1.0.7.0_x64__5ac4zzwt665rw");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void Real_Castlevania_MatchesSimonsDestiny()
    {
        var cat = Cat("Castlevania: Simon's Destiny", id: "castlevania-simon",
            downloadUrl: "https://github.com/EmulationRevival/emulationrevival-downloads/releases/download/castlevania-simon-v1.0.7.0/CastlevaniaSimonsDestiny-powered-by-GZDOOM-v1.0.7.0.appx");
        var pkg = PkgFull("Castlevania", "1.0.7.0",
            displayName: "Castlevania Simons Destiny (powered by GZDoom)",
            pfn: "CastlevaniaSimonsDestiny-powered-by-GZDOOM",
            fullName: "CastlevaniaSimonsDestiny-powered-by-GZDOOM_1.0.7.0_x64__5ac4zzwt665rw");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_CVR_MatchesCastlevaniaRevampedViaDownloadUrl()
    {
        var cat = Cat("Castlevania Revamped",
            downloadUrl: "https://github.com/EmulationRevival/emulationrevival-downloads/releases/download/cvr-1.0.0.0/CVR-UWP-1.0.0.0.zip");
        var pkg = PkgFull("CVR", "1.0.0.0",
            displayName: "CVR",
            pfn: "9764a40a-6f56-46f7-a6fc-03762760f67a_8wekyb3d8bbwe",
            fullName: "CVR_1.0.0.0_x64__9764a40a-6f56-46f7-a6fc-03762760f67a");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_FlashBack_MatchesFlashbackREminiscence()
    {
        var cat = Cat("Flashback / REminiscence", id: "flashback-reminiscence");
        var pkg = PkgFull("FlashBack", "1.2.32.0",
            displayName: "UWP",
            pfn: "7800b501-ec0e-4d85-9d3b-f9499b529e37_8wekyb3d8bbwe");
        var overrides = """{ "packageNameOverrides": [{ "packageName": "FlashBack", "catalogId": "flashback-reminiscence" }] }""";
        Assert.True(MatchWithOverrides(cat, pkg, overrides));
    }

    [Fact]
    public void Real_Maze0x72_MatchesCatalog()
    {
        var cat = Cat("Maze0x72");
        var pkg = PkgFull("Maze 0x72", "1.0.1.0",
            displayName: "Maze 0x72",
            pfn: "2DBuiltInRenderer_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_DuckStation_MatchesCatalogByName()
    {
        var cat = Cat("DuckStation");
        var pkg = PkgFull("DuckStation", "1.0.0.0",
            displayName: "DuckStation",
            pfn: "57bcfd1f-31c1-4f8e-bf91-958732a81506_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    #endregion
}

using System.Text.Json;
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
        var pkg = PkgFull("REminiscenceUWP", "1.2.32.0",
            displayName: "REminiscence",
            pfn: "REminiscenceUWP_8wekyb3d8bbwe");
        var overrides = """{ "packageFamilyNameOverrides": [{ "packageFamilyName": "REminiscenceUWP", "catalogId": "flashback-reminiscence" }] }""";
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

    [Fact]
    public void Real_SRB2_MatchesViaDownloadUrlPrefix()
    {
        var cat = Cat("Sonic Robo Blast 2", id: "srb2");
        cat.Downloads = [new DownloadAsset { Url = "https://github.com/aerisarn/srb2-uwp/releases/download/1.0.213/SRB2SDL2_1.0.213.0.zip" }];
        var pkg = PkgFull("SRB2", "1.0.213.0",
            displayName: "SRB2",
            pfn: "115827d0-c6e7-4fcc-befe-a1de5c24c5d1_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_FlashBack_OverrideTakesPriorityOverAlgorithmicMatch()
    {
        var catRevamped = Cat("Castlevania Revamped", id: "castlevania");
        catRevamped.Downloads = [new DownloadAsset { Url = "https://github.com/EmulationRevival/emulationrevival-downloads/releases/download/cvr-1.0.0.0/CVR-UWP-1.0.0.0.zip" }];
        var catFlashback = Cat("Flashback / REminiscence", id: "flashback-reminiscence");
        var pkg = PkgFull("REminiscenceUWP", "1.2.32.0",
            displayName: "REminiscence",
            pfn: "REminiscenceUWP_8wekyb3d8bbwe");
        var overrides = """{ "packageFamilyNameOverrides": [{ "packageFamilyName": "REminiscenceUWP", "catalogId": "flashback-reminiscence" }] }""";
        var os = new PackageOverrideService();
        os.ParseAndMerge(overrides);
        var svc = new VersionCheckerService(os, new UpdateVersionCache(Path.Combine(Path.GetTempPath(), "t", Guid.NewGuid().ToString("N") + ".json")));
        svc.SetCatalog([catRevamped, catFlashback]);
        var (match, _) = svc.FindCatalogMatch(pkg);
        Assert.NotNull(match);
        Assert.Equal("flashback-reminiscence", match!.Id);
    }

    [Fact]
    public void Real_FlashBack_DoesNotMatchCastlevaniaRevamped()
    {
        var cat = Cat("Castlevania Revamped", id: "castlevania");
        cat.Downloads = [new DownloadAsset { Url = "https://github.com/EmulationRevival/emulationrevival-downloads/releases/download/cvr-1.0.0.0/CVR-UWP-1.0.0.0.zip" }];
        var pkg = PkgFull("REminiscenceUWP", "1.2.32.0",
            displayName: "REminiscence",
            pfn: "REminiscenceUWP_8wekyb3d8bbwe");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void E2_DownloadFilenamePrefixMatchesSRB2()
    {
        var cat = Cat("Sonic Robo Blast 2", id: "srb2");
        cat.Downloads = [new DownloadAsset { Url = "SRB2SDL2_1.0.213.0.zip" }];
        var pkg = PkgFull("SRB2", "1.0.213.0");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void E2_ShortDisplayName_DoesNotMatchUnrelatedUrl()
    {
        var cat = Cat("Castlevania Revamped");
        cat.Downloads = [new DownloadAsset { Url = "CVR-UWP-1.0.0.0.zip" }];
        var pkg = PkgFull("FlashBack", "1.2.32.0", displayName: "UWP");
        Assert.False(Match(cat, pkg));
    }

    #endregion

    #region IsPackageMatch — False positive prevention

    [Fact]
    public void FP_Zelda64Recomp_DoesNotMatchGen1Recomp()
    {
        var catGen1 = Cat("Gen1Recomp", id: "gen1recomp");
        var pkg = PkgFull("Zelda 64: Recompiled", "1.2.0.0",
            displayName: "Zelda 64: Recompiled",
            pfn: "recomp");
        Assert.False(Match(catGen1, pkg));
    }

    [Fact]
    public void FP_ShortPFN_Recomp_DoesNotMatchLongerCatalogContainment()
    {
        var cat = Cat("Gen1Recomp", id: "gen1recomp");
        var pkg = PkgFull("SomeApp", "1.0.0", pfn: "recomp");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void FP_ShortPFN_DoesNotMatchUnrelatedCatalog()
    {
        var cat = Cat("Starfox64Recomp", id: "starfox64recomp");
        var pkg = PkgFull("Zelda 64: Recompiled", "1.0.0", pfn: "recomp");
        Assert.False(Match(cat, pkg));
    }

    [Theory]
    [InlineData("Doom64EXClassicUWP", "DOOM")]
    [InlineData("HeXen II UWP", "HeXen")]
    public void FP_ShortCatalogName_MatchesViaFullNamePrefix(string pkgName, string catalogName)
        => Assert.True(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    [Fact]
    public void FP_Castlevania_DoesNotMatchCastlevaniaRevamped()
    {
        var cat = Cat("Castlevania Revamped",
            downloadUrl: "https://github.com/releases/download/cvr-1.0/CVR-UWP-1.0.zip");
        var pkg = PkgFull("Castlevania", "1.0.7.0",
            displayName: "Castlevania Simons Destiny (powered by GZDoom)",
            pfn: "CastlevaniaSimonsDestiny-powered-by-GZDOOM");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void FP_EmptyPFNDoesNotMatchAnything()
    {
        var cat = Cat("TestApp");
        var pkg = PkgFull("DifferentApp", "1.0.0", pfn: "");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void FP_NullPFNDoesNotMatchAnything()
    {
        var cat = Cat("TestApp");
        var pkg = PkgFull("DifferentApp", "1.0.0", pfn: null);
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void FP_CatalogIdAsSubstring_DoesNotMatchUnrelatedPackage()
    {
        var cat = Cat("Super App", id: "super");
        var pkg = PkgFull("Supercalifragilistic", "1.0.0");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void FP_AppIdSubstring_MatchesViaFullNamePrefix()
    {
        var cat = Cat("Doom", appId: "doom");
        var pkg = PkgFull("Doom64EXClassicUWP", "1.0.0");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void FP_CastlevaniaRevamped_DoesNotMatchFlashBack()
    {
        var cat = Cat("Castlevania Revamped", id: "castlevania");
        cat.Downloads = [new DownloadAsset { Url = "https://github.com/releases/download/cvr-1.0/CVR-UWP-1.0.zip" }];
        var pkg = PkgFull("FlashBack", "1.2.32.0",
            displayName: "UWP",
            pfn: "7800b501-ec0e-4d85-9d3b-f9499b529e37_8wekyb3d8bbwe");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void FP_CompletelyUnrelatedNames_NoMatch()
    {
        Assert.False(Match(Cat("Zelda"), PkgFull("Halo", "1.0.0")));
        Assert.False(Match(Cat("Minecraft"), PkgFull("Fortnite", "1.0.0")));
        Assert.False(Match(Cat("GTA"), PkgFull("Tetris", "1.0.0")));
    }

    [Fact]
    public void FP_SimilarButDifferentApps_NoMatch()
    {
        Assert.False(Match(Cat("Sonic 1 Decompilation"), PkgFull("Sonic 2", "1.0.0")));
        Assert.False(Match(Cat("Super Mario Bros"), PkgFull("Super Mario World", "1.0.0")));
    }

    [Fact]
    public void FP_ReversePrefixDirection_CatLongerThanPkg_NoMatch()
    {
        var cat = Cat("Sonic 1 Decompilation");
        var pkg = PkgFull("Sonic", "1.0.0");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void FP_ShortName_MatchesViaFullNamePrefix()
    {
        var cat = Cat("Pong");
        var pkg = PkgFull("Pong Clone Ultra", "1.0.0");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void FP_ShortName_NoMatchWhenFullNameDiffers()
    {
        var cat = Cat("Pong");
        var pkg = PkgFull("Different", "1.0.0", fullName: "SomethingElse_1.0.0_neutral__hash");
        Assert.False(Match(cat, pkg));
    }

    #endregion

    #region IsPackageMatch — Boundary conditions

    [Fact]
    public void Boundary_CatNameExactlyMinLength_Matches()
    {
        var cat = Cat("MyGame"); // 6 chars
        var pkg = PkgFull("MyGame", "1.0.0");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Boundary_PFNExactlyMinLength_PrefixMatchWorks()
    {
        var cat = Cat("Sonic1 Decompilation"); // 20 chars, normalized
        var pkg = PkgFull("SomeGame", "1.0.0", pfn: "Sonic1"); // 6 chars PFN
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Boundary_PFNBelowMinLength_NoPrefixMatch()
    {
        var cat = Cat("Sonic1 Decompilation");
        var pkg = PkgFull("SomeGame", "1.0.0", pfn: "Soni"); // 4 chars PFN — below 6
        Assert.False(Match(cat, pkg));
    }

    [Theory]
    [InlineData("Sonic1", "Sonic1Decompilation")]
    [InlineData("Sonic1Decompilation", "Sonic1")]
    public void Boundary_EqualLengthPrefix_BothDirections(string pfn, string catalogName)
        => Assert.True(Match(Cat(catalogName), PkgFull("SomeGame", "1.0.0", pfn: pfn)));

    [Fact]
    public void Boundary_CaseInsensitive_NameMatch()
    {
        Assert.True(Match(Cat("SONIC"), PkgFull("sonic", "1.0.0")));
        Assert.True(Match(Cat("sonic"), PkgFull("SONIC", "1.0.0")));
        Assert.True(Match(Cat("DoLpHiN"), PkgFull("dolphin", "1.0.0")));
    }

    [Fact]
    public void Boundary_CaseInsensitive_NormalizedMatch()
    {
        Assert.True(Match(Cat("SUPER MARIO BROS"), PkgFull("SuperMarioBros", "1.0.0")));
        Assert.True(Match(Cat("super_mario_bros"), PkgFull("SUPER MARIO BROS", "1.0.0")));
    }

    [Fact]
    public void Boundary_SpecialCharacters_AllStrippedInNormalization()
    {
        Assert.True(Match(Cat("Game-Title!"), PkgFull("Game Title", "1.0.0")));
        Assert.True(Match(Cat("App.Name"), PkgFull("App Name", "1.0.0")));
        Assert.True(Match(Cat("Test (UWP)"), PkgFull("Test UWP", "1.0.0")));
    }

    [Fact]
    public void Boundary_UnicodeStripped_DoesNotMatchExact()
    {
        Assert.False(Match(Cat("Café"), PkgFull("Different", "1.0.0")));
        Assert.False(Match(Cat("Ñoño"), PkgFull("Nino", "1.0.0")));
    }

    #endregion

    #region IsPackageMatch — Cross-catalog interaction (FindCatalogMatch)

    private static (CatalogItem? match, bool isOutdated) FindMatch(InstalledPackage pkg, params CatalogItem[] catalog)
    {
        using var os = new PackageOverrideService();
        var svc = new VersionCheckerService(os, new UpdateVersionCache(Path.Combine(Path.GetTempPath(), "t", Guid.NewGuid().ToString("N") + ".json")));
        svc.SetCatalog(catalog);
        return svc.FindCatalogMatch(pkg);
    }

    [Fact]
    public void CrossCatalog_Zelda64_MatchesCorrectCatalogItem()
    {
        var catGen1 = Cat("Gen1Recomp", id: "gen1recomp");
        var catZelda = Cat("Zelda 64 Recompiled", id: "zelda64recomp");
        var pkg = PkgFull("Zelda 64: Recompiled", "1.2.0.0",
            displayName: "Zelda 64: Recompiled",
            pfn: "recomp");
        var (match, _) = FindMatch(pkg, catGen1, catZelda);
        Assert.NotNull(match);
        Assert.Equal("zelda64recomp", match!.Id);
    }

    [Fact]
    public void CrossCatalog_Zelda64_NoFalseGen1Match()
    {
        var catGen1 = Cat("Gen1Recomp", id: "gen1recomp");
        var pkg = PkgFull("Zelda 64: Recompiled", "1.2.0.0",
            displayName: "Zelda 64: Recompiled",
            pfn: "recomp");
        var (match, _) = FindMatch(pkg, catGen1);
        Assert.Null(match);
    }

    [Fact]
    public void CrossCatalog_MultipleCompetingCatalogs_WinnerIsCorrect()
    {
        var cat1 = Cat("Sonic 1 Decompilation", id: "sonic1decomp");
        var cat2 = Cat("Sonic 2 Decompilation", id: "sonic2decomp");
        var cat3 = Cat("Sonic CD (Decompilation)", id: "soniccddecomp");
        var pkg = PkgFull("Sonic the Hedgehog 2", "1.0.9.0",
            displayName: "Sonic the Hedgehog 2",
            pfn: "Sonic2_8wekyb3d8bbwe");
        var (match, _) = FindMatch(pkg, cat1, cat2, cat3);
        Assert.NotNull(match);
        Assert.Equal("sonic2decomp", match!.Id);
    }

    [Fact]
    public void CrossCatalog_EmptyCatalog_NoMatch()
    {
        var pkg = PkgFull("Sonic", "1.0.0");
        var (match, _) = FindMatch(pkg);
        Assert.Null(match);
    }

    [Fact]
    public void CrossCatalog_SingleCatalog_ExactMatch()
    {
        var cat = Cat("Dolphin", id: "dolphin");
        var pkg = PkgFull("Dolphin Emulator", "1.1.9.0", displayName: "Dolphin");
        var (match, _) = FindMatch(pkg, cat);
        Assert.NotNull(match);
        Assert.Equal("dolphin", match!.Id);
    }

    #endregion

    #region IsPackageMatch — Real-world Emulation Revival edge cases

    [Fact]
    public void Real_Recomp_Packages_MatchTheirOwnCatalogs()
    {
        var catZelda = Cat("Zelda 64 Recompiled", id: "zelda64recomp");
        var catSf = Cat("Starfox 64 Recompiled", id: "starfox64recomp");
        var catSonic = Cat("Sonic Unleashed Recomp", id: "sonicunleashedrecomp");

        var pkgZelda = PkgFull("Zelda 64 Recompiled", "1.2.0.0", pfn: "recomp");
        var pkgSf = PkgFull("Starfox 64 Recompiled", "1.0.0.0", pfn: "recomp");
        var pkgSonic = PkgFull("Sonic Unleashed Recomp", "1.0.0.0", pfn: "recomp");

        Assert.True(Match(catZelda, pkgZelda));
        Assert.True(Match(catSf, pkgSf));
        Assert.True(Match(catSonic, pkgSonic));
    }

    [Fact]
    public void Real_RecompPackages_DontMatchEachOthersCatalogs()
    {
        var catZelda = Cat("Zelda 64 Recompiled", id: "zelda64recomp");
        var catGen1 = Cat("Gen1Recomp", id: "gen1recomp");

        var pkgZelda = PkgFull("Zelda 64 Recompiled", "1.2.0.0", pfn: "recomp");
        Assert.False(Match(catGen1, pkgZelda));
    }

    [Fact]
    public void Real_SpaceCadetPinballUWP_MatchesCatalog()
    {
        var cat = Cat("SpaceCadetPinball", id: "spacecadetpinball");
        var pkg = PkgFull("Space Cadet Pinball", "1.0.1.33",
            displayName: "Space Cadet Pinball UWP",
            pfn: "Revive.SpaceCadetPinballUWP_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_ScummVMFrontend_MatchesScummVMCatalog()
    {
        var cat = Cat("ScummVM");
        var pkg = PkgFull("ScummVM UWP Frontend", "1.0.0.0",
            pfn: "ScummVMFrontend_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_Sonic1PFNCatPrefix_Matches()
    {
        var cat = Cat("Sonic 1 Decompilation");
        var pkg = PkgFull("Sonic the Hedgehog", "1.0.9.0",
            pfn: "Sonic1_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_RecompCVR_MatchesCastlevaniaRevampedViaDownloadUrl()
    {
        var cat = Cat("Castlevania Revamped",
            downloadUrl: "https://github.com/EmulationRevival/emulationrevival-downloads/releases/download/cvr-1.0.0.0/CVR-UWP-1.0.0.0.zip");
        var pkg = PkgFull("CVR", "1.0.0.0",
            displayName: "CVR",
            pfn: "9764a40a-6f56-46f7-a6fc-03762760f67a_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Real_FlashBack_MatchesViaOverride()
    {
        var cat = Cat("Flashback / REminiscence", id: "flashback-reminiscence");
        var pkg = PkgFull("FlashBack", "1.2.32.0",
            displayName: "UWP",
            pfn: "7800b501-ec0e-4d85-9d3b-f9499b529e37_8wekyb3d8bbwe");
        var overrides = """{ "packageNameOverrides": [{ "packageName": "FlashBack", "catalogId": "flashback-reminiscence" }] }""";
        Assert.True(MatchWithOverrides(cat, pkg, overrides));
    }

    [Fact]
    public void Real_DuckStation_MatchesByName()
    {
        var cat = Cat("DuckStation");
        var pkg = PkgFull("DuckStation", "1.0.0.0");
        Assert.True(Match(cat, pkg));
    }

    #endregion

    #region IsPackageMatch — E0d AppId/Id word-contains

    [Fact]
    public void E0d_AppIdWordContains_RatioPreventsLongPkgMatch()
    {
        var cat = Cat("Test", appId: "doom");
        var pkg = PkgFull("Doom64EXClassicUWP", "1.0.0");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void E0d_AppIdWordContains_RatioPassesForShorterPkg()
    {
        var cat = Cat("Test", appId: "doom");
        var pkg = PkgFull("Doom Legacy", "1.0.0"); // "doom" (4) * 2 = 8 >= "doomlegacy" (10)? No
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void E0d_IdWordContains_Matches()
    {
        var cat = Cat("MyGame", id: "mygame");
        var pkg = PkgFull("MyGame Plus Edition", "1.0.0");
        Assert.True(Match(cat, pkg));
    }

    #endregion

    #region IsPackageMatch — E1.1f FullName base prefix

    [Fact]
    public void E11f_FullNameBasePrefix_Matches()
    {
        var cat = Cat("Dolphin");
        var pkg = PkgFull("Other", "1.0.0",
            fullName: "Dolphin_1.1.9.0_x64__hash");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void E11f_FullNameBase_ShortCatalogStillMatches()
    {
        var cat = Cat("AB");
        var pkg = PkgFull("Other", "1.0.0",
            fullName: "ABCD_1.0.0_neutral__hash");
        Assert.True(Match(cat, pkg));
    }

    #endregion

    #region IsPackageMatch — E1.2 Suffix stripping

    [Theory]
    [InlineData("DXX Rebirth", "dxx_rebirth_uwp")]
    [InlineData("Syphon Filter", "Syphon Filter PC")]
    [InlineData("SpaceCadetPinball", "Space Cadet Pinball UWP")]
    public void E12_SuffixStripping_Matches(string catalogName, string pkgName)
        => Assert.True(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    [Fact]
    public void E12_SuffixStripping_PFNStripped_Matches()
    {
        var cat = Cat("DXX Rebirth");
        var pkg = PkgFull("SomeName", "1.0.0",
            pfn: "DXX_Rebirth_UWP_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    #endregion

    #region IsPackageMatch — E2 Download URL

    [Fact]
    public void E2_DownloadUrlWordBoundary_ShortName()
    {
        var cat = Cat("Test", downloadUrl: "https://example.com/CVR_v1.zip");
        var pkg = PkgFull("CVR", "1.0.0");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void E2_DownloadUrlNoMatch_WrongName()
    {
        var cat = Cat("Unrelated", downloadUrl: "https://example.com/CVR_v1.zip");
        var pkg = PkgFull("FlashBack", "1.0.0", displayName: "UWP");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void E2_DownloadMultipleUrls_MatchesAnyUrl()
    {
        var cat = Cat("Test");
        cat.Downloads = [
            new DownloadAsset { Url = "https://example.com/first.zip" },
            new DownloadAsset { Url = "https://example.com/MyGame_v2.zip" }
        ];
        var pkg = PkgFull("MyGame", "1.0.0");
        Assert.True(Match(cat, pkg));
    }

    #endregion

    #region IsPackageMatch — E3 AppId word-contains PFN

    [Fact]
    public void E3_AppIdContainsPfnAsWord_Matches()
    {
        var cat = Cat("Test", appId: "sm64ex.uwp");
        var pkg = PkgFull("SomeGame", "1.0.0", pfn: "sm64ex");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void E3_AppIdContainsPfnNotAsWord_NoMatch()
    {
        var cat = Cat("Test", appId: "asm64exb");
        var pkg = PkgFull("SomeGame", "1.0.0", pfn: "sm64ex");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void E3_PfnTooShortRelToAppId_NoMatch()
    {
        var cat = Cat("Test", appId: "verylongappid");
        var pkg = PkgFull("SomeGame", "1.0.0", pfn: "x");
        Assert.False(Match(cat, pkg));
    }

    #endregion

    #region IsPackageMatch — E5 Override fallback

    [Fact]
    public void E5_OverridePfn_Matches()
    {
        var cat = Cat("Flashback / REminiscence", id: "flashback-reminiscence");
        var pkg = PkgFull("FlashBack", "1.2.32.0",
            displayName: "UWP",
            pfn: "7800b501-ec0e-4d85-9d3b-f9499b529e37_8wekyb3d8bbwe");
        var overrides = """{ "packageNameOverrides": [{ "packageName": "FlashBack", "catalogId": "flashback-reminiscence" }] }""";
        Assert.True(MatchWithOverrides(cat, pkg, overrides));
    }

    [Fact]
    public void E5_OverrideWrongCatalog_NoMatch()
    {
        var cat = Cat("Wrong Catalog", id: "wrong");
        var pkg = PkgFull("FlashBack", "1.2.32.0",
            pfn: "7800b501-ec0e-4d85-9d3b-f9499b529e37_8wekyb3d8bbwe");
        var overrides = """{ "packageNameOverrides": [{ "packageName": "FlashBack", "catalogId": "flashback-reminiscence" }] }""";
        Assert.False(MatchWithOverrides(cat, pkg, overrides));
    }

    #endregion

    #region IsPackageMatch — E1.1p PFN prefix (bidirectional)

    [Theory]
    [InlineData("Sonic1", "Sonic 1 Decompilation")]
    [InlineData("ScummVMFrontend", "ScummVM")]
    public void E11p_PFNPrefixedByCatalogName_Matches(string pfn, string catalogName)
        => Assert.True(Match(Cat(catalogName), PkgFull("SomeGame", "1.0.0", pfn: pfn)));

    [Theory]
    [InlineData("recomp", "Gen1Recomp")]
    [InlineData("recomp", "Starfox64Recomp")]
    public void E11p_ShortPFNSubstring_DoesNotMatch(string pfn, string catalogName)
        => Assert.False(Match(Cat(catalogName), PkgFull("SomeGame", "1.0.0", pfn: pfn)));

    [Fact]
    public void E11p_PFNPrefixedByCatalog_Matches()
    {
        var cat = Cat("Sonic 1 Decompilation");
        var pkg = PkgFull("SomeGame", "1.0.0", pfn: "Sonic1");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void E11p_CatalogPrefixedByPFN_Matches()
    {
        var cat = Cat("ScummVM");
        var pkg = PkgFull("SomeGame", "1.0.0", pfn: "ScummVMFrontend");
        Assert.True(Match(cat, pkg));
    }

    #endregion

    #region IsPackageMatch — Aggressive negative matrix (many false positives at once)

    [Fact]
    public void NegMatrix_NoPackageMatchesAllCatalogs()
    {
        var catalogs = new[]
        {
            Cat("Dolphin", id: "dolphin"),
            Cat("Sonic 1 Decompilation", id: "sonic1"),
            Cat("SpaceCadetPinball", id: "spacecadet"),
            Cat("Gen1Recomp", id: "gen1recomp"),
            Cat("Castlevania Revamped", id: "cvr"),
            Cat("ScummVM", id: "scummvm"),
            Cat("Maze0x72", id: "maze"),
            Cat("DuckStation", id: "duckstation"),
            Cat("Flashback / REminiscence", id: "flashback"),
            Cat("Sonic 2 Decompilation", id: "sonic2"),
        };

        var unrelated = PkgFull("RandomUnknownApp", "9.9.9", pfn: "totally_random_pfn");
        var (match, _) = FindMatch(unrelated, catalogs);
        Assert.Null(match);
    }

    [Fact]
    public void NegMatrix_Zelda64Recomp_OnlyMatchesZeldaCatalog()
    {
        var catalogs = new[]
        {
            Cat("Gen1Recomp", id: "gen1recomp"),
            Cat("Starfox 64 Recompiled", id: "starfox64recomp"),
            Cat("Sonic Unleashed Recomp", id: "sonicunleashedrecomp"),
            Cat("Zelda 64 Recompiled", id: "zelda64recomp"),
        };

        var pkg = PkgFull("Zelda 64: Recompiled", "1.2.0.0",
            displayName: "Zelda 64: Recompiled",
            pfn: "recomp");
        var (match, _) = FindMatch(pkg, catalogs);
        Assert.NotNull(match);
        Assert.Equal("zelda64recomp", match!.Id);
    }

    [Fact]
    public void NegMatrix_Sonic1_OnlyMatchesSonic1Catalog()
    {
        var catalogs = new[]
        {
            Cat("Sonic 2 Decompilation", id: "sonic2"),
            Cat("Sonic CD (Decompilation)", id: "soniccd"),
            Cat("Sonic 1 Decompilation", id: "sonic1"),
        };

        var pkg = PkgFull("Sonic the Hedgehog", "1.0.9.0",
            displayName: "Sonic the Hedgehog",
            pfn: "Sonic1_8wekyb3d8bbwe");
        var (match, _) = FindMatch(pkg, catalogs);
        Assert.NotNull(match);
        Assert.Equal("sonic1", match!.Id);
    }

    [Fact]
    public void NegMatrix_CastlevaniaDoesNotMatchRevamped()
    {
        var catalogs = new[]
        {
            Cat("Castlevania Revamped", id: "cvr",
                downloadUrl: "https://github.com/releases/download/cvr-1.0/CVR-UWP-1.0.zip"),
            Cat("Castlevania: Simon's Destiny", id: "castlevania-simon",
                downloadUrl: "https://github.com/releases/download/castlevania-simon/CastlevaniaSimonsDestiny.appx"),
        };

        var pkg = PkgFull("Castlevania", "1.0.7.0",
            displayName: "Castlevania Simons Destiny (powered by GZDoom)",
            pfn: "CastlevaniaSimonsDestiny-powered-by-GZDOOM");
        var (match, _) = FindMatch(pkg, catalogs);
        Assert.NotNull(match);
        Assert.Equal("castlevania-simon", match!.Id);
    }

    #endregion

    #region IsPackageMatch — E0d edge cases for Id/AppId word boundary

    [Theory]
    [InlineData("sm64ex", "sm64ex Plus Alpha")]
    [InlineData("mygame", "MyGame Plus Edition")]
    public void E0d_IdRatioCheckPreventsFalseMatch(string id, string pkgName)
        => Assert.False(Match(Cat("Test", id: id), PkgFull(pkgName, "1.0.0")));

    [Theory]
    [InlineData("doom", "Doom64EXClassicUWP")]
    [InlineData("son", "Sonic1")]
    public void E0d_IdTooShortVsPkgName_DoesNotMatch(string id, string pkgName)
        => Assert.False(Match(Cat("Test", id: id), PkgFull(pkgName, "1.0.0")));

    [Fact]
    public void E0d_AppIdExactRatioBoundary_NoMatch()
    {
        var cat = Cat("Test", appId: "abcde");
        var pkg = PkgFull("abcdefghij", "1.0.0");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void E0d_AppIdPassesRatioCheck_Matches()
    {
        var cat = Cat("Test", appId: "abcdefghij"); // 10 chars
        var pkg = PkgFull("abcdefghij Extra", "1.0.0"); // "abcdefghij" (10) * 2 = 20 >= 16? Yes
        Assert.True(Match(cat, pkg));
    }

    #endregion

    #region Override — real sweep mappings (manifestName -> catalogId)

    // These come from the catalog sweep (build/catalog-sweep.ps1): the real
    // package identity <Name> reported by the Xbox Device Portal as pkg.Name and
    // as the stripped PackageFamilyName. Each catalog title below does NOT match
    // its installed identity by any heuristic (E0-E4), so an explicit override is
    // the only reliable resolution. The override is keyed on manifestName.
    [Theory]
    [InlineData("2S2H (2Ship2Harkinian)", "ship2harkinian", "2ship2harkinian")]
    [InlineData("BLOOM", "bloom", "BLOOM-powered-by-GZDOOM")]
    [InlineData("DOOM", "doom", "DOOM-powered-by-GZDOOM")]
    [InlineData("HeXen", "hexen", "HeXen-powered-by-GZDOOM")]
    [InlineData("Wolfenstein 3D TC", "wolfenstein3d-tc", "Wolf3DTC-powered-by-GZDOOM")]
    [InlineData("Castlevania Revamped", "castlevania", "9764a40a-6f56-46f7-a6fc-03762760f67a")]
    [InlineData("ioquake3", "ioquake3", "ioq3-uwp")]
    [InlineData("Nazi Zombies: Portable", "nazi-zombies-portable", "nzportable.uwp")]
    [InlineData("Ruffle", "ruffle", "7800b501-ec0e-4d85-9d3b-f9499b529e37")]
    [InlineData("SOH (Ship of Harkinian)", "soh", "Shipwright")]
    [InlineData("Sonic CD (Decompilation)", "soniccd", "SONICCD")]
    [InlineData("Sonic Robo Blast 2", "srb2", "115827d0-c6e7-4fcc-befe-a1de5c24c5d1")]
    [InlineData("Sonic Unleashed Recomp", "sonicunleashed", "xenon-uwp")]
    [InlineData("Starfox 64: Recompiled", "starfox-64-recompiled", "sf64")]
    [InlineData("Tails Adventure Remake", "tails-adventure-remake", "tails-adventure-uwp")]
    [InlineData("VLC Media Player", "vlc", "VideoLAN.VLC")]
    [InlineData("Zelda 64 Recompiled", "zelda64", "recomp")]
    public void Override_RealManifestName_ResolvesCatalog(string catalogTitle, string catalogId, string manifestName)
    {
        var cat = Cat(catalogTitle, id: catalogId);
        var pkg = PkgFull(manifestName, "1.0.0.0", pfn: $"{manifestName}_8wekyb3d8bbwe");
        var overrides = JsonSerializer.Serialize(new
        {
            packageFamilyNameOverrides = new[] { new { packageFamilyName = manifestName, catalogId } }
        });
        Assert.True(MatchWithOverrides(cat, pkg, overrides));
    }

    #endregion

    #region IsPackageMatch — Synthetic positive pairs (arbitrary names that must match)

    [Theory]
    // E0 exact name
    [InlineData("RetroArch", "RetroArch")]
    [InlineData("mGBA", "mGBA")]
    [InlineData("PPSSPP", "PPSSPP")]
    [InlineData("DeSmuME", "DeSmuME")]
    // E0 exact name case-insensitive
    [InlineData("VBA-M", "vba-m")]
    [InlineData("Citra", "citra")]
    [InlineData("Yuzu", "YUZU")]
    // E0 DisplayName match
    [InlineData("Project64", "N64 Emulator")]
    [InlineData("Mupen64Plus", "Mupen")]
    [InlineData("Flycast", "Dreamcast")]
    // E0 PFN match
    [InlineData("Mednafen", "Mednafen")]
    [InlineData("Snes9x", "Snes9x")]
    // E0c FullName base match
    [InlineData("MAME", "SomeFrontend")]
    [InlineData("FBNeo", "ArcadeCab")]
    public void Synthetic_E0_Match(string catalogName, string pkgName)
        => Assert.True(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0", displayName: catalogName)));

    [Theory]
    // E1 normalized equality — spaces vs no spaces
    [InlineData("FinalBurn Neo", "FinalBurnNeo")]
    [InlineData("Cemu Emulator", "CemuEmulator")]
    [InlineData("PPSSPP Gold", "PPSSPPGold")]
    [InlineData("MAME4Droid", "MAME 4 Droid")]
    [InlineData("OpenBOR", "Open B O R")]
    [InlineData("Gasia Station", "GasiaStation")]
    // E1 — underscores and hyphens
    [InlineData("N64oid", "N64_oid")]
    [InlineData("MyEmulator", "My_Emulator")]
    [InlineData("GameBoid", "Game-Boid")]
    // E1 — mixed separators
    [InlineData("Mario Kart DS", "MarioKartDS")]
    [InlineData("Sonic Mega", "Sonic_Mega")]
    [InlineData("Wave Race", "Wave-Race")]
    public void Synthetic_E1_NormalizedEquality(string catalogName, string pkgName)
        => Assert.True(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    [Theory]
    // E1.1 prefix — pkg has extra generic suffix
    [InlineData("VisualBoy", "VisualBoyAdvance")]
    [InlineData("ePSXe", "ePSXeAndroid")]
    [InlineData("Gens", "GensPlus")]
    [InlineData("ZSNES", "ZSNESw")]
    [InlineData("Nester", "NesterDC")]
    [InlineData("Os9x", "Os9xPocket")]
    [InlineData("MAME", "MAME078")]
    [InlineData("FCEUX", "FCEUXMM")]
    public void Synthetic_E11_PrefixMatch(string catalogName, string pkgName)
        => Assert.True(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    [Theory]
    // E1.1 PFN prefix bidirectional — both must be >= 6 chars, one must be prefix of the other
    [InlineData("RetroArch", "RetroArchCore")]
    [InlineData("Sonic1Decomp", "Sonic1")]
    [InlineData("MGBAPlus", "MGBAPlusExtended")]
    public void Synthetic_E11p_PFNPrefixedByCatalog(string pfn, string catalogName)
        => Assert.True(Match(Cat(catalogName), PkgFull("SomeGame", "1.0.0", pfn: pfn, fullName: "SomeGame_1.0.0_neutral__hash")));

    [Theory]
    // E1.1 PFN prefix — catalog prefix of PFN
    [InlineData("ScummVM", "ScummVMFrontend")]
    [InlineData("Dolphin", "DolphinX")]
    public void Synthetic_E11p_CatalogPrefixOfPFN(string catalogName, string pfn)
        => Assert.True(Match(Cat(catalogName), PkgFull("SomeGame", "1.0.0", pfn: pfn)));

    [Theory]
    // E1.2 suffix stripping
    [InlineData("MyGame", "MyGame UWP")]
    [InlineData("CoolApp", "CoolApp PC")]
    [InlineData("RetroCore", "RetroCore Frontend")]
    [InlineData("EmuBoy", "EmuBoy uwp")]
    [InlineData("Pixel8", "Pixel8 pc")]
    [InlineData("NeoGeo", "NeoGeo frontend")]
    public void Synthetic_E12_SuffixStripping(string catalogName, string pkgName)
        => Assert.True(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    [Theory]
    // E1.2 PFN suffix stripping
    [InlineData("DXX Rebirth", "GameName", "DXX_Rebirth_UWP")]
    [InlineData("MyGame", "Other", "MyGame_UWP")]
    public void Synthetic_E12_PFNSuffixStripping(string catalogName, string pkgName, string pfn)
        => Assert.True(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0", pfn: pfn)));

    [Fact]
    public void Synthetic_E11f_FullNamePrefix_Matches()
    {
        var cat = Cat("RetroArch");
        var pkg = PkgFull("DifferentInternal", "1.0.0",
            fullName: "RetroArch_1.18.0_x64__hash");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Synthetic_E11f_FullNameNotPrefix_NoMatch()
    {
        var cat = Cat("Zelda");
        var pkg = PkgFull("DifferentInternal", "1.0.0",
            fullName: "SomethingElse_1.0.0_neutral__hash");
        Assert.False(Match(cat, pkg));
    }

    [Theory]
    // E0d AppId word-contains — ratio check: appId.Length * 2 >= pkgName.Length
    // Also requires word boundary match (ContainsAsWord)
    [InlineData("doom", "Doom")]
    [InlineData("doom", "Doom 64")]
    [InlineData("doom", "Doom Pro")]
    [InlineData("retroarch", "RetroArch")]
    [InlineData("dolphin", "Dolphin")]
    public void Synthetic_E0d_AppIdWordContains(string appId, string pkgName)
        => Assert.True(Match(Cat("Test", appId: appId), PkgFull(pkgName, "1.0.0")));

    [Theory]
    // E3 AppId word-contains PFN
    [InlineData("retroarch.uwp", "retroarch")]
    [InlineData("mgba.app", "mgba")]
    [InlineData("cemu.exe", "cemu")]
    public void Synthetic_E3_AppIdContainsPfn(string appId, string pfn)
        => Assert.True(Match(Cat("Test", appId: appId), PkgFull("AnyName", "1.0.0", pfn: pfn)));

    #endregion

    #region IsPackageMatch — Synthetic negative pairs (arbitrary names that must NOT match)

    [Theory]
    // Completely unrelated names
    [InlineData("Zelda", "Halo")]
    [InlineData("Minecraft", "Fortnite")]
    [InlineData("Doom", "Tetris")]
    [InlineData("Sonic", "Mario")]
    [InlineData("Pac-Man", "Galaga")]
    [InlineData("Contra", "Castlevania")]
    [InlineData("Metroid", "Megaman")]
    [InlineData("Mortal Kombat", "Street Fighter")]
    [InlineData("Gran Turismo", "Need for Speed")]
    [InlineData("Resident Evil", "Silent Hill")]
    public void Synthetic_CompletelyDifferent(string catalogName, string pkgName)
        => Assert.False(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    [Theory]
    // Same franchise, different game — must NOT match
    [InlineData("Sonic 1", "Sonic 2")]
    [InlineData("Sonic 1", "Sonic 3")]
    [InlineData("Sonic CD", "Sonic Mania")]
    [InlineData("Mario Bros", "Mario World")]
    [InlineData("Mario 64", "Mario Sunshine")]
    [InlineData("Zelda OoT", "Zelda MM")]
    [InlineData("Zelda OoT", "Zelda BotW")]
    [InlineData("Mega Man 1", "Mega Man 2")]
    [InlineData("Mega Man X", "Mega Man Zero")]
    [InlineData("FF7", "FF8")]
    [InlineData("FF7", "FF9")]
    [InlineData("Contra 1", "Contra 2")]
    [InlineData("Castlevania 1", "Castlevania 2")]
    [InlineData("Resident Evil 1", "Resident Evil 2")]
    [InlineData("Doom 1", "Doom 2")]
    public void Synthetic_SameFranchise_DifferentEntry(string catalogName, string pkgName)
        => Assert.False(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    [Theory]
    // Short catalog name vs long unrelated pkg name
    [InlineData("Pong", "Space Invaders")]
    [InlineData("Tron", "Discs of Tron Enhanced")]
    [InlineData("Centipede", "Millipede")]
    [InlineData("Frogger", "Snake")]
    [InlineData("Breakout", "Arkanoid")]
    public void Synthetic_ShortVsLong_Unrelated(string catalogName, string pkgName)
        => Assert.False(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    [Theory]
    // Substring that is NOT a word boundary — catalog name must NOT match via any rule
    // Note: these use custom fullName to avoid E1.1f FullName prefix matching
    [InlineData("Art", "Masterpiece")]
    [InlineData("Car", "Garage")]
    [InlineData("Bat", "Combat")]
    [InlineData("Pen", "Open")]
    [InlineData("Fan", "Refan")]
    public void Synthetic_SubstringNotWord(string catalogName, string pkgName)
        => Assert.False(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0", fullName: $"{pkgName}_1.0.0_neutral__hash")));

    [Theory]
    // Reverse prefix — catalog is longer than pkg, should not match via E1.1
    // Must use custom PFN to avoid E1.1p bidirectional prefix match
    [InlineData("Sonic the Hedgehog", "Sonic")]
    [InlineData("Super Mario Bros", "Mario")]
    [InlineData("The Legend of Zelda", "Zelda")]
    [InlineData("Final Fantasy VII", "Final")]
    [InlineData("Resident Evil", "Resident")]
    public void Synthetic_LongerCatalog_ShorterPkg(string catalogName, string pkgName)
        => Assert.False(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0", pfn: "DifferentPFN_8wekyb3d8bbwe")));

    [Theory]
    // Same initials / abbreviation coincidence
    [InlineData("GTA", "Game Time Adventures")]
    [InlineData("COD", "Crazy Orb Destruction")]
    [InlineData("FF", "Furry Fighters")]
    [InlineData("EG", "Electric Guitar")]
    [InlineData("TF", "Turbo Fox")]
    public void Synthetic_AbbreviationCoincidence(string catalogName, string pkgName)
        => Assert.False(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    [Theory]
    // Reversed containment — catalog contains pkg name but NOT as prefix
    [InlineData("Mega Drive Classics", "Drive")]
    [InlineData("Super Star Wars", "Star")]
    [InlineData("Ultra Street Fighter", "Street")]
    [InlineData("Grand Theft Auto", "Theft")]
    [InlineData("Half-Life 2", "Life")]
    public void Synthetic_CatalogContainsPkg_NotPrefix(string catalogName, string pkgName)
        => Assert.False(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    [Theory]
    // Same word different order
    [InlineData("Sonic Blast", "Blast Sonic")]
    [InlineData("Street Fighter", "Fighter Street")]
    [InlineData("Star Fox", "Fox Star")]
    [InlineData("Metal Gear", "Gear Metal")]
    [InlineData("Space Jam", "Jam Space")]
    public void Synthetic_WordsReversed(string catalogName, string pkgName)
        => Assert.False(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    [Theory]
    // One shared word + different other word
    [InlineData("Super Mario", "Super Smash")]
    [InlineData("Mega Man", "Mega Drive")]
    [InlineData("Star Wars", "Star Trek")]
    [InlineData("Sonic CD", "Sonic Rush")]
    [InlineData("Final Fantasy", "Final Countdown")]
    public void Synthetic_SharedWordButDifferent(string catalogName, string pkgName)
        => Assert.False(Match(Cat(catalogName), PkgFull(pkgName, "1.0.0")));

    [Fact]
    public void Synthetic_PFNUnrelatedToCatalog_NoMatch()
    {
        var cat = Cat("Zelda");
        var pkg = PkgFull("SomethingElse", "1.0.0", pfn: "totally_unrelated_pfn_123");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void Synthetic_BothEmptyNames_NoMatch()
    {
        var cat = Cat("Something");
        var pkg = PkgFull("SomethingElse", "1.0.0");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Synthetic_EmptyCatalogName_MatchesViaFullNamePrefix()
    {
        var cat = Cat("AB");
        var pkg = PkgFull("ABSomething", "1.0.0");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Synthetic_CatalogNameIsPartOfPFN_NotAsPrefix()
    {
        var cat = Cat("RetroArch");
        var pkg = PkgFull("Game", "1.0.0", pfn: "NotRetroArchHere_8wekyb3d8bbwe");
        Assert.False(Match(cat, pkg));
    }

    [Fact]
    public void Synthetic_PFNIsPartOfCatalogName_NotAsPrefix()
    {
        var cat = Cat("NotSonicHere");
        var pkg = PkgFull("Game", "1.0.0", pfn: "Sonic_8wekyb3d8bbwe");
        Assert.False(Match(cat, pkg));
    }

    #endregion

    #region IsPackageMatch — Synthetic adversarial edge cases

    [Fact]
    public void Adversarial_CatalogNameOnlyDifferByOneChar()
    {
        Assert.False(Match(Cat("RetroArchX"), PkgFull("RetroArch", "1.0.0", fullName: "RetroArch_1.0.0_neutral__hash", pfn: "DifferentPFN_8wekyb3d8bbwe")));
        Assert.False(Match(Cat("PPSSPPP"), PkgFull("PPSSPP", "1.0.0", fullName: "PPSSPP_1.0.0_neutral__hash", pfn: "DifferentPFN_8wekyb3d8bbwe")));
    }

    [Fact]
    public void Adversarial_CatalogNameIsReversedPkgName()
    {
        Assert.False(Match(Cat("ecnalabmiS"), PkgFull("SimBalancing", "1.0.0", fullName: "SimBalancing_1.0.0_neutral__hash")));
    }

    [Fact]
    public void Adversarial_VeryLongNames()
    {
        var longCat = new string('A', 200);
        var longPkg = new string('B', 200);
        Assert.False(Match(Cat(longCat), PkgFull(longPkg, "1.0.0")));
    }

    [Fact]
    public void Adversarial_WhitespaceVariations()
    {
        Assert.True(Match(Cat("Game Title"), PkgFull("GameTitle", "1.0.0")));
        Assert.True(Match(Cat("Game Title"), PkgFull("Game  Title", "1.0.0")));
        Assert.True(Match(Cat("Game Title"), PkgFull("Game Title ", "1.0.0")));
        Assert.True(Match(Cat("Game Title"), PkgFull(" GameTitle", "1.0.0")));
    }

    [Fact]
    public void Adversarial_NumbersInNames()
    {
        Assert.True(Match(Cat("Sonic 1"), PkgFull("Sonic1", "1.0.0")));
        Assert.True(Match(Cat("Mega Man X4"), PkgFull("MegaManX4", "1.0.0")));
        Assert.False(Match(Cat("Sonic 1"), PkgFull("Sonic 2", "1.0.0")));
        Assert.False(Match(Cat("FF7"), PkgFull("FF8", "1.0.0")));
    }

    [Fact]
    public void Adversarial_SpecialCharsInPFN()
    {
        var pkg1 = PkgFull("Game1", "1.0.0", pfn: "TestApp_8wekyb3d8bbwe", fullName: "Game1_1.0.0_neutral__hash");
        var pkg2 = PkgFull("Game2", "1.0.0", pfn: "NotTestApp_8wekyb3d8bbwe", fullName: "Game2_1.0.0_neutral__hash");
        Assert.True(Match(Cat("TestApp"), pkg1));
        Assert.False(Match(Cat("TestApp"), pkg2));
    }

    [Fact]
    public void Adversarial_PFNWithDots()
    {
        var cat = Cat("MyApp");
        var pkg = PkgFull("Game", "1.0.0", pfn: "My.App_8wekyb3d8bbwe", fullName: "Game_1.0.0_neutral__hash");
        Assert.True(Match(cat, pkg));
    }

    [Theory]
    // PFN "recomp" edge cases — should never match Gen1Recomp catalog
    [InlineData("Zelda 64: Recompiled")]
    [InlineData("Sonic Unleashed Recompiled")]
    [InlineData("Banjo-Kazooie Recompiled")]
    [InlineData("Perfect Dark Recompiled")]
    [InlineData("GoldenEye Recompiled")]
    public void Adversarial_RecompPFN_NeverMatchesGen1Recomp(string pkgName)
        => Assert.False(Match(Cat("Gen1Recomp", id: "gen1recomp"), PkgFull(pkgName, "1.0.0", pfn: "recomp", fullName: $"{pkgName}_1.0.0_neutral__hash")));

    [Theory]
    // Short PFN "doom" edge cases — should never match Duke Nukem catalog
    [InlineData("DOOM")]
    [InlineData("Doom64")]
    [InlineData("Doom 3")]
    public void Adversarial_ShortPFNDoomEdgeCases(string pkgName)
        => Assert.False(Match(Cat("Duke Nukem", id: "dukenukem"), PkgFull(pkgName, "1.0.0", pfn: "doom", fullName: $"{pkgName}_1.0.0_neutral__hash")));

    [Fact]
    public void Adversarial_PFNExactlyAtMinLength_6Chars()
    {
        var cat = Cat("RetroArch 2024");
        var pkg = PkgFull("Game", "1.0.0", pfn: "retroa", fullName: "Game_1.0.0_neutral__hash");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Adversarial_PFNBelowMinLength_5Chars()
    {
        var cat = Cat("RetroArch 2024");
        var pkg = PkgFull("Game", "1.0.0", pfn: "retro", fullName: "Game_1.0.0_neutral__hash");
        Assert.False(Match(cat, pkg));
    }

    #endregion

    #region IsPackageMatch — Synthetic multi-catalog competition (FindCatalogMatch)

    [Fact]
    public void Synthetic_ManyCatalogs_CorrectWinner()
    {
        var catalogs = new[]
        {
            Cat("Zelda OoT", id: "zelda-oot"),
            Cat("Zelda MM", id: "zelda-mm"),
            Cat("Zelda BotW", id: "zelda-botw"),
            Cat("Zelda TP", id: "zelda-tp"),
            Cat("Zelda SS", id: "zelda-ss"),
        };
        var pkg = PkgFull("Zelda Majora's Mask", "1.0.0",
            displayName: "Majora's Mask",
            fullName: "ZeldaMM_1.0.0_x64__hash",
            pfn: "ZeldaMM_8wekyb3d8bbwe");
        var (match, _) = FindMatch(pkg, catalogs);
        Assert.NotNull(match);
        Assert.Equal("zelda-mm", match!.Id);
    }

    [Fact]
    public void Synthetic_ManyCatalogs_NoneMatch()
    {
        var catalogs = new[]
        {
            Cat("Dolphin", id: "dolphin"),
            Cat("RetroArch", id: "retroarch"),
            Cat("Citra", id: "citra"),
            Cat("Yuzu", id: "yuzu"),
            Cat("PPSSPP", id: "ppsspp"),
        };
        var pkg = PkgFull("MortalKombat", "1.0.0");
        var (match, _) = FindMatch(pkg, catalogs);
        Assert.Null(match);
    }

    [Fact]
    public void Synthetic_ManyCatalogs_PFNDisambiguates()
    {
        var catalogs = new[]
        {
            Cat("Sonic 1 Decompilation", id: "sonic1"),
            Cat("Sonic 2 Decompilation", id: "sonic2"),
            Cat("Sonic CD Decompilation", id: "soniccd"),
            Cat("Sonic Mania", id: "sonicmania"),
            Cat("Sonic Frontiers", id: "sonicfrontiers"),
        };
        var pkg1 = PkgFull("Game1", "1.0.0", pfn: "Sonic1");
        var pkg2 = PkgFull("Game2", "1.0.0", pfn: "Sonic2");
        var pkg3 = PkgFull("Game3", "1.0.0", pfn: "SonicCD");
        var (m1, _) = FindMatch(pkg1, catalogs);
        var (m2, _) = FindMatch(pkg2, catalogs);
        var (m3, _) = FindMatch(pkg3, catalogs);
        Assert.Equal("sonic1", m1!.Id);
        Assert.Equal("sonic2", m2!.Id);
        Assert.Equal("soniccd", m3!.Id);
    }

    [Fact]
    public void Synthetic_EmptyPkgName_MatchesViaPFNOnly()
    {
        var cat = Cat("MyApp");
        var pkg = PkgFull("", "1.0.0", pfn: "MyApp_8wekyb3d8bbwe");
        Assert.True(Match(cat, pkg));
    }

    [Fact]
    public void Synthetic_EmptyDisplayName_MatchesViaName()
    {
        var cat = Cat("Dolphin");
        var pkg = PkgFull("Dolphin", "1.0.0", displayName: null, fullName: "Dolphin_1.0.0_neutral__hash");
        Assert.True(Match(cat, pkg));
    }

    #endregion

    #region Local override (install-time) — resolution + priority

    private static (VersionCheckerService svc, LocalOverrideService local) BuildWithLocal(params CatalogItem[] catalog)
    {
        var path = Path.Combine(Path.GetTempPath(), "xbvault-tests", Guid.NewGuid().ToString("N") + ".json");
        var local = new LocalOverrideService(path);
        var svc = new VersionCheckerService(new PackageOverrideService(), cache: null, localOverrideService: local);
        svc.SetCatalog(catalog);
        return (svc, local);
    }

    [Fact]
    public void LocalOverride_ResolvesMatch_WhenHeuristicFails()
    {
        // "Shipwright" does not heuristic-match "Ship of Harkinian", but a local
        // override from a prior install links them.
        var (svc, local) = BuildWithLocal(Cat("Ship of Harkinian", id: "soh"));
        local.AddOrUpdate("Shipwright", "soh");
        var pkg = PkgFull("Shipwright", "1.0.0", pfn: "Shipwright_8wekyb3d8bbwe");

        var (match, _) = svc.FindCatalogMatch(pkg);

        Assert.NotNull(match);
        Assert.Equal("soh", match!.Id);
    }

    [Fact]
    public void LocalOverride_WithoutCatalogEntry_ResolvesToNull()
    {
        var (svc, local) = BuildWithLocal(Cat("Ship of Harkinian", id: "soh"));
        local.AddOrUpdate("Unknown", "nonexistent");
        var pkg = PkgFull("Unknown", "1.0.0");

        var (match, _) = svc.FindCatalogMatch(pkg);

        Assert.Null(match);
    }

    [Fact]
    public void LocalOverride_TakesPriorityOverGlobalOverride()
    {
        // Global says Shipwright → some-other; local says Shipwright → soh.
        // Local (user-authored) must win.
        var path = Path.Combine(Path.GetTempPath(), "xbvault-tests", Guid.NewGuid().ToString("N") + ".json");
        var local = new LocalOverrideService(path);
        local.AddOrUpdate("Shipwright", "soh");

        var globalJson = """{ "packageFamilyNameOverrides": [{ "packageFamilyName": "Shipwright", "catalogId": "other" }] }""";
        var os = new PackageOverrideService();
        os.ParseAndMerge(globalJson);

        var svc = new VersionCheckerService(os, cache: null, localOverrideService: local);
        svc.SetCatalog([Cat("Ship of Harkinian", id: "soh"), Cat("Other App", id: "other")]);
        var pkg = PkgFull("Shipwright", "1.0.0", pfn: "Shipwright_8wekyb3d8bbwe");

        var (match, _) = svc.FindCatalogMatch(pkg);

        Assert.NotNull(match);
        Assert.Equal("soh", match!.Id);
    }

    [Fact]
    public void LocalOverride_CaseInsensitiveLookup()
    {
        var (svc, local) = BuildWithLocal(Cat("Ship of Harkinian", id: "soh"));
        local.AddOrUpdate("SHIPWRIGHT", "soh");
        var pkg = PkgFull("shipwright", "1.0.0");

        var (match, _) = svc.FindCatalogMatch(pkg);

        Assert.NotNull(match);
        Assert.Equal("soh", match!.Id);
    }

    #endregion
}

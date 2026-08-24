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
}

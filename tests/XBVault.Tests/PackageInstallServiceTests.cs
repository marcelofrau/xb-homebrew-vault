using System.IO.Compression;
using XBVault.Services;

namespace XBVault.Tests;

public class PackageInstallServiceTests : IDisposable
{
    private readonly string _dir;

    public PackageInstallServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "xbvault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string CreateZip(params (string Name, byte[] Content)[] entries)
    {
        var zipPath = Path.Combine(_dir, $"{Guid.NewGuid():N}.zip");
        using (var fs = File.Create(zipPath))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                using var es = entry.Open();
                es.Write(content, 0, content.Length);
            }
        }
        return zipPath;
    }

    [Fact]
    public void ClassifyPackages_SeparatesMainFromDeps()
    {
        var files = new[]
        {
            Path.Combine(_dir, "MyGame_x64.appx"),
            Path.Combine(_dir, "Microsoft.VCLibs.x64.14.00.appx"),
            Path.Combine(_dir, "install.ps1"),
            Path.Combine(_dir, "MyGame.cer"),
            Path.Combine(_dir, "readme.txt")
        };

        var (main, deps) = PackageInstallService.ClassifyPackages(files);

        Assert.Equal("MyGame_x64.appx", Path.GetFileName(main));
        Assert.Single(deps);
        Assert.Equal("Microsoft.VCLibs.x64.14.00.appx", Path.GetFileName(deps[0]));
    }

    [Fact]
    public void ClassifyPackages_PrefersBundleOverFlat()
    {
        var files = new[]
        {
            Path.Combine(_dir, "Game.appx"),
            Path.Combine(_dir, "Game.msixbundle")
        };

        var (main, _) = PackageInstallService.ClassifyPackages(files);

        Assert.Equal("Game.msixbundle", Path.GetFileName(main));
    }

    [Fact]
    public void ClassifyPackages_AllDeps_UsesFirstAsMain()
    {
        var files = new[]
        {
            Path.Combine(_dir, "Microsoft.VCLibs.x64.14.00.appx"),
            Path.Combine(_dir, "Microsoft.NET.Native.Runtime.2.2.appx")
        };

        var (main, deps) = PackageInstallService.ClassifyPackages(files);

        Assert.Equal("Microsoft.NET.Native.Runtime.2.2.appx", Path.GetFileName(main));
        Assert.Single(deps);
        Assert.Equal("Microsoft.VCLibs.x64.14.00.appx", Path.GetFileName(deps[0]));
    }

    [Fact]
    public void ClassifyPackages_Empty_ReturnsNullMain()
    {
        var (main, deps) = PackageInstallService.ClassifyPackages([]);

        Assert.Null(main);
        Assert.Empty(deps);
    }

    [Fact]
    public void GetFileNameFromUrl_Simple()
    {
        Assert.Equal("game.appx", PackageInstallService.GetFileNameFromUrl("https://example.com/dl/game.appx"));
    }

    [Fact]
    public void GetFileNameFromUrl_NoFileName_ReturnsDefault()
    {
        Assert.Equal("package.appx", PackageInstallService.GetFileNameFromUrl("https://example.com/dl/"));
    }

    [Fact]
    public void GetFileNameFromUrl_IgnoresQueryString()
    {
        Assert.Equal("game.appx", PackageInstallService.GetFileNameFromUrl("https://example.com/dl/game.appx?token=abc&sig=xyz"));
    }

    [Fact]
    public void GetFileNameFromUrl_DecodesPercentEncoding()
    {
        Assert.Equal("My Game.appx", PackageInstallService.GetFileNameFromUrl("https://example.com/dl/My%20Game.appx"));
    }

    [Fact]
    public void ExtractPackage_FromZip_FindsInstallables()
    {
        var zip = CreateZip(
            ("Game_x64.appx", new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
            ("Dependencies/Microsoft.VCLibs.appx", new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
            ("readme.txt", new byte[] { 0x68, 0x69 }));
        var extractDir = Path.Combine(_dir, "extract");

        var packages = PackageInstallService.ExtractPackage(zip, extractDir);

        Assert.Contains(packages, p => Path.GetFileName(p) == "Game_x64.appx");
        Assert.Contains(packages, p => Path.GetFileName(p) == "Microsoft.VCLibs.appx");
        Assert.DoesNotContain(packages, p => Path.GetFileName(p) == "readme.txt");
    }

    [Fact]
    public void ExtractPackage_AppxFile_CopiedDirectly()
    {
        var appx = Path.Combine(_dir, "standalone.appx");
        File.WriteAllBytes(appx, [0x50, 0x4B, 0x03, 0x04]);
        var extractDir = Path.Combine(_dir, "extract");

        var packages = PackageInstallService.ExtractPackage(appx, extractDir);

        Assert.Single(packages);
        Assert.Equal("standalone.appx", Path.GetFileName(packages[0]));
        Assert.True(File.Exists(packages[0]));
    }

    [Fact]
    public void ExtractPackage_ReusesExistingExtractDir()
    {
        var appx = Path.Combine(_dir, "game.appx");
        File.WriteAllBytes(appx, [0x01]);
        var extractDir = Path.Combine(_dir, "extract");
        PackageInstallService.ExtractPackage(appx, extractDir);

        var before = Directory.GetFiles(extractDir).Length;
        var packages = PackageInstallService.ExtractPackage(appx, extractDir);

        Assert.Single(packages);
        Assert.Equal(before, Directory.GetFiles(extractDir).Length);
    }

    [Fact]
    public void ExtractBundles_ReturnsBundles()
    {
        File.WriteAllBytes(Path.Combine(_dir, "Game.msixbundle"), [0x50, 0x4B, 0x03, 0x04]);

        var bundles = PackageInstallService.ExtractBundles(_dir);

        Assert.Single(bundles);
        Assert.Equal("Game.msixbundle", Path.GetFileName(bundles[0]));
    }

    [Fact]
    public void GetInstallableFiles_FiltersByArchitecture()
    {
        File.WriteAllBytes(Path.Combine(_dir, "App_x64.appx"), [0x01]);
        File.WriteAllBytes(Path.Combine(_dir, "App_x86.appx"), [0x01]);
        File.WriteAllBytes(Path.Combine(_dir, "App_neutral.msix"), [0x01]);
        File.WriteAllBytes(Path.Combine(_dir, "App.appx"), [0x01]);
        File.WriteAllBytes(Path.Combine(_dir, "notes.txt"), [0x01]);

        var files = PackageInstallService.GetInstallableFiles(_dir);

        var names = files.Select(Path.GetFileName).ToArray();
        Assert.Contains("App_x64.appx", names);
        Assert.Contains("App_neutral.msix", names);
        Assert.Contains("App.appx", names);
        Assert.DoesNotContain("App_x86.appx", names);
        Assert.DoesNotContain("notes.txt", names);
    }

    [Fact]
    public void AnalyzeDirectory_Classifies()
    {
        File.WriteAllBytes(Path.Combine(_dir, "Game_x64.appx"), [0x01]);
        File.WriteAllBytes(Path.Combine(_dir, "Microsoft.VCLibs.x64.appx"), [0x01]);
        File.WriteAllBytes(Path.Combine(_dir, "junk.cer"), [0x01]);

        var result = PackageInstallService.AnalyzeDirectory(_dir);

        Assert.Equal("Game_x64.appx", Path.GetFileName(result.MainPackage));
        Assert.Single(result.Dependencies);
        Assert.Equal("Microsoft.VCLibs.x64.appx", Path.GetFileName(result.Dependencies[0]));
        Assert.Equal(_dir, result.WorkingDirectory);
    }
}

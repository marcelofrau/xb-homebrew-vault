using XBVault.Models;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Tests;

public class BrowseViewModelTests : IDisposable
{
    private readonly string _dir;

    public BrowseViewModelTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "xbvault-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private sealed class FakeAuth : IXboxAuthService
    {
        public bool IsConnected { get; set; }
        public event Action<bool>? ConnectionChanged;
        public bool IsConfigured => true;
        public string? SmbPassword => null;
        public string? Host => "192.168.0.1";
        public void Configure(string baseUrl, string username, string password) { }
        public SshConnectionInfo GetSshCredentials() => new("localhost", 22, "user", "pass");
        public Task<string?> FetchSmbPasswordAsync() => Task.FromResult<string?>(null);
        public string? GetDevPortalUrl() => null;
        public void MarkConnected() { }
        public void Disconnect() { }
        public Task<bool> EnsureConnectedAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
            => Task.FromResult(new ConnectionTestResult(true, 200, null));
        public void Dispose() { }
    }

    private sealed class FakePackages : IXboxPackageService
    {
        public List<InstalledPackage> Packages { get; set; } = [];
        public Task<List<InstalledPackage>> GetInstalledPackagesAsync() => Task.FromResult(Packages);
        public Task<bool> UninstallPackageAsync(string packageFullName) => Task.FromResult(true);
        public Task<(bool Success, string? ErrorMessage)> LaunchPackageAsync(string packageFullName, string packageRelativeId)
            => Task.FromResult((true, (string?)null));
        public Task<HashSet<string>> GetRunningPackageNamesAsync() => Task.FromResult(new HashSet<string>());
        public Task<bool> SuspendPackageAsync(string packageFullName) => Task.FromResult(true);
        public Task<bool> TerminatePackageAsync(string packageFullName) => Task.FromResult(true);
        public Task<bool> InstallPackageAsync(string filePath, IProgress<double>? progress = null) => Task.FromResult(true);
        public Task<bool> InstallPackageAsync(string packagePath, string[] dependencies, IProgress<InstallProgressInfo>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private BrowseViewModel Build()
    {
        var auth = new FakeAuth();
        var pkg = new FakePackages();
        var cache = new CacheService(Path.Combine(_dir, "cache"));
        var installService = new PackageInstallService(cache, pkg);
        var catalogService = new CatalogApiService();
        var overrideService = new PackageOverrideService();
        var checker = new VersionCheckerService(overrideService, new UpdateVersionCache(Path.Combine(_dir, "cache2.json")));
        return new BrowseViewModel(installService, auth, pkg, catalogService, overrideService, checker);
    }

    [Fact]
    public void Default_NotInstalledMode_ReinstallAndUninstallHidden()
    {
        var vm = Build();
        Assert.False(vm.IsInstalledMode);
        Assert.False(vm.ShowReinstallButton);
        Assert.False(vm.ShowUninstallButton);
        Assert.True(vm.ShowCheckButton);
        Assert.True(vm.ShowInstallActionButton);
    }

    [Fact]
    public void InstalledMode_ShowsReinstallAndUninstall_HidesCheckAndInstall()
    {
        var vm = Build();
        vm.IsInstalledMode = true;
        Assert.True(vm.ShowReinstallButton);
        Assert.True(vm.ShowUninstallButton);
        Assert.False(vm.ShowCheckButton);
        Assert.False(vm.ShowInstallActionButton);
    }

    [Fact]
    public void UpdatingMode_DoesNotShowReinstallOrUninstall()
    {
        var vm = Build();
        vm.IsInstalledMode = true;
        vm.IsUpdateMode = true;
        vm.IsInstalledMode = false;
        Assert.False(vm.ShowReinstallButton);
        Assert.False(vm.ShowUninstallButton);
    }

    [Fact]
    public void InstalledMode_WhileInstalling_HidesReinstallAndUninstall()
    {
        var vm = Build();
        vm.IsInstalledMode = true;
        Assert.True(vm.ShowReinstallButton);
        vm.IsInstalling = true;
        Assert.False(vm.ShowReinstallButton);
        Assert.False(vm.ShowUninstallButton);
    }

    [Fact]
    public void UninstallFromDetail_FiresDelegateWithSelectedInstalledPackage()
    {
        var vm = Build();
        var pkg = new InstalledPackage { Name = "Zelda 64: Recompiled" };
        InstalledPackage? received = null;
        vm.UninstallFromDetailAction = p => received = p;
        vm.SelectedInstalledPackage = pkg;
        Assert.True(vm.UninstallFromDetailCommand.CanExecute(null));
        vm.UninstallFromDetailCommand.Execute(null);
        Assert.Same(pkg, received);
    }

    [Fact]
    public void UninstallFromDetail_NoSelectedPackage_DoesNotFire()
    {
        var vm = Build();
        var fired = false;
        vm.UninstallFromDetailAction = _ => fired = true;
        vm.SelectedInstalledPackage = null;
        vm.UninstallFromDetailCommand.Execute(null);
        Assert.False(fired);
    }

    [Fact]
    public async Task ReinstallFromDetail_FiresDelegateWithSelectedInstalledPackage()
    {
        var vm = Build();
        var pkg = new InstalledPackage { Name = "Zelda 64: Recompiled" };
        InstalledPackage? received = null;
        vm.ReinstallFromDetailAction = p => { received = p; return Task.CompletedTask; };
        vm.SelectedInstalledPackage = pkg;
        Assert.True(vm.ReinstallFromDetailCommand.CanExecute(null));
        await vm.ReinstallFromDetailCommand.ExecuteAsync(null);
        Assert.Same(pkg, received);
    }
}

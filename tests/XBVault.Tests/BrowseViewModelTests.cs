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
        public FakeAuth(bool connected) => IsConnected = connected;
        public bool IsConnected { get; set; }
        public event Action<bool>? ConnectionChanged;
        public bool IsConfigured => true;
        public string? SmbPassword => null;
        public string? Host => "192.168.0.1";
        public void Configure(string baseUrl, string username, string password) { }
        public SshConnectionInfo GetSshCredentials() => new("localhost", 22, "user", "pass");
        public Task<string?> FetchSmbPasswordAsync() => Task.FromResult<string?>(null);
        public string? GetDevPortalUrl() => null;
        public void MarkConnected() => ConnectionChanged?.Invoke(true);
        public void Disconnect() => ConnectionChanged?.Invoke(false);
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

    private (BrowseViewModel Vm, FakeAuth Auth) Build(bool connected = false)
    {
        var auth = new FakeAuth(connected);
        var pkg = new FakePackages();
        var cache = new CacheService(Path.Combine(_dir, "cache"));
        var installService = new PackageInstallService(cache, pkg);
        var catalogService = new CatalogApiService();
        var overrideService = new PackageOverrideService();
        var checker = new VersionCheckerService(overrideService, new UpdateVersionCache(Path.Combine(_dir, "cache2.json")));
        return (new BrowseViewModel(installService, auth, pkg, catalogService, overrideService, checker), auth);
    }

    private static CatalogItem Item(bool installed = false, bool outdated = false, string? installedVersion = null)
    {
        var item = new CatalogItem { Name = "Zelda 64: Recompiled", Version = "2.9.2" };
        item.IsInstalledOnXbox = installed;
        item.IsOutdatedOnXbox = outdated;
        if (installed && installedVersion is not null)
            item.MatchedInstalledPackage = new InstalledPackage { Name = item.Name, Version = installedVersion };
        return item;
    }

    [Fact]
    public void NotConnected_ShowsConnectButton_HidesAllActions()
    {
        var (vm, _) = Build();
        vm.SelectedItem = Item();
        Assert.False(vm.IsConnected);
        Assert.True(vm.DetailNeedsConnect);
        Assert.True(vm.ShowConnectButton);
        Assert.False(vm.ShowDetailInstallButton);
        Assert.False(vm.ShowDetailUpdateButton);
        Assert.False(vm.ShowDetailReinstallButton);
        Assert.False(vm.ShowDetailUninstallButton);
        Assert.Null(vm.DetailStatusText);
    }

    [Fact]
    public void NoSelection_HidesAllActionsAndConnect()
    {
        var (vm, _) = Build();
        Assert.False(vm.DetailNeedsConnect);
        Assert.False(vm.ShowConnectButton);
        Assert.False(vm.ShowDetailInstallButton);
        Assert.False(vm.ShowDetailUpdateButton);
        Assert.False(vm.ShowDetailReinstallButton);
        Assert.False(vm.ShowDetailUninstallButton);
    }

    [Fact]
    public void Connected_NotInstalled_ShowsInstallOnly()
    {
        var (vm, _) = Build(connected: true);
        vm.SelectedItem = Item();
        Assert.False(vm.ShowConnectButton);
        Assert.True(vm.ShowDetailInstallButton);
        Assert.False(vm.ShowDetailUpdateButton);
        Assert.False(vm.ShowDetailReinstallButton);
        Assert.False(vm.ShowDetailUninstallButton);
        Assert.Null(vm.DetailStatusText);
    }

    [Fact]
    public void Connected_InstalledUpToDate_ShowsReinstallAndUninstall()
    {
        var (vm, _) = Build(connected: true);
        vm.SelectedItem = Item(installed: true, installedVersion: "2.9.2");
        Assert.False(vm.ShowConnectButton);
        Assert.False(vm.ShowDetailInstallButton);
        Assert.False(vm.ShowDetailUpdateButton);
        Assert.True(vm.ShowDetailReinstallButton);
        Assert.True(vm.ShowDetailUninstallButton);
        Assert.Contains("2.9.2", vm.DetailStatusText);
    }

    [Fact]
    public void Connected_InstalledOutdated_ShowsUpdateAndUninstall_NotReinstall()
    {
        var (vm, _) = Build(connected: true);
        vm.SelectedItem = Item(installed: true, outdated: true, installedVersion: "2.9.0");
        Assert.False(vm.ShowDetailInstallButton);
        Assert.True(vm.ShowDetailUpdateButton);
        Assert.False(vm.ShowDetailReinstallButton);
        Assert.True(vm.ShowDetailUninstallButton);
        Assert.Contains("Update available", vm.DetailStatusText);
        Assert.Contains("2.9.0", vm.DetailStatusText);
    }

    [Fact]
    public void Connected_InstalledNotOutdated_WithSelectedPackage_UsesPackageForStatus()
    {
        var (vm, _) = Build(connected: true);
        vm.SelectedItem = Item(installed: true, installedVersion: "2.9.1");
        Assert.True(vm.ShowDetailReinstallButton);
        Assert.Contains("2.9.1", vm.DetailStatusText);
    }

    [Fact]
    public void Installed_WhileInstalling_HidesReinstallAndUninstall()
    {
        var (vm, _) = Build(connected: true);
        vm.SelectedItem = Item(installed: true, installedVersion: "2.9.2");
        vm.IsInstalling = true;
        Assert.False(vm.ShowConnectButton);
        Assert.False(vm.ShowDetailInstallButton);
        Assert.False(vm.ShowDetailUpdateButton);
        Assert.False(vm.ShowDetailReinstallButton);
        Assert.False(vm.ShowDetailUninstallButton);
    }

    [Fact]
    public void InstallComplete_Success_ShowsFinishButton()
    {
        var (vm, _) = Build(connected: true);
        vm.SelectedItem = Item();
        vm.InstallComplete = true;
        vm.InstallSuccess = true;
        Assert.True(vm.ShowFinishButton);
        Assert.False(vm.ShowDetailInstallButton);
        Assert.False(vm.ShowDetailUninstallButton);
    }

    [Fact]
    public void ConnectFromDetail_FiresDelegate()
    {
        var (vm, _) = Build();
        vm.SelectedItem = Item();
        var fired = false;
        vm.ShowConnectAction = () => { fired = true; return Task.FromResult(true); };
        Assert.True(vm.ConnectFromDetailCommand.CanExecute(null));
        vm.ConnectFromDetailCommand.Execute(null);
        Assert.True(fired);
    }

    [Fact]
    public async Task ConnectFromDetail_AfterConnected_HydratesSelectedPackageFromMatched()
    {
        var (vm, auth) = Build();
        var item = Item(installed: true, installedVersion: "2.9.2");
        vm.SelectedItem = item;
        vm.ShowConnectAction = () =>
        {
            auth.MarkConnected();
            return Task.FromResult(true);
        };
        await vm.ConnectFromDetailCommand.ExecuteAsync(null);
        Assert.Same(item.MatchedInstalledPackage, vm.SelectedInstalledPackage);
        Assert.True(vm.ShowDetailReinstallButton);
    }

    [Fact]
    public async Task UninstallFromDetail_ConfirmedUninstallsAndClosesDetail()
    {
        var (vm, _) = Build(connected: true);
        var pkg = new InstalledPackage { Name = "Zelda 64: Recompiled" };
        var closed = false;
        vm.CloseDetailAction = () => closed = true;
        vm.ConfirmUninstallAsync = _ => Task.FromResult(true);
        vm.SelectedInstalledPackage = pkg;
        Assert.True(vm.UninstallFromDetailCommand.CanExecute(null));
        await vm.UninstallFromDetailCommand.ExecuteAsync(null);
        Assert.False(vm.IsUninstallingDetail);
        Assert.False(vm.UninstallDetailFailed);
        Assert.True(closed);
    }

    [Fact]
    public async Task UninstallFromDetail_Declined_DoesNotUninstallNorClose()
    {
        var (vm, _) = Build(connected: true);
        var pkg = new InstalledPackage { Name = "Zelda 64: Recompiled" };
        var closed = false;
        vm.CloseDetailAction = () => closed = true;
        vm.ConfirmUninstallAsync = _ => Task.FromResult(false);
        vm.SelectedInstalledPackage = pkg;
        await vm.UninstallFromDetailCommand.ExecuteAsync(null);
        Assert.False(vm.IsUninstallingDetail);
        Assert.False(closed);
    }

    [Fact]
    public async Task UninstallFromDetail_NoSelectedPackage_DoesNotFire()
    {
        var (vm, _) = Build(connected: true);
        var closed = false;
        vm.CloseDetailAction = () => closed = true;
        vm.SelectedInstalledPackage = null;
        vm.SelectedItem = null;
        await vm.UninstallFromDetailCommand.ExecuteAsync(null);
        Assert.False(vm.IsUninstallingDetail);
        Assert.False(closed);
    }

    [Fact]
    public async Task UninstallFromDetail_UsesMatchedInstalledPackage_FromBrowse()
    {
        var (vm, _) = Build(connected: true);
        var item = new CatalogItem { Name = "Zelda 64: Recompiled" };
        item.MatchedInstalledPackage = new InstalledPackage { Name = "Zelda 64: Recompiled" };
        vm.SelectedItem = item;
        var closed = false;
        vm.CloseDetailAction = () => closed = true;
        vm.ConfirmUninstallAsync = _ => Task.FromResult(true);
        await vm.UninstallFromDetailCommand.ExecuteAsync(null);
        Assert.False(vm.IsUninstallingDetail);
        Assert.True(closed);
    }

    [Fact]
    public async Task ReinstallFromDetail_FiresDelegateWithSelectedInstalledPackage()
    {
        var (vm, _) = Build(connected: true);
        var pkg = new InstalledPackage { Name = "Zelda 64: Recompiled" };
        InstalledPackage? received = null;
        vm.ReinstallFromDetailAction = p => { received = p; return Task.CompletedTask; };
        vm.SelectedInstalledPackage = pkg;
        Assert.True(vm.ReinstallFromDetailCommand.CanExecute(null));
        await vm.ReinstallFromDetailCommand.ExecuteAsync(null);
        Assert.Same(pkg, received);
    }
}

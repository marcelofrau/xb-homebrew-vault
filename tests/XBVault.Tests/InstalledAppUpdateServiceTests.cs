using XBVault.Models;
using XBVault.Services;

namespace XBVault.Tests;

public class InstalledAppUpdateServiceTests : IDisposable
{
    private readonly string _dir;

    public InstalledAppUpdateServiceTests()
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

        public void RaiseConnectionChanged(bool connected) => ConnectionChanged?.Invoke(connected);

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
        public Task<bool> InstallPackageAsync(string packagePath, string[] dependencies, IProgress<InstallProgressInfo>? progress = null)
            => Task.FromResult(true);
    }

    private sealed class InlineNotifications : NotificationCenterService
    {
        protected override void PostToUi(Action action) => action();
    }

    private sealed class InlineTasks : BackgroundTaskService
    {
        protected override void PostToUi(Action action) => action();
        protected override Avalonia.Threading.DispatcherTimer? CreateElapsedTimer() => null;
    }

    private (InstalledAppUpdateService service, FakeAuth auth, InlineNotifications notifications, VersionCheckerService checker, InlineTasks tasks) Build(
        List<CatalogItem> catalog,
        List<InstalledPackage> packages)
    {
        var auth = new FakeAuth { IsConnected = true };
        var pkgService = new FakePackages { Packages = packages };
        using var overrideService = new PackageOverrideService();
        var checker = new VersionCheckerService(overrideService, new UpdateVersionCache(Path.Combine(_dir, "cache.json")));
        checker.SetCatalog(catalog);
        var notifications = new InlineNotifications();
        var tasks = new InlineTasks();
        tasks.Start();
        var service = new InstalledAppUpdateService(auth, pkgService, checker, notifications, tasks);
        return (service, auth, notifications, checker, tasks);
    }

    private static CatalogItem Item(string name, string version)
        => new()
        {
            Id = $"catalog-{name}",
            Name = name,
            Version = version,
            Category = "Games"
        };

    private static InstalledPackage Pkg(string name, string version)
        => new()
        {
            Name = name,
            Version = version,
            FullName = $"{name}_1.0.0.0_neutral__8wekyb3d8bbwe",
            PackageFamilyName = $"{name}_8wekyb3d8bbwe"
        };

    [Fact]
    public async Task ScanAsync_NotConnected_NoNotification()
    {
        var (service, auth, notifications, _, _) = Build(
            [Item("Sonic", "1.2.0")],
            [Pkg("sonic", "1.0.0")]);
        auth.IsConnected = false;

        await service.ScanAsync();

        Assert.Empty(notifications.Active);
        Assert.Empty(notifications.History);
    }

    [Fact]
    public async Task ScanAsync_NoCatalog_NoNotification()
    {
        var (service, _, notifications, _, _) = Build(
            [],
            [Pkg("sonic", "1.0.0")]);

        await service.ScanAsync();

        Assert.Empty(notifications.Active);
    }

    [Fact]
    public async Task ScanAsync_NoOutdated_NoNotification()
    {
        var (service, _, notifications, _, _) = Build(
            [Item("Sonic", "1.0.0")],
            [Pkg("sonic", "1.0.0")]);

        await service.ScanAsync();

        Assert.Empty(notifications.Active);
    }

    [Fact]
    public async Task ScanAsync_OneOutdated_NotifiesWithAction()
    {
        var opened = false;
        var (service, _, notifications, _, _) = Build(
            [Item("Sonic", "1.2.0")],
            [Pkg("sonic", "1.0.0")]);
        service.OpenUpdateDialogAsync = _ =>
        {
            opened = true;
            return Task.CompletedTask;
        };

        await service.ScanAsync();

        var item = Assert.Single(notifications.Active);
        Assert.Equal("1 app update available", item.Title);
        var action = Assert.Single(item.Actions);
        Assert.Contains("Sonic", action.Label);
        Assert.Contains("1.0.0", action.Label);
        Assert.Contains("1.2.0", action.Label);

        action.Action?.Invoke();
        Assert.True(opened);
    }

    [Fact]
    public async Task ScanAsync_MultipleOutdated_OneGroupedNotification()
    {
        var (service, _, notifications, _, _) = Build(
            [Item("Sonic", "1.2.0"), Item("Mario", "3.0.0")],
            [Pkg("sonic", "1.0.0"), Pkg("mario", "2.0.0"), Pkg("zelda", "1.0.0")]);

        await service.ScanAsync();

        var item = Assert.Single(notifications.Active);
        Assert.Equal("2 app updates available", item.Title);
        Assert.Equal(2, item.Actions.Count);
    }

    [Fact]
    public async Task ScanAsync_Twice_SamePair_NoDuplicateNotification()
    {
        var (service, _, notifications, _, _) = Build(
            [Item("Sonic", "1.2.0")],
            [Pkg("sonic", "1.0.0")]);

        await service.ScanAsync();
        await service.ScanAsync();

        Assert.Single(notifications.Active);
        Assert.Empty(notifications.History);
    }

    [Fact]
    public async Task ScanAsync_CountChange_ReplacesNotificationInsteadOfStacking()
    {
        var (service, _, notifications, _, _) = Build(
            [Item("Sonic", "1.2.0"), Item("Mario", "2.0.0")],
            [Pkg("sonic", "1.0.0"), Pkg("mario", "1.0.0")]);

        await service.ScanAsync();
        var first = Assert.Single(notifications.Active);
        Assert.Equal("2 app updates available", first.Title);

        var second = Build(
            [Item("Sonic", "1.2.0")],
            [Pkg("sonic", "1.0.0")]);
        await second.service.ScanAsync();

        var replaced = Assert.Single(second.notifications.Active);
        Assert.Equal("1 app update available", replaced.Title);
        Assert.NotEqual(first.Id, replaced.Id);
        Assert.Equal(1, second.notifications.UnacknowledgedCount);
    }

    [Fact]
    public async Task ScanAsync_AfterDismiss_NotifiesAgain()
    {
        var (service, _, notifications, _, _) = Build(
            [Item("Sonic", "1.2.0")],
            [Pkg("sonic", "1.0.0")]);

        await service.ScanAsync();
        var first = Assert.Single(notifications.Active);
        notifications.Dismiss(first.Id);
        Assert.Empty(notifications.Active);

        await service.ScanAsync();

        var second = Assert.Single(notifications.Active);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task ScanAsync_DoesNotPersistSuppressionCache()
    {
        var (service, _, notifications, checker, _) = Build(
            [Item("Sonic", "1.2.0")],
            [Pkg("sonic", "1.0.0")]);

        await service.ScanAsync();
        Assert.Single(notifications.Active);

        Assert.NotNull(checker.FindOutdated(Pkg("sonic", "1.0.0")));
    }

    [Fact]
    public async Task ScanAsync_NewerCatalogVersion_NotifiesAgain()
    {
        var (service, _, notifications, _, _) = Build(
            [Item("Sonic", "1.2.0")],
            [Pkg("sonic", "1.0.0")]);

        await service.ScanAsync();
        Assert.Single(notifications.Active);

        service.Stop();
        var second = Build(
            [Item("Sonic", "2.0.0")],
            [Pkg("sonic", "1.0.0")]);
        await second.service.ScanAsync();

        Assert.Single(second.notifications.Active);
    }

    [Fact]
    public async Task ScanAsync_ConcurrentScans_SingleNotification()
    {
        var (service, _, notifications, _, _) = Build(
            [Item("Sonic", "1.2.0")],
            [Pkg("sonic", "1.0.0")]);

        await Task.WhenAll(service.ScanAsync(), service.ScanAsync(), service.ScanAsync());

        Assert.Single(notifications.Active);
    }

    [Fact]
    public async Task OnConnectionChanged_Connected_TriggersScan()
    {
        var (service, auth, notifications, _, tasks) = Build(
            [Item("Sonic", "1.2.0")],
            [Pkg("sonic", "1.0.0")]);
        service.Start();

        auth.RaiseConnectionChanged(true);
        await Task.Delay(50);

        Assert.NotEmpty(notifications.Active);
        service.Stop();
        tasks.Stop();
    }

    [Fact]
    public async Task ConnectionChanged_Disconnected_NoScan()
    {
        var (service, auth, notifications, _, tasks) = Build(
            [Item("Sonic", "1.2.0")],
            [Pkg("sonic", "1.0.0")]);
        service.Start();

        auth.RaiseConnectionChanged(false);
        await Task.Delay(50);

        Assert.Empty(notifications.Active);
        service.Stop();
        tasks.Stop();
    }
}

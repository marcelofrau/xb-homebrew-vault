using XBVault.Models;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Tests;

public class InstalledViewModelTests
{
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
        public int UninstallCalls { get; private set; }
        public List<InstalledPackage> Packages { get; set; } = [];
        public Task<List<InstalledPackage>> GetInstalledPackagesAsync() => Task.FromResult(Packages);
        public Task<bool> UninstallPackageAsync(string packageFullName)
        {
            UninstallCalls++;
            return Task.FromResult(true);
        }
        public Task<(bool Success, string? ErrorMessage)> LaunchPackageAsync(string packageFullName, string packageRelativeId)
            => Task.FromResult((true, (string?)null));
        public Task<HashSet<string>> GetRunningPackageNamesAsync() => Task.FromResult(new HashSet<string>());
        public Task<bool> SuspendPackageAsync(string packageFullName) => Task.FromResult(true);
        public Task<bool> TerminatePackageAsync(string packageFullName) => Task.FromResult(true);
        public Task<bool> InstallPackageAsync(string filePath, IProgress<double>? progress = null) => Task.FromResult(true);
        public Task<bool> InstallPackageAsync(string packagePath, string[] dependencies, IProgress<InstallProgressInfo>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private InstalledViewModel Build(out FakePackages pkg)
    {
        var auth = new FakeAuth();
        pkg = new FakePackages();
        return new InstalledViewModel(auth, pkg);
    }

    private static InstalledPackage MakePkg(string name) => new()
    {
        Name = name,
        FullName = $"Test.{name}_x.x.x_x86__hash",
        DisplayName = name
    };

    [Fact]
    public async Task Reinstall_InstallOverExisting_DoesNotUninstall()
    {
        var vm = Build(out var pkgSvc);
        var pkg = MakePkg("MyGame");

        var reinstallActionCalls = 0;
        vm.ConfirmReinstallAsync = _ => Task.FromResult(true);
        vm.ReinstallInstallAction = () => reinstallActionCalls++;

        await vm.ReinstallPackageCommand.ExecuteAsync(pkg);

        // Reinstall must install over the existing package (no uninstall) to
        // preserve the app's LocalState.
        Assert.Equal(1, reinstallActionCalls);
        Assert.Equal(0, pkgSvc.UninstallCalls);
    }

    [Fact]
    public async Task Reinstall_CancelledByConfirm_DoesNothing()
    {
        var vm = Build(out var pkgSvc);
        var pkg = MakePkg("MyGame");

        var reinstallActionCalls = 0;
        vm.ConfirmReinstallAsync = _ => Task.FromResult(false);
        vm.ReinstallInstallAction = () => reinstallActionCalls++;

        await vm.ReinstallPackageCommand.ExecuteAsync(pkg);

        Assert.Equal(0, reinstallActionCalls);
        Assert.Equal(0, pkgSvc.UninstallCalls);
    }
}

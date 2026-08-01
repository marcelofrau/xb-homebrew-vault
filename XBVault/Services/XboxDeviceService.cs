using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

/// <summary>
/// Facade over the Xbox domain services. Kept for compatibility with existing
/// ViewModels and tests. New code should depend on the domain services directly.
/// </summary>
public class XboxDeviceService : IDisposable
{
    private readonly XboxAuthService _auth;
    private readonly XboxPackageService _packages;
    private readonly XboxProcessService _processes;
    private readonly XboxSystemService _system;
    private readonly XboxNetworkService _network;
    private readonly XboxPerformanceService _performance;

    public XboxDeviceService()
    {
        _auth = new XboxAuthService();
        _auth.ConnectionChanged += (connected) => ConnectionChanged?.Invoke(connected);
        _packages = new XboxPackageService(_auth);
        _processes = new XboxProcessService(_auth);
        _system = new XboxSystemService(_auth);
        _network = new XboxNetworkService(_auth);
        _performance = new XboxPerformanceService(_auth);
    }

    public event Action<bool>? ConnectionChanged;

    // ---- Configuration / connection (delegated to XboxAuthService) ----

    public bool IsConfigured => _auth.IsConfigured;
    public bool IsConnected => _auth.IsConnected;
    public string? SmbPassword => _auth.SmbPassword;
    public string? Host => _auth.Host;

    public void Configure(string baseUrl, string username, string password)
        => _auth.Configure(baseUrl, username, password);

    public SshConnectionInfo GetSshCredentials() => _auth.GetSshCredentials();

    public Task<string?> FetchSmbPasswordAsync() => _auth.FetchSmbPasswordAsync();

    public string? GetDevPortalUrl() => _auth.GetDevPortalUrl();

    public void MarkConnected() => _auth.MarkConnected();

    public void Disconnect() => _auth.Disconnect();

    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
        => _auth.TestConnectionAsync(ct);

    // ---- Packages (delegated to XboxPackageService) ----

    public Task<List<InstalledPackage>> GetInstalledPackagesAsync()
        => _packages.GetInstalledPackagesAsync();

    public Task<bool> UninstallPackageAsync(string packageFullName)
        => _packages.UninstallPackageAsync(packageFullName);

    public Task<(bool Success, string? ErrorMessage)> LaunchPackageAsync(string packageFullName, string packageRelativeId)
        => _packages.LaunchPackageAsync(packageFullName, packageRelativeId);

    public Task<HashSet<string>> GetRunningPackageNamesAsync()
        => _packages.GetRunningPackageNamesAsync();

    public Task<bool> SuspendPackageAsync(string packageFullName)
        => _packages.SuspendPackageAsync(packageFullName);

    public Task<bool> TerminatePackageAsync(string packageFullName)
        => _packages.TerminatePackageAsync(packageFullName);

    public Task<bool> InstallPackageAsync(string filePath, IProgress<double>? progress = null)
        => _packages.InstallPackageAsync(filePath, progress);

    public Task<bool> InstallPackageAsync(string packagePath, string[] dependencies, IProgress<InstallProgressInfo>? progress = null)
        => _packages.InstallPackageAsync(packagePath, dependencies, progress);

    // ---- Processes (delegated to XboxProcessService) ----

    public Task<string?> GetRunningTitleAsync()
        => _processes.GetRunningTitleAsync();

    public Task<string?> GetProcessesAsync()
        => _processes.GetProcessesAsync();

    public Task<bool> KillProcessAsync(int pid)
        => _processes.KillProcessAsync(pid);

    // ---- System (delegated to XboxSystemService) ----

    public Task<byte[]?> CaptureScreenshotAsync(CancellationToken ct = default)
        => _system.CaptureScreenshotAsync(ct);

    public Task<string?> GetSystemInfoAsync()
        => _system.GetSystemInfoAsync();

    public Task<string?> GetCrashDumpsAsync()
        => _system.GetCrashDumpsAsync();

    public Task<bool> DeleteCrashDumpAsync(string filename)
        => _system.DeleteCrashDumpAsync(filename);

    public Task<string?> GetCrashControlAsync()
        => _system.GetCrashControlAsync();

    public Task<bool> SetCrashControlAsync(bool enabled)
        => _system.SetCrashControlAsync(enabled);

    public Task<bool> RestartXboxAsync()
        => _system.RestartXboxAsync();

    public Task<bool> ShutdownXboxAsync()
        => _system.ShutdownXboxAsync();

    // ---- Network (delegated to XboxNetworkService) ----

    public Task<string?> GetNetworkConfigAsync()
        => _network.GetNetworkConfigAsync();

    public Task<string?> GetWifiInterfacesAsync()
        => _network.GetWifiInterfacesAsync();

    public Task<string?> GetWifiNetworksAsync(string interfaceGuid)
        => _network.GetWifiNetworksAsync(interfaceGuid);

    // ---- Performance (delegated to XboxPerformanceService) ----

    public Task ConnectPerformanceWsAsync(Action<PerformanceSnapshot> onData, CancellationToken ct)
        => _performance.ConnectPerformanceWsAsync(onData, ct);

    // ---- Static response parsers (delegated to XboxResponseParser) ----
    // Kept so existing tests and callers keep working unchanged.

    internal static string? TryParseError(string? body) => XboxResponseParser.TryParseError(body);

    internal static string? ParseMsixPackageName(string msixPath) => XboxResponseParser.ParseMsixPackageName(msixPath);

    internal static bool IsIdleCode(HttpStatusCode code) => XboxResponseParser.IsIdleCode(code);

    internal static bool IsSignatureError(string json) => XboxResponseParser.IsSignatureError(json);

    internal static bool IsResourceInUseError(string json, out string busyApps)
        => XboxResponseParser.IsResourceInUseError(json, out busyApps);

    internal static bool IsHigherVersionError(string json, out string message)
        => XboxResponseParser.IsHigherVersionError(json, out message);

    internal static bool IsFatalDeploymentError(string json, out string error)
        => XboxResponseParser.IsFatalDeploymentError(json, out error);

    internal static bool IsJsonSuccess(string json, out string statusMessage)
        => XboxResponseParser.IsJsonSuccess(json, out statusMessage);

    internal static string Truncate(string s, int maxLen) => XboxResponseParser.Truncate(s, maxLen);

    internal static string SizeFormat(long bytes) => XboxResponseParser.SizeFormat(bytes);

    public void Dispose()
    {
        _auth.Dispose();
        GC.SuppressFinalize(this);
    }
}

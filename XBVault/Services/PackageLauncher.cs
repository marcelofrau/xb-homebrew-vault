#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

public class PackageLauncher
{
    private readonly IXboxPackageService _packageService;

    public PackageLauncher(IXboxPackageService packageService)
    {
        _packageService = packageService;
    }

    public async Task<LaunchPackageResult> LaunchAsync(
        InstalledPackage pkg,
        IReadOnlyCollection<InstalledPackage> candidates,
        Action<string>? onStatus = null)
    {
        if (pkg.IsRunning)
            return LaunchPackageResult.Ok;

        var rid = pkg.PackageRelativeId;
        if (string.IsNullOrEmpty(rid))
            return LaunchPackageResult.Fail("Cannot launch: no package relative id");

        var running = candidates.FirstOrDefault(p => p.IsRunning && p != pkg);
        if (running is not null)
        {
            onStatus?.Invoke($"Suspending {running.Name}...");
            var suspended = await _packageService.SuspendPackageAsync(running.FullName);
            if (!suspended)
            {
                Logger.Warn($"Failed to suspend {running.Name} before launch");
                return LaunchPackageResult.FailSuspend(running.Name);
            }
            running.IsRunning = false;
            Logger.Info($"Suspended {running.Name} before launch");
        }

        try
        {
            var (ok, err) = await _packageService.LaunchPackageAsync(pkg.FullName, rid);
            if (!ok)
            {
                Logger.Warn($"Launch failed for {pkg.Name}: {err ?? "unknown error"}");
                return LaunchPackageResult.Fail(err ?? "unknown error");
            }

            pkg.IsRunning = true;
            Logger.Info($"Launched {pkg.Name}");
            return LaunchPackageResult.Ok;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Launch failed for {pkg.Name}");
            return LaunchPackageResult.Fail("Launch failed");
        }
    }
}

public readonly record struct LaunchPackageResult(bool Success, string? Error, bool SuspendFailed = false)
{
    public static LaunchPackageResult Ok => new(true, null);
    public static LaunchPackageResult Fail(string? error) => new(false, error);
    public static LaunchPackageResult FailSuspend(string name) => new(false, $"Failed to suspend {name}", SuspendFailed: true);
}

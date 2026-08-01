using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

public interface IXboxPackageService
{
    Task<List<InstalledPackage>> GetInstalledPackagesAsync();
    Task<bool> UninstallPackageAsync(string packageFullName);
    Task<(bool Success, string? ErrorMessage)> LaunchPackageAsync(string packageFullName, string packageRelativeId);
    Task<HashSet<string>> GetRunningPackageNamesAsync();
    Task<bool> SuspendPackageAsync(string packageFullName);
    Task<bool> TerminatePackageAsync(string packageFullName);
    Task<bool> InstallPackageAsync(string filePath, IProgress<double>? progress = null);
    Task<bool> InstallPackageAsync(string packagePath, string[] dependencies, IProgress<InstallProgressInfo>? progress = null);
}

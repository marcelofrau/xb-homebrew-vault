#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

/// <summary>
/// Provides package-management operations over the Xbox Device Portal API.
/// </summary>
/// <remarks>
/// Implementations are expected to handle Device Portal encoding rules and return user-actionable failures
/// instead of leaking raw HTTP details to ViewModels.
/// </remarks>
public interface IXboxPackageService
{
    /// <summary>
    /// Lists packages installed on the Xbox.
    /// </summary>
    Task<List<InstalledPackage>> GetInstalledPackagesAsync();

    /// <summary>
    /// Uninstalls a package by full package name.
    /// </summary>
    Task<bool> UninstallPackageAsync(string packageFullName);

    /// <summary>
    /// Launches a package using its full name and relative app id.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> LaunchPackageAsync(string packageFullName, string packageRelativeId);

    /// <summary>
    /// Returns full package names currently reported as running.
    /// </summary>
    Task<HashSet<string>> GetRunningPackageNamesAsync();

    /// <summary>
    /// Suspends a running package.
    /// </summary>
    Task<bool> SuspendPackageAsync(string packageFullName);

    /// <summary>
    /// Terminates a running package.
    /// </summary>
    Task<bool> TerminatePackageAsync(string packageFullName);

    /// <summary>
    /// Installs a single package file from local storage.
    /// </summary>
    Task<bool> InstallPackageAsync(string filePath, IProgress<double>? progress = null);

    /// <summary>
    /// Installs a package and its dependency files, reporting staged progress.
    /// </summary>
    Task<bool> InstallPackageAsync(string packagePath, string[] dependencies, IProgress<InstallProgressInfo>? progress = null, CancellationToken cancellationToken = default);
}

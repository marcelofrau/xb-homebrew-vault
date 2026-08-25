#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XBVault.Helpers;
using XBVault.Models;

#pragma warning disable CA1001 // HttpClient is long-lived singleton

namespace XBVault.Services;

/// <summary>
/// Downloads, analyzes, and installs Xbox packages with dependency handling.
/// </summary>
/// <remarks>
/// This service is UI-agnostic and should remain reusable by desktop and Android frontends.
/// It owns install-file classification, dependency ordering, temporary extraction, and progress reporting;
/// ViewModels own user-facing status state and dialogs.
/// </remarks>
public class PackageInstallService
{
    private readonly HttpClient _http;
    private readonly CacheService _cache;
    private readonly IXboxPackageService _packageService;
    private readonly IAppLogger _log;

    private static readonly HashSet<string> DepFolderNames = new(
        StringComparer.OrdinalIgnoreCase) { "Dependencies", "deps", "dep" };

    private static readonly Regex DepPattern = new(
        @"(?i)(microsoft\.|vclibs|net\.core|ui\.xaml|net\.native|vcruntime|dotnet|runtime\.)");

    private static readonly Regex JunkPattern = new(
        @"(?i)(\.cer$|\.pfx$|add-appdevpackage|install\.ps1|\.appxsym$|\.psd1$|" +
        @"telemetrydependenc|logsideloading|diagnostics\.tracing|" +
        @"visualstudio\.(remote|telemetry|util)|newtonsoft|system\.runtime\.compiler)");

    private static readonly Regex ArchPattern = new(
        @"(?:^|[\._\-])(arm64|arm|x64|x86|neutral)(?:[\._\-]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> InstallerExts = new(
        StringComparer.OrdinalIgnoreCase) { ".appx", ".msix", ".appxbundle", ".msixbundle" };

    private static bool IsDep(string fileName) => DepPattern.IsMatch(fileName);
    private static bool IsJunk(string fileName) => JunkPattern.IsMatch(fileName);
    private static bool IsInstallable(string fileName) => InstallerExts.Contains(Path.GetExtension(fileName));

    private static string[] FilterByArchitecture(string[] files)
    {
        // Xbox packages are always x64. Use host arch only as fallback,
        // but prefer x64 since that's what the target device runs.
        var targetSuffix = "x64";

        return files.Where(f =>
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var match = ArchPattern.Match(name);
            if (match.Success)
            {
                var fileArch = match.Groups[1].Value.ToLowerInvariant();
                return fileArch == targetSuffix || fileArch == "neutral";
            }

            var pathArch = GetPathArchitecture(f);
            if (pathArch is not null)
                return pathArch == targetSuffix;

            return true;
        }).ToArray();
    }

    private static string? GetPathArchitecture(string filePath)
    {
        var segments = filePath.Replace('\\', '/').Split('/');
        foreach (var seg in segments)
        {
            var segLower = seg.ToLowerInvariant();
            if (segLower is "x64" or "x86" or "arm64" or "arm")
                return segLower;
        }
        return null;
    }

    public PackageInstallService(CacheService cache, IXboxPackageService packageService)
        : this(cache, packageService, http: null, log: null)
    {
    }

    // Back-compat overload used throughout tests and callers: keep three-arg ctor
    public PackageInstallService(CacheService cache, IXboxPackageService packageService, HttpClient? http)
        : this(cache, packageService, http, log: null)
    {
    }

    public PackageInstallService(CacheService cache, IXboxPackageService packageService, HttpClient? http, IAppLogger? log)
    {
        _cache = cache;
        _packageService = packageService;
        _log = log ?? new SerilogAdapter();

        if (http is not null)
        {
            _http = http;
            return;
        }

        // GitHub release downloads redirect (302) to a CDN. Reusing a pooled
        // keep-alive connection that the server has closed causes
        // "The response ended prematurely". Limit pooled connection lifetime
        // so stale connections are recycled before they get reused.
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            AllowAutoRedirect = true
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", $"XB Homebrew Vault/{BuildInfo.Version}");
    }

    public async Task<InstallResult> DownloadAndInstallAsync(
        CatalogItem item,
        string? downloadUrl = null,
        IProgress<InstallProgressInfo>? progress = null)
    {
        var url = downloadUrl ?? item.DownloadUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _log.LogError($"No download URL for {item.Name}");
            return InstallResult.Fail(InstallFailureStage.Download, "No download URL available for this item.");
        }

        progress?.Report(new InstallProgressInfo { Status = $"Starting install of {item.Name}..." });
        _log.Info($"DownloadAndInstall: {item.Name} from {url}");

        var fileName = GetFileNameFromUrl(url);
        var localPath = _cache.GetDownloadPath(item.Id, fileName);
        _log.Debug($"Target local path: {localPath}");

        // Phase 1: Download
        if (_cache.IsCached(item.Id, fileName))
        {
            Logger.Debug($"Cache hit for {item.Id}/{fileName}");
            progress?.Report(new InstallProgressInfo { Total = 0.4, Status = $"Using cached {fileName}" });
        }
        else
        {
            Logger.Info($"Cache miss — downloading {fileName}");
            progress?.Report(new InstallProgressInfo { Total = 0.05, Status = $"Downloading {fileName}..." });

            const int maxAttempts = 3;
            Exception? lastError = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (attempt > 1)
                        Logger.Warn($"Retry {attempt - 1}/{maxAttempts - 1} for download of {fileName}");
                    var response = await _http.GetAsync(url,
                        HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var total = response.Content.Headers.ContentLength ?? -1;
                    _log.Info($"Download size: {(total > 0 ? $"{total} bytes" : "unknown")}");
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = File.Create(localPath);

                    var buffer = new byte[81920];
                    long read = 0;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        read += bytesRead;
                        if (total > 0)
                        {
                            var pct = 0.05 + (0.35 * (double)read / total);
                            progress?.Report(new InstallProgressInfo
                            {
                                Total = pct,
                                Status = $"Downloading {fileName} ({FormatBytes(read)}/{FormatBytes(total)})..."
                            });
                        }
                    }

                    _log.Info($"Downloaded {read} bytes to {localPath}");
                    lastError = null;
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _log.LogError(ex, $"Download attempt {attempt}/{maxAttempts} failed for {url}");
                    if (File.Exists(localPath))
                        File.Delete(localPath);
                    if (attempt < maxAttempts)
                        await Task.Delay(TimeSpan.FromSeconds(1.5) * attempt).ConfigureAwait(false);
                }
            }

            if (lastError is not null)
            {
                _log.LogError(lastError, $"Download failed for {url} after {maxAttempts} attempts");
                return InstallResult.Fail(InstallFailureStage.Download,
                    $"Download failed after {maxAttempts} attempts ({lastError.Message}). The source may be down or your network blocked it.");
            }
        }

        progress?.Report(new InstallProgressInfo { Total = 0.4, Status = "Extracting package..." });

        // Phase 2: Extract ZIP
        _log.Info("Extracting package...");

        var extractDir = GetExtractPath(item.Id, fileName);
        string[] packages;
        try
        {
            packages = ExtractPackage(localPath, extractDir);
            if (packages.Length == 0)
            {
                Logger.Error($"No installable packages found in {localPath}");
                return InstallResult.Fail(InstallFailureStage.Extraction,
                    "No installable package found after extraction. The download may be corrupt or unsupported.");
            }
            _log.Info($"Found {packages.Length} installable file(s):");
            foreach (var p in packages)
                _log.Info($"  {Path.GetFileName(p)}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, $"Extraction failed for {localPath}");
            return InstallResult.Fail(InstallFailureStage.Extraction,
                $"Extraction failed: {ex.Message}");
        }

        // Phase 3: Classify main vs dependencies by name patterns
        _log.Info("Classifying packages (main vs dependencies)...");
        progress?.Report(new InstallProgressInfo { Total = 0.5, Status = "Classifying packages..." });
        var (mainPackage, dependencies) = ClassifyPackages(packages);
        if (mainPackage is null)
        {
            Logger.Error($"No installable main package found in {localPath}");
            return InstallResult.Fail(InstallFailureStage.Extraction,
                "No main package identified after extraction. The download may be corrupt.");
        }
        _log.Info($"  Main: {Path.GetFileName(mainPackage)}");
        for (int i = 0; i < dependencies.Length; i++)
            _log.Info($"  Dep {i + 1}/{dependencies.Length}: {Path.GetFileName(dependencies[i])}");

        // Phase 4: Uninstall conflicting package if different PFN
        _log.Info("Checking for conflicting installed packages...");
        progress?.Report(new InstallProgressInfo { Total = 0.55, Status = "Checking for conflicts..." });

        try
        {
            var installed = await _packageService.GetInstalledPackagesAsync();
            var conflicting = installed.FirstOrDefault(p => VersionCheckerService.IsPackageMatchBasic(item, p));
            if (conflicting is not null)
            {
                var installedPfn = conflicting.PackageFamilyName ?? "unknown";
                var catalogPfn = item.AppId ?? item.Id ?? "unknown";
                if (!string.Equals(installedPfn, catalogPfn, StringComparison.OrdinalIgnoreCase))
                {
                    // Different PFN — catalog item matches this package but with a different identity
                    // (e.g. old XBSX2 PFN "XBSX2" vs new "595c25f0-..."). Uninstall to avoid duplicate.
                    _log.Info($"Found conflict: installed '{conflicting.Name}' (PFN: {installedPfn}) conflicts with catalog '{item.Name}' (PFN: {catalogPfn}). Uninstalling first...");
                    progress?.Report(new InstallProgressInfo { Total = 0.55, Status = $"Removing {conflicting.Name} to avoid duplicate..." });
                    var uninstalled = await _packageService.UninstallPackageAsync(conflicting.FullName);
                    _log.Info(uninstalled
                        ? $"Conflict resolved: {conflicting.Name} uninstalled successfully"
                        : $"Conflict uninstall failed for {conflicting.Name} — proceeding with install anyway");
                }
                else
                {
                    _log.Info($"Same PFN ({installedPfn}) — in-place update, no uninstall needed");
                }
            }
            else
            {
                _log.Info("No conflicting package found — fresh install");
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Conflict detection failed — proceeding with install anyway");
        }

        // Phase 5: Install on Xbox
        _log.Info("Installing on Xbox...");
        progress?.Report(new InstallProgressInfo { Total = 0.6, Status = "Installing on Xbox..." });

        var installProgress = dependencies.Length > 0
            ? new Progress<InstallProgressInfo>(p =>
            {
                var overall = 0.6 + (0.4 * p.Total);
                progress?.Report(new InstallProgressInfo
                {
                    Total = overall,
                    File = p.File,
                    Status = p.Status,
                    CurrentFile = p.CurrentFile
                });
            })
            : new Progress<InstallProgressInfo>(p =>
            {
                progress?.Report(new InstallProgressInfo
                {
                    Total = 0.6 + (0.4 * p.Total),
                    File = p.File,
                    Status = p.Status,
                    CurrentFile = p.CurrentFile
                });
            });

        var result = await _packageService.InstallPackageAsync(mainPackage, dependencies, installProgress);

        if (result)
        {
            progress?.Report(new InstallProgressInfo { Total = 1.0, Status = "Complete!" });
            _log.Info($"Install SUCCESS: {item.Name}");
            _cache.ClearAppCache(item.Id);
            _log.Debug($"Cache cleared for {item.Id} after successful install");
        }
        else
        {
            _log.LogError($"Install FAILED: {item.Name}");
        }
        return result
            ? InstallResult.Ok()
            : InstallResult.Fail(InstallFailureStage.Install,
                "Xbox rejected the install. The package may be incompatible, corrupted during transfer, or the console rejected it. Check the logs for details.");
    }

    private string GetExtractPath(string itemId, string fileName)
    {
        var cacheDir = _cache.GetAppCacheDir(itemId);
        return Path.Combine(cacheDir, $"{Path.GetFileNameWithoutExtension(fileName)}_extracted");
    }

    public static string[] ExtractPackage(string archivePath, string extractDir)
    {
        Logger.Info($"Extracting {archivePath} to {extractDir}");

        if (Directory.Exists(extractDir))
        {
            Logger.Debug("Extract dir exists, checking for valid packages...");
            var existing = FindInstallablePackages(extractDir);
            if (existing.Length > 0)
            {
                Logger.Debug($"Reusing {existing.Length} previously extracted package(s)");
                return existing;
            }
            Logger.Debug("No valid packages found in existing extract dir, re-extracting");
            Directory.Delete(extractDir, true);
        }

        Directory.CreateDirectory(extractDir);

        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, extractDir);
            Logger.Debug("ZIP extraction complete");
        }
        else if (archivePath.EndsWith(".appx", StringComparison.OrdinalIgnoreCase) ||
                 archivePath.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) ||
                 archivePath.EndsWith(".appxbundle", StringComparison.OrdinalIgnoreCase) ||
                 archivePath.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Debug("File is already an installable package");
            File.Copy(archivePath, Path.Combine(extractDir, Path.GetFileName(archivePath)), true);
        }
        else
        {
            Logger.Warn($"Unknown archive type: {archivePath}, trying as ZIP");
            try { ZipFile.ExtractToDirectory(archivePath, extractDir); }
            catch
            {
                Logger.Warn("Not a valid ZIP, copying as-is");
                File.Copy(archivePath, Path.Combine(extractDir, Path.GetFileName(archivePath)), true);
            }
        }

        var standalone = FindInstallablePackages(extractDir);
        Logger.Info($"Found {standalone.Length} standalone packages");

        var extractedFromBundles = ExtractBundles(extractDir);
        Logger.Info($"Extracted {extractedFromBundles.Length} packages from bundles");

        // Merge: bundle contents first (main app), then standalone non-deps, then deps
        var depSubPaths = DepFolderNames
            .Select(n => Path.Combine(extractDir, n))
            .ToArray();
        var allPackages = extractedFromBundles
            .Concat(standalone.Where(f => !depSubPaths.Any(d => f.StartsWith(d, StringComparison.OrdinalIgnoreCase))))
            .Concat(standalone.Where(f => depSubPaths.Any(d => f.StartsWith(d, StringComparison.OrdinalIgnoreCase))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Logger.Info($"Total packages: {allPackages.Length}");
        foreach (var p in allPackages)
            Logger.Debug($"  {Path.GetFileName(p)}");

        return allPackages;
    }

    private static string[] FindInstallablePackages(string directory)
    {
        var results = new List<string>();

        var depSubPaths = DepFolderNames
            .Select(n => Path.Combine(directory, n))
            .ToArray();

        var skipDirs = new HashSet<string>(
            new[] { Path.Combine(directory, "_extracted_bundles") }
                .Concat(depSubPaths),
            StringComparer.OrdinalIgnoreCase);

        string[] allFiles;
        try
        {
            allFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException)
        {
            Logger.Warn($"FindInstallablePackages: access denied scanning {directory}, skipping subdirs");
            try { allFiles = Directory.GetFiles(directory, "*"); }
            catch { return []; }
        }
        catch (PathTooLongException)
        {
            Logger.Warn($"FindInstallablePackages: path too long in {directory}");
            return [];
        }

        foreach (var f in allFiles)
        {
            var parent = Path.GetDirectoryName(f) ?? "";
            if (skipDirs.Any(d => parent.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (IsInstallable(Path.GetFileName(f)))
                results.Add(f);
        }

        // Look for dependency folders (deps/, dep/, Dependencies/)
        foreach (var sub in depSubPaths)
        {
            if (!Directory.Exists(sub)) continue;
            try
            {
                var deps = Directory.GetFiles(sub, "*", SearchOption.AllDirectories)
                    .Where(f => IsInstallable(Path.GetFileName(f)))
                    .ToArray();
                results.AddRange(deps);
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Warn($"FindInstallablePackages: access denied scanning dep folder {sub}");
            }
        }

        results = results.OrderBy(f => Path.GetFileName(f)).ToList();
        return FilterByArchitecture(results.ToArray());
    }

    public static string[] ExtractBundles(string directory)
    {
        var bundles = Directory.GetFiles(directory, "*.appxbundle", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(directory, "*.msixbundle", SearchOption.TopDirectoryOnly))
            .ToArray();

        if (bundles.Length == 0)
            return [];

        // Bundles (msixbundle/appxbundle) are self-contained — they already include
        // all dependencies inside. Return them directly instead of extracting inner
        // appx/msix files, which would incorrectly split deps out of the bundle.
        foreach (var b in bundles)
            Logger.Debug($"Bundle (self-contained, no extraction needed): {Path.GetFileName(b)}");

        return FilterByArchitecture(bundles);
    }

    public static (string? main, string[] deps) ClassifyPackages(string[] files)
    {
        var candidates = new List<string>();
        var deps = new List<string>();

        foreach (var f in files)
        {
            var name = Path.GetFileName(f);
            if (IsJunk(name))
            {
                Logger.Debug($"  Junk filtered: {name}");
                continue;
            }
            if (IsDep(name))
            {
                Logger.Info($"  Dependency: {name}");
                deps.Add(f);
            }
            else if (IsInstallable(name))
            {
                Logger.Info($"  Main candidate: {name}");
                candidates.Add(f);
            }
            else
            {
                Logger.Debug($"  Skipped (not installable): {name}");
            }
        }

        deps = deps.OrderBy(f => Path.GetFileName(f)).ToList();

        // Pick main: prefer bundle formats over flat .appx/.msix
        var main = candidates.OrderBy(f =>
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            var bundleRank = ext is ".msixbundle" or ".appxbundle" ? 0 : 1;
            return (bundleRank, Path.GetFileName(f));
        }).FirstOrDefault();

        if (main is null && candidates.Count == 0 && deps.Count > 0)
        {
            // No non-dep candidates found — maybe all files are deps.
            // Use first dep as main as last resort.
            Logger.Warn("No main candidate found, using first dependency as main");
            main = deps[0];
            deps = deps.Skip(1).ToList();
        }

        return (main, deps.ToArray());
    }

    public static string[] GetInstallableFiles(string directory)
    {
        var packages = FindInstallablePackages(directory);
        var bundles = ExtractBundles(directory);
        var all = packages.Concat(bundles).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return FilterByArchitecture(all);
    }

    public static AnalyzeResult AnalyzeLocalFile(string filePath)
    {
        var extractDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XBVault", "analysis", Guid.NewGuid().ToString("N"));
        var packages = ExtractPackage(filePath, extractDir);

        var isZip = filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        var scanDir = isZip ? extractDir : Path.GetDirectoryName(filePath);

        if (scanDir is not null && Directory.Exists(scanDir))
        {
            var siblings = GetInstallableFiles(scanDir)
                .Where(f => !f.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (siblings.Length > 0)
                packages = packages.Concat(siblings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        var (main, deps) = ClassifyPackages(packages);
        return new AnalyzeResult(packages, main, deps, extractDir);
    }

    public static AnalyzeResult AnalyzeDirectory(string directory)
    {
        var all = GetInstallableFiles(directory);
        var (main, deps) = ClassifyPackages(all);
        return new AnalyzeResult(all, main, deps, directory);
    }

    private static string FormatBytes(long bytes)
    {
        // InvariantCulture: "1.5 GB" regardless of pt-BR comma vs en-US dot
        string[] units = ["B", "KB", "MB", "GB"];
        double n = bytes;
        foreach (var u in units)
        {
            if (n < 1024) return $"{n.ToString("F1", CultureInfo.InvariantCulture)}{u}";
            n /= 1024;
        }
        return $"{n.ToString("F1", CultureInfo.InvariantCulture)}TB";
    }

    public static string GetFileNameFromUrl(string url)
    {
        var uri = new Uri(url);
        var fileName = Path.GetFileName(uri.LocalPath);
        return string.IsNullOrWhiteSpace(fileName) ? "package.appx" : fileName;
    }
}

public class AnalyzeResult
{
    public string[] AllFiles { get; }
    public string? MainPackage { get; }
    public string[] Dependencies { get; }
    public string WorkingDirectory { get; }

    public AnalyzeResult(string[] allFiles, string? mainPackage, string[] dependencies, string workingDirectory)
    {
        AllFiles = allFiles;
        MainPackage = mainPackage;
        Dependencies = dependencies;
        WorkingDirectory = workingDirectory;
    }
}

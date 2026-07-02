using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XBVault.Helpers;
using XBVault.Models;

#pragma warning disable CA1001 // HttpClient is long-lived singleton

namespace XBVault.Services;

public class PackageInstallService
{
    private readonly HttpClient _http;
    private readonly CacheService _cache;
    private readonly XboxDeviceService _xbox;

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
        var targetArch = RuntimeInformation.ProcessArchitecture;
        var targetSuffix = targetArch switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => null
        };

        return files.Where(f =>
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var match = ArchPattern.Match(name);
            if (!match.Success)
                return true;

            var fileArch = match.Groups[1].Value.ToLowerInvariant();
            return fileArch == targetSuffix || fileArch == "neutral";
        }).ToArray();
    }

    public PackageInstallService(CacheService cache, XboxDeviceService xbox)
    {
        _cache = cache;
        _xbox = xbox;
        _http = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", $"XB Homebrew Vault/{BuildInfo.Version}");
    }

    public async Task<bool> DownloadAndInstallAsync(
        CatalogItem item,
        string? downloadUrl = null,
        IProgress<InstallProgressInfo>? progress = null)
    {
        var url = downloadUrl ?? item.DownloadUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            Logger.Error($"No download URL for {item.Name}");
            return false;
        }

        progress?.Report(new InstallProgressInfo { Status = $"Starting install of {item.Name}..." });
        Logger.Info($"DownloadAndInstall: {item.Name} from {url}");

        var fileName = GetFileNameFromUrl(url);
        var localPath = _cache.GetDownloadPath(item.Id, fileName);
        Logger.Debug($"Target local path: {localPath}");

        // Phase 1: Download
        if (_cache.IsCached(item.Id, fileName))
        {
            Logger.Debug($"Cache hit for {item.Id}/{fileName}");
            progress?.Report(new InstallProgressInfo { Total = 0.4, Status = $"Using cached {fileName}" });
        }
        else
        {
            Logger.Debug($"Cache miss — downloading {fileName}");
            progress?.Report(new InstallProgressInfo { Total = 0.05, Status = $"Downloading {fileName}..." });

            try
            {
                var response = await _http.GetAsync(url,
                    HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1;
                Logger.Info($"Download size: {(total > 0 ? $"{total} bytes" : "unknown")}");
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

                Logger.Info($"Downloaded {read} bytes to {localPath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Download failed for {url}");
                if (File.Exists(localPath))
                    File.Delete(localPath);
                return false;
            }
        }

        progress?.Report(new InstallProgressInfo { Total = 0.4, Status = "Extracting package..." });

        // Phase 2: Extract ZIP
        Logger.Info("Extracting package...");

        var extractDir = GetExtractPath(item.Id, fileName);
        string[] packages;
        try
        {
            packages = ExtractPackage(localPath, extractDir);
            if (packages.Length == 0)
            {
                Logger.Error($"No installable packages found in {localPath}");
                return false;
            }
            Logger.Info($"Found {packages.Length} installable file(s):");
            foreach (var p in packages)
                Logger.Info($"  {Path.GetFileName(p)}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Extraction failed for {localPath}");
            return false;
        }

        // Phase 3: Classify main vs dependencies by name patterns
        Logger.Info("Classifying packages (main vs dependencies)...");
        progress?.Report(new InstallProgressInfo { Total = 0.5, Status = "Classifying packages..." });
        var (mainPackage, dependencies) = ClassifyPackages(packages);
        if (mainPackage is null)
        {
            Logger.Error($"No installable main package found in {localPath}");
            return false;
        }
        Logger.Info($"  Main: {Path.GetFileName(mainPackage)}");
        for (int i = 0; i < dependencies.Length; i++)
            Logger.Info($"  Dep {i + 1}/{dependencies.Length}: {Path.GetFileName(dependencies[i])}");

        // Phase 4: Install on Xbox
        Logger.Info("Installing on Xbox...");
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

        var result = await _xbox.InstallPackageAsync(mainPackage, dependencies, installProgress);

        if (result)
        {
            progress?.Report(new InstallProgressInfo { Total = 1.0, Status = "Complete!" });
            Logger.Info($"Install SUCCESS: {item.Name}");
            _cache.ClearAppCache(item.Id);
            Logger.Debug($"Cache cleared for {item.Id} after successful install");
        }
        else
        {
            Logger.Error($"Install FAILED: {item.Name}");
        }
        return result;
    }

    private string GetExtractPath(string itemId, string fileName)
    {
        var cacheDir = _cache.GetAppCacheDir(itemId);
        return Path.Combine(cacheDir, $"{Path.GetFileNameWithoutExtension(fileName)}_extracted");
    }

    public static string[] ExtractPackage(string archivePath, string extractDir)
    {
        Logger.Debug($"Extracting {archivePath} to {extractDir}");

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
        Logger.Debug($"Found {standalone.Length} standalone packages");

        var extractedFromBundles = ExtractBundles(extractDir);
        Logger.Debug($"Extracted {extractedFromBundles.Length} packages from bundles");

        // Merge: bundle contents first (main app), then standalone non-deps, then deps
        var depSubPaths = DepFolderNames
            .Select(n => Path.Combine(extractDir, n))
            .ToArray();
        var allPackages = extractedFromBundles
            .Concat(standalone.Where(f => !depSubPaths.Any(d => f.StartsWith(d, StringComparison.OrdinalIgnoreCase))))
            .Concat(standalone.Where(f => depSubPaths.Any(d => f.StartsWith(d, StringComparison.OrdinalIgnoreCase))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Logger.Debug($"Total packages: {allPackages.Length}");
        foreach (var p in allPackages)
            Logger.Debug($"  {Path.GetFileName(p)}");

        return allPackages;
    }

    private static string[] FindInstallablePackages(string directory)
    {
        var results = new List<string>();

        var allFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

        var depSubPaths = DepFolderNames
            .Select(n => Path.Combine(directory, n))
            .ToArray();

        var skipDirs = new HashSet<string>(
            new[] { Path.Combine(directory, "_extracted_bundles") }
                .Concat(depSubPaths),
            StringComparer.OrdinalIgnoreCase);

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
            if (Directory.Exists(sub))
            {
                var deps = Directory.GetFiles(sub, "*", SearchOption.AllDirectories)
                    .Where(f => IsInstallable(Path.GetFileName(f)))
                    .ToArray();
                results.AddRange(deps);
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

        var extracted = new List<string>();
        var outDir = Path.Combine(directory, "_extracted_bundles");

        foreach (var bundle in bundles)
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(bundle);
                var bundleOut = Path.Combine(outDir, name);
                if (!Directory.Exists(bundleOut))
                {
                    Directory.CreateDirectory(bundleOut);
                    ZipFile.ExtractToDirectory(bundle, bundleOut);
                    Logger.Debug($"Extracted bundle {Path.GetFileName(bundle)} → {bundleOut}");
                }
                var inner = Directory.GetFiles(bundleOut, "*.appx", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(bundleOut, "*.msix", SearchOption.AllDirectories))
                    .ToArray();
                extracted.AddRange(inner);
                foreach (var f in inner)
                    Logger.Debug($"  Bundle content: {Path.GetFileName(f)}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to extract bundle {bundle}: {ex.Message}");
            }
        }

        return FilterByArchitecture(extracted.ToArray());
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
                Logger.Debug($"  Dependency: {name}");
                deps.Add(f);
            }
            else if (IsInstallable(name))
            {
                Logger.Debug($"  Main candidate: {name}");
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

        // Scan sibling files in parent directory for additional deps
        var parentDir = Path.GetDirectoryName(filePath);
        if (parentDir is not null && Directory.Exists(parentDir))
        {
            var siblings = GetInstallableFiles(parentDir)
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
        string[] units = ["B", "KB", "MB", "GB"];
        double n = bytes;
        foreach (var u in units)
        {
            if (n < 1024) return $"{n:F1}{u}";
            n /= 1024;
        }
        return $"{n:F1}TB";
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

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public record SelectableDep
{
    public string FilePath { get; init; } = "";
    public string BaseDirectory { get; init; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public string DisplayName { get; set; }
    public string RelativePath => MakeRelative(FilePath, BaseDirectory);
    public bool IsSelected { get; set; } = true;

    public SelectableDep()
    {
        DisplayName = FileName;
    }

    private static string MakeRelative(string fullPath, string basePath)
    {
        if (string.IsNullOrEmpty(basePath))
            return fullPath;
        try
        {
            var baseUri = new Uri(basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
            var fileUri = new Uri(fullPath);
            if (baseUri.Scheme != fileUri.Scheme)
                return fullPath;
            var relative = baseUri.MakeRelativeUri(fileUri).ToString();
            return Uri.UnescapeDataString(relative).Replace('/', Path.DirectorySeparatorChar);
        }
        catch
        {
            return fullPath;
        }
    }
}

public partial class CustomInstallViewModel : ObservableObject
{
    private const int MinProgressMs = 1000;
    // Give the console a beat after uninstall/conflict before resuming install
    private const int UninstallRetryDelayMs = 1500;

    private readonly IXboxPackageService _packageService;
    private readonly PackageInstallService _installService;
    private static readonly HttpClient _http = new();

    private AnalyzeResult? _analysis;
    private string? _downloadedFile;

    public Func<Task<string?>>? PickFileAsync { get; set; }
    public Func<Task<string[]?>>? PickDependencyFilesAsync { get; set; }
    public Action? CloseAction { get; set; }
    public Action? OnInstallComplete { get; set; }

    public CustomInstallViewModel(IXboxPackageService packageService, PackageInstallService installService)
    {
        _packageService = packageService;
        _installService = installService;
    }

    [ObservableProperty]
    private int _currentStep;

    public static string[] StepLabels => ["Source", "Analysis", "Dependencies", "Install"];

    [ObservableProperty]
    private bool _useFileSource = true;

    [ObservableProperty]
    private string? _sourcePath;

    [ObservableProperty]
    private string? _sourceUrl;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isAnalyzing;

    [ObservableProperty]
    private string? _analysisResultText;

    public string? MainPackageName => _analysis?.MainPackage is not null ? Path.GetFileName(_analysis.MainPackage) : null;

    public int DependencyCount => DepItems.Count;

    public string DependencyText
    {
        get
        {
            var c = DependencyCount;
            return c == 0 ? "No dependencies" : $"{c} dependenc{(c == 1 ? "y" : "ies")}";
        }
    }

    public ObservableCollection<string> FileList { get; } = [];

    public ObservableCollection<SelectableDep> DepItems { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isInstalling;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private double _packageProgress;

    [ObservableProperty]
    private string? _installStatus;

    [ObservableProperty]
    private string? _packageStatus;

    [ObservableProperty]
    private string? _currentFile;

    [ObservableProperty]
    private bool _installComplete;

    [ObservableProperty]
    private string? _installResultMessage;

    [ObservableProperty]
    private bool _installSuccess;

    [ObservableProperty]
    private bool _performCleanInstall;

    public Cursor? Cursor => (IsAnalyzing || IsInstalling) ? AppStartingCursor : null;

    private static readonly Cursor AppStartingCursor = new(StandardCursorType.AppStarting);

    public bool CanGoNext => CurrentStep switch
    {
        0 => !string.IsNullOrEmpty(SourcePath) || !string.IsNullOrEmpty(SourceUrl),
        1 => _analysis is not null,
        2 => _analysis?.MainPackage is not null,
        _ => false
    };

    public bool CanGoBack => CurrentStep > 0 && !IsAnalyzing && !IsInstalling;
    public bool CanCancel => !IsAnalyzing && !IsInstalling && !InstallComplete;

    public bool IsSourceStep => CurrentStep == 0;
    public bool IsAnalysisStep => CurrentStep == 1;
    public bool IsConfirmStep => CurrentStep == 2;
    public bool IsInstallStep => CurrentStep == 3;
    public bool IsSummaryVisible => !IsInstalling && !InstallComplete;
    public bool CanShowInstallButton => IsInstallStep && !InstallComplete;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsSourceStep));
        OnPropertyChanged(nameof(IsAnalysisStep));
        OnPropertyChanged(nameof(IsConfirmStep));
        OnPropertyChanged(nameof(IsInstallStep));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanShowInstallButton));
    }

    partial void OnIsAnalyzingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoBack));
    }

    partial void OnIsInstallingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsSummaryVisible));
    }

    partial void OnInstallCompleteChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(IsSummaryVisible));
        OnPropertyChanged(nameof(CanShowInstallButton));
    }

    partial void OnUseFileSourceChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoNext));
    }

    partial void OnSourcePathChanged(string? value)
    {
        OnPropertyChanged(nameof(CanGoNext));
    }

    partial void OnSourceUrlChanged(string? value)
    {
        OnPropertyChanged(nameof(CanGoNext));
    }

    [RelayCommand]
    private async Task BrowseFileAsync()
    {
        if (PickFileAsync is null)
        {
            Logger.Warn("BrowseFileAsync: PickFileAsync is null");
            return;
        }
        try
        {
            var path = await PickFileAsync();
            if (string.IsNullOrEmpty(path))
            {
                Logger.Trace("BrowseFileAsync: user cancelled file picker");
                return;
            }
            Logger.Info($"BrowseFileAsync: selected file — {path}");
            SourcePath = path;
            var fi = new FileInfo(path);
            StatusText = fi.Exists
                ? $"Selected: {fi.Name} ({fi.Length / 1024} KB)"
                : $"Selected: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "BrowseFileAsync: file picker failed");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (CurrentStep != 0) return;

        Logger.Info($"AnalyzeAsync: starting — source={UseFileSource}, path={SourcePath}, url={SourceUrl}");

        // Full state reset — treat as fresh start
        Cleanup();
        _analysis = null;
        _downloadedFile = null;
        FileList.Clear();
        DepItems.Clear();
        AnalysisResultText = null;
        OnPropertyChanged(nameof(MainPackageName));
        OnPropertyChanged(nameof(DependencyCount));
        OnPropertyChanged(nameof(DependencyText));

        IsAnalyzing = true;
        StatusText = "Analyzing package...";
        Logger.Debug($"AnalyzeAsync: step 0 → 1 (analysis)");
        CurrentStep = 1;

        try
        {
            var useFile = !string.IsNullOrWhiteSpace(SourcePath);
            if (useFile)
            {
                if (string.IsNullOrWhiteSpace(SourcePath))
                {
                    Logger.Warn("AnalyzeAsync: no file path provided");
                    AnalysisResultText = "No file selected.";
                    Logger.Debug($"AnalyzeAsync: step 1 → 2 (empty path, no analysis)");
                    CurrentStep = 2;
                    return;
                }
                Logger.Debug($"AnalyzeAsync: local file mode — {SourcePath}");
                StatusText = "Analyzing local file...";
                await Task.Run(() => AnalyzeLocalFile(SourcePath));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(SourceUrl))
                {
                    Logger.Warn("AnalyzeAsync: no URL provided");
                    AnalysisResultText = "No URL entered.";
                    Logger.Debug($"AnalyzeAsync: step 1 → 2 (empty URL, no analysis)");
                    CurrentStep = 2;
                    return;
                }
                if (!Uri.TryCreate(SourceUrl, UriKind.Absolute, out var parsedUri) ||
                    (parsedUri.Scheme != "http" && parsedUri.Scheme != "https"))
                {
                    Logger.Warn($"AnalyzeAsync: invalid URL — {SourceUrl}");
                    AnalysisResultText = "Invalid URL. Must start with http:// or https://";
                    Logger.Debug($"AnalyzeAsync: step 1 → 2 (invalid URL, no analysis)");
                    CurrentStep = 2;
                    return;
                }
                Logger.Debug($"AnalyzeAsync: download mode — {SourceUrl}");
                StatusText = "Resolving URL...";
                var (resolvedUrl, suggestedName) = await UrlResolverService.ResolveAsync(SourceUrl);
                if (resolvedUrl != SourceUrl)
                    Logger.Info($"AnalyzeAsync: URL resolved — {SourceUrl} → {resolvedUrl}");
                StatusText = "Downloading and analyzing...";
                await DownloadAndAnalyzeAsync(resolvedUrl, suggestedName);
            }

            if (_analysis is not null)
            {
                Logger.Info($"AnalyzeAsync: success — main={Path.GetFileName(_analysis.MainPackage)}, deps={_analysis.Dependencies?.Length ?? 0}, total={_analysis.AllFiles.Length}");

                // UI updates must run on UI thread
                await XBVault.Helpers.UIHelpers.RunOnUIAsync(() =>
                {
                    FileList.Clear();
                    foreach (var f in _analysis.AllFiles)
                        FileList.Add($"  {Path.GetFileName(f)}");

                    DepItems.Clear();
                    foreach (var d in _analysis.Dependencies ?? [])
                        DepItems.Add(new SelectableDep { FilePath = d, BaseDirectory = _analysis.WorkingDirectory, IsSelected = true });

                    DisambiguateDepNames();

                    var main = _analysis.MainPackage is not null ? Path.GetFileName(_analysis.MainPackage) : "None";
                    var depCount = _analysis.Dependencies?.Length ?? 0;
                    AnalysisResultText = $"Main: {main}\nDependencies: {depCount}";
                    OnPropertyChanged(nameof(MainPackageName));
                    OnPropertyChanged(nameof(DependencyCount));
                    OnPropertyChanged(nameof(DependencyText));
                    OnPropertyChanged(nameof(CanGoNext));

                    Logger.Debug($"AnalyzeAsync: step 1 → 2 (review)");
                    CurrentStep = 2;
                    return Task.CompletedTask;
                });
            }
            else
            {
                Logger.Warn("AnalyzeAsync: no installable packages found after analysis");
                await XBVault.Helpers.UIHelpers.RunOnUIAsync(() =>
                {
                    AnalysisResultText = "Analysis failed — no installable packages found.";
                    OnPropertyChanged(nameof(CanGoNext));
                    Logger.Debug($"AnalyzeAsync: step 1 → 2 (analysis failed)");
                    CurrentStep = 2;
                    return Task.CompletedTask;
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "AnalyzeAsync: analysis failed with exception");
            await XBVault.Helpers.UIHelpers.RunOnUIAsync(() =>
            {
                AnalysisResultText = $"Error: {ex.Message}";
                Logger.Debug($"AnalyzeAsync: step 1 → 2 (exception)");
                CurrentStep = 2;
                return Task.CompletedTask;
            });
        }
        finally
        {
            IsAnalyzing = false;
            Logger.Trace("AnalyzeAsync: completed");
        }
    }

    private void AnalyzeLocalFile(string path)
    {
        if (Directory.Exists(path))
        {
            Logger.Debug($"AnalyzeLocalFile: analyzing directory — {path}");
            _analysis = PackageInstallService.AnalyzeDirectory(path);
        }
        else
        {
            Logger.Debug($"AnalyzeLocalFile: analyzing file — {path} (exists={File.Exists(path)})");
            _analysis = PackageInstallService.AnalyzeLocalFile(path);
        }

        if (_analysis is not null)
        {
            Logger.Debug($"AnalyzeLocalFile: result — main={Path.GetFileName(_analysis.MainPackage)}, deps={_analysis.Dependencies?.Length ?? 0}, files={_analysis.AllFiles.Length}, workDir={_analysis.WorkingDirectory}");
            if (_analysis.AllFiles.Length > 0)
            {
                foreach (var f in _analysis.AllFiles)
                    Logger.Debug($"  AnalyzeLocalFile: file: {f}");
            }
            else
            {
                Logger.Warn($"AnalyzeLocalFile: analysis returned 0 files for {path}");
            }
        }
        else
        {
            Logger.Warn($"AnalyzeLocalFile: null result for {path}");
        }

        if (_analysis?.AllFiles.Length == 0)
        {
            Logger.Warn($"AnalyzeLocalFile: clearing _analysis — AllFiles is empty for {path}");
            _analysis = null;
        }
    }

    private async Task DownloadAndAnalyzeAsync(string url, string? suggestedFileName = null)
    {
        Logger.Info($"DownloadAndAnalyzeAsync: starting download from {url}");
        StatusText = "Downloading...";
        var fileName = suggestedFileName ?? PackageInstallService.GetFileNameFromUrl(url);
        var tempDir = Path.Combine(Path.GetTempPath(), "XBVault", "custom");
        Directory.CreateDirectory(tempDir);
        var localPath = Path.Combine(tempDir, fileName);
        Logger.Debug($"DownloadAndAnalyzeAsync: fileName={fileName}, tempDir={tempDir}, localPath={localPath}");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";
            Logger.Debug($"DownloadAndAnalyzeAsync: HTTP {(int)response.StatusCode}, contentType={contentType}, contentLength={totalBytes}");

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fs = File.Create(localPath))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;
                }

                sw.Stop();
                Logger.Debug($"DownloadAndAnalyzeAsync: download complete — {totalRead} bytes in {sw.ElapsedMilliseconds}ms ({(totalRead > 0 && sw.ElapsedMilliseconds > 0 ? $"{totalRead / 1024.0 / (sw.ElapsedMilliseconds / 1000.0):F1} KB/s" : "N/A")})");
            }

            var fileInfo = new FileInfo(localPath);
            var ext = Path.GetExtension(localPath);
            Logger.Debug($"DownloadAndAnalyzeAsync: saved {fileInfo.Length} bytes to {localPath} (ext={ext})");

            if (string.Equals(ext, ".html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".htm", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warn($"DownloadAndAnalyzeAsync: downloaded file has suspicious extension '{ext}' — may not be a valid package");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            Logger.Error(ex, $"DownloadAndAnalyzeAsync: download failed for {url} after {sw.ElapsedMilliseconds}ms");
            StatusText = $"Download failed: {ex.Message}";
            throw;
        }

        _downloadedFile = localPath;
        Logger.Debug($"DownloadAndAnalyzeAsync: starting analysis of {localPath}");
        AnalyzeLocalFile(localPath);
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStep > 0)
        {
            if (CurrentStep == 3)
            {
                IsInstalling = false;
                InstallComplete = false;
            }
            var prev = CurrentStep;
            CurrentStep = CurrentStep switch
            {
                2 => 0,
                _ => CurrentStep - 1
            };

            // Reset all state when going back to source step
            if (CurrentStep == 0 && prev != 0)
            {
                Cleanup();
                _analysis = null;
                _downloadedFile = null;
                SourcePath = null;
                SourceUrl = null;
                StatusText = null;
                AnalysisResultText = null;
                FileList.Clear();
                DepItems.Clear();
                OnPropertyChanged(nameof(MainPackageName));
                OnPropertyChanged(nameof(DependencyCount));
                OnPropertyChanged(nameof(DependencyText));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    [RelayCommand]
    private async Task GoNextAsync()
    {
        if (CurrentStep < 3)
        {
            CurrentStep++;
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        var analysis = _analysis;
        if (analysis?.MainPackage is null)
        {
            Logger.Warn("InstallAsync: no analysis or main package, aborting");
            return;
        }

        Logger.Info($"InstallAsync: starting — main={Path.GetFileName(analysis.MainPackage)}, deps={DepItems.Count}, cleanInstall={PerformCleanInstall}");
        IsInstalling = true;
        InstallComplete = false;
        InstallProgress = 0;
        PackageProgress = 0;
        InstallResultMessage = null;
        PackageStatus = "Starting...";
        InstallStatus = "Starting...";
        CurrentFile = Path.GetFileName(analysis.MainPackage);

        var progress = new Progress<InstallProgressInfo>(info =>
        {
            InstallProgress = info.Total;
            PackageProgress = info.File;
            PackageStatus = info.Status;
            InstallStatus = info.Status;
            CurrentFile = info.CurrentFile;
        });

        var startTime = DateTime.UtcNow;

        // Clean install: uninstall existing version first
        if (PerformCleanInstall)
        {
            var identityName = ExtractPackageIdentity(analysis.MainPackage);
            Logger.Info($"InstallAsync: cleanInstall identity={identityName ?? "null"}");
            if (!string.IsNullOrEmpty(identityName))
            {
                try
                {
                    InstallStatus = "Checking for existing version...";
                    var packages = await _packageService.GetInstalledPackagesAsync();
                    var existing = packages.FirstOrDefault(p =>
                        string.Equals(p.Name, identityName, StringComparison.OrdinalIgnoreCase));

                    if (existing is not null)
                    {
                        Logger.Info($"InstallAsync: found existing version — {existing.Name} v{existing.Version}, uninstalling...");
                        InstallStatus = $"Uninstalling {existing.DisplayName ?? existing.Name}...";
                        var uninstalled = await _packageService.UninstallPackageAsync(existing.FullName);
                        if (!uninstalled)
                        {
                            Logger.Warn("InstallAsync: uninstall returned false, continuing anyway");
                            InstallStatus = "Warning: uninstall failed, continuing anyway...";
                            await Task.Delay(UninstallRetryDelayMs);
                        }
                        else
                        {
                            Logger.Debug("InstallAsync: uninstall succeeded");
                        }
                    }
                    else
                    {
                        Logger.Debug($"InstallAsync: no existing version of {identityName} found");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Clean install: failed to check/uninstall existing package");
                    InstallStatus = "Warning: could not check for existing version...";
                    await Task.Delay(UninstallRetryDelayMs);
                }
            }
        }

        var selectedDeps = DepItems
            .Select(d => d.FilePath)
            .ToArray();
        Logger.Info($"InstallAsync: sending {selectedDeps.Length} selected deps to Xbox");

        var result = await _packageService.InstallPackageAsync(
            analysis.MainPackage,
            selectedDeps,
            progress);

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        if (elapsed < MinProgressMs)
            await Task.Delay(MinProgressMs - (int)elapsed);

        InstallComplete = true;
        InstallSuccess = result;

        if (result)
        {
            Logger.Info($"InstallAsync: SUCCESS — {Path.GetFileName(analysis.MainPackage)} installed");
            InstallStatus = "Complete!";
            InstallResultMessage = null;
            OnInstallComplete?.Invoke();
        }
        else
        {
            Logger.Error($"InstallAsync: FAILED — {Path.GetFileName(analysis.MainPackage)}");
            InstallStatus = "Install failed";
            InstallResultMessage = "Install failed";
        }

        InstallProgress = result ? 1.0 : 0;
        IsInstalling = false;
        Cleanup();
    }

    [RelayCommand]
    private async Task AddDepAsync()
    {
        if (PickDependencyFilesAsync is null) return;
        try
        {
            var paths = await PickDependencyFilesAsync();
            if (paths is null || paths.Length == 0) return;
            foreach (var path in paths)
            {
                if (DepItems.Any(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase))) continue;
                var dir = Path.GetDirectoryName(path) ?? "";
                DepItems.Add(new SelectableDep { FilePath = path, BaseDirectory = dir, IsSelected = true });
            }
            DisambiguateDepNames();
            OnPropertyChanged(nameof(DependencyCount));
            OnPropertyChanged(nameof(DependencyText));
        }
        catch (Exception ex)
        {
            StatusText = $"Error adding dependency: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveDep(SelectableDep dep)
    {
        DepItems.Remove(dep);
        DisambiguateDepNames();
        OnPropertyChanged(nameof(DependencyCount));
        OnPropertyChanged(nameof(DependencyText));
    }

    private void DisambiguateDepNames()
    {
        var groups = DepItems
            .GroupBy(d => d.FileName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToHashSet();

        foreach (var dep in DepItems)
        {
            if (groups.Contains(dep))
            {
                var parent = Path.GetFileName(Path.GetDirectoryName(dep.FilePath)) ?? "";
                dep.DisplayName = string.IsNullOrEmpty(parent)
                    ? dep.FileName
                    : $"{parent}/{dep.FileName}";
            }
            else
            {
                dep.DisplayName = dep.FileName;
            }
        }
    }

    [RelayCommand]
    private void Close() => CloseAction?.Invoke();

    [RelayCommand]
    private void Cancel()
    {
        Cleanup();
        CloseAction?.Invoke();
    }

    private void Cleanup()
    {
        Logger.Trace("Cleanup: removing temp files");
        if (_downloadedFile is not null && File.Exists(_downloadedFile))
        {
            try
            {
                File.Delete(_downloadedFile);
                Logger.Debug($"Cleanup: deleted downloaded file {_downloadedFile}");
            }
            catch (Exception ex)
            {
                Logger.Trace($"Cleanup: failed to delete downloaded file: {ex.Message}");
            }
        }
        if (_analysis?.WorkingDirectory is not null && Directory.Exists(_analysis.WorkingDirectory))
        {
            try
            {
                Directory.Delete(_analysis.WorkingDirectory, true);
                Logger.Debug($"Cleanup: deleted working dir {_analysis.WorkingDirectory}");
            }
            catch (Exception ex)
            {
                Logger.Trace($"Cleanup: failed to delete working dir: {ex.Message}");
            }
        }
    }

    private static string? ExtractPackageIdentity(string? packagePath)
    {
        if (string.IsNullOrEmpty(packagePath) || !File.Exists(packagePath))
            return null;

        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var manifestEntry = archive.GetEntry("AppxManifest.xml");
            if (manifestEntry is null) return null;

            using var stream = manifestEntry.Open();
            var doc = XDocument.Load(stream);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            var identity = doc.Root?.Element(ns + "Identity");
            return identity?.Attribute("Name")?.Value;
        }
        catch
        {
            return null;
        }
    }
}

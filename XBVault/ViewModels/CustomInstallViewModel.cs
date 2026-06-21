using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public record SelectableDep
{
    public string FilePath { get; init; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public bool IsSelected { get; set; } = true;
}

public partial class CustomInstallViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;
    private readonly PackageInstallService _installService;
    private static readonly HttpClient _http = new();

    private AnalyzeResult? _analysis;
    private string? _downloadedFile;

    public Func<Task<string?>>? PickFileAsync;
    public Func<Task<string[]?>>? PickDependencyFilesAsync;
    public Action? CloseAction;

    public CustomInstallViewModel(XboxDeviceService xboxService, PackageInstallService installService)
    {
        _xboxService = xboxService;
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

    public bool CanGoNext => CurrentStep switch
    {
        0 => UseFileSource ? !string.IsNullOrEmpty(SourcePath) : !string.IsNullOrEmpty(SourceUrl),
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
        if (PickFileAsync is null) return;
        try
        {
            var path = await PickFileAsync();
            if (string.IsNullOrEmpty(path)) return;
            SourcePath = path;
            var fi = new FileInfo(path);
            StatusText = fi.Exists
                ? $"Selected: {fi.Name} ({fi.Length / 1024} KB)"
                : $"Selected: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (CurrentStep != 0) return;

        IsAnalyzing = true;
        StatusText = "Analyzing package...";
        CurrentStep = 1;

        try
        {
            if (UseFileSource)
            {
                await Task.Run(() => AnalyzeLocalFile(SourcePath!));
            }
            else
            {
                await DownloadAndAnalyzeAsync(SourceUrl!);
            }

            if (_analysis is not null)
            {
                FileList.Clear();
                foreach (var f in _analysis.AllFiles)
                    FileList.Add($"  {Path.GetFileName(f)}");

                DepItems.Clear();
                foreach (var d in _analysis.Dependencies ?? [])
                    DepItems.Add(new SelectableDep { FilePath = d, IsSelected = true });

                var main = _analysis.MainPackage is not null ? Path.GetFileName(_analysis.MainPackage) : "None";
                var depCount = _analysis.Dependencies?.Length ?? 0;
                AnalysisResultText = $"Main: {main}\nDependencies: {depCount}";
                OnPropertyChanged(nameof(MainPackageName));
                OnPropertyChanged(nameof(DependencyCount));
                OnPropertyChanged(nameof(DependencyText));
                OnPropertyChanged(nameof(CanGoNext));

                CurrentStep = 2;
            }
            else
            {
                AnalysisResultText = "Analysis failed — no installable packages found.";
                CurrentStep = 2;
            }
        }
        catch (Exception ex)
        {
            AnalysisResultText = $"Error: {ex.Message}";
            CurrentStep = 2;
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    private void AnalyzeLocalFile(string path)
    {
        if (Directory.Exists(path))
        {
            _analysis = PackageInstallService.AnalyzeDirectory(path);
        }
        else
        {
            _analysis = PackageInstallService.AnalyzeLocalFile(path);
        }

        if (_analysis?.AllFiles.Length == 0)
            _analysis = null;
    }

    private async Task DownloadAndAnalyzeAsync(string url)
    {
        StatusText = "Downloading...";
        var fileName = PackageInstallService.GetFileNameFromUrl(url);
        var tempDir = Path.Combine(Path.GetTempPath(), "XBVault", "custom");
        Directory.CreateDirectory(tempDir);
        var localPath = Path.Combine(tempDir, fileName);

        var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var fs = File.Create(localPath);
        await stream.CopyToAsync(fs);
        await fs.FlushAsync();

        _downloadedFile = localPath;
        AnalyzeLocalFile(localPath);
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStep > 0)
            CurrentStep--;
    }

    [RelayCommand]
    private void GoNext()
    {
        if (CurrentStep < 3)
            CurrentStep++;
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        var analysis = _analysis;
        if (analysis?.MainPackage is null) return;

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

        var selectedDeps = DepItems
            .Select(d => d.FilePath)
            .ToArray();

        var result = await _xboxService.InstallPackageAsync(
            analysis.MainPackage,
            selectedDeps,
            progress);

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        if (elapsed < 1000)
            await Task.Delay(1000 - (int)elapsed);

        InstallComplete = true;
        InstallSuccess = result;

        if (result)
        {
            InstallStatus = "Complete!";
            InstallResultMessage = null;
        }
        else
        {
            InstallStatus = "Install failed";
            InstallResultMessage = "Install failed";
        }

        InstallProgress = result ? 1.0 : 0;
        IsInstalling = false;
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
                DepItems.Add(new SelectableDep { FilePath = path, IsSelected = true });
            }
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
        OnPropertyChanged(nameof(DependencyCount));
        OnPropertyChanged(nameof(DependencyText));
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
        if (_downloadedFile is not null && File.Exists(_downloadedFile))
        {
            try { File.Delete(_downloadedFile); } catch { }
        }
        if (_analysis?.WorkingDirectory is not null && Directory.Exists(_analysis.WorkingDirectory))
        {
            try { Directory.Delete(_analysis.WorkingDirectory, true); } catch { }
        }
    }
}

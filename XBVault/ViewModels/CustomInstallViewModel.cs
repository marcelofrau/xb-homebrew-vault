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

public partial class CustomInstallViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;
    private readonly PackageInstallService _installService;
    private static readonly HttpClient _http = new();

    private AnalyzeResult? _analysis;
    private string? _downloadedFile;

    public Func<Task<string?>>? PickFileAsync;
    public Action? CloseAction;

    public CustomInstallViewModel(XboxDeviceService xboxService, PackageInstallService installService)
    {
        _xboxService = xboxService;
        _installService = installService;
    }

    [ObservableProperty]
    private int _currentStep;

    public static string[] StepLabels => ["Source", "Analysis", "Confirm", "Install"];

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

    public int DependencyCount => _analysis?.Dependencies.Length ?? 0;

    public string DependencyText
    {
        get
        {
            var c = DependencyCount;
            return c == 0 ? "No dependencies" : $"{c} dependenc{(c == 1 ? "y" : "ies")}";
        }
    }

    public ObservableCollection<string> FileList { get; } = [];

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

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsSourceStep));
        OnPropertyChanged(nameof(IsAnalysisStep));
        OnPropertyChanged(nameof(IsConfirmStep));
        OnPropertyChanged(nameof(IsInstallStep));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
    }

    partial void OnIsAnalyzingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoBack));
    }

    partial void OnIsInstallingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoBack));
    }

    partial void OnInstallCompleteChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCancel));
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

                var main = _analysis.MainPackage is not null ? Path.GetFileName(_analysis.MainPackage) : "None";
                AnalysisResultText = $"Main: {main}\nDependencies: {_analysis.Dependencies.Length}";
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
    private async Task InstallAsync()
    {
        var analysis = _analysis;
        if (analysis?.MainPackage is null) return;

        IsInstalling = true;
        CurrentStep = 3;
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

        var result = await _xboxService.InstallPackageAsync(
            analysis.MainPackage,
            analysis.Dependencies ?? [],
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

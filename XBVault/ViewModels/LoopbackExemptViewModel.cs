#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class LoopbackExemptViewModel : ObservableObject
{
    public const string XFilesProjectUrl = "https://github.com/marcelofrau/x-files-uwp";

    public enum StatusSeverity { None, Success, Error, Info }

    private readonly IXboxAuthService _authService;
    private readonly ISftpService _sftpService;
    private readonly IXboxPackageService _packageService;
    private readonly bool _quickMode;
    private readonly string _step1Label = "Overview";
    private readonly string _step4Label = "Run";
    private InstalledPackage? _xFilesPackage;

    public LoopbackExemptViewModel(IXboxAuthService authService, ISftpService sftpService, IXboxPackageService packageService, bool quickMode = false)
    {
        _authService = authService;
        _sftpService = sftpService;
        _packageService = packageService;
        _quickMode = quickMode;
        if (_quickMode)
            CurrentStep = 2;
    }

    // Injected from App.axaml.cs
    public Func<string, string, string, string, string?, string?, Task<bool>>? ShowConfirmAsync { get; set; }
    public Action? OpenProjectLinkAction { get; set; }
    public Action? CloseAction { get; set; }

    public bool IsQuickMode => _quickMode;
    public string WindowTitle => _quickMode ? "X-Files Enablement" : "Loopback Exempt Manager";

    // ---------- Wizard steps ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverviewStep))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(IsStep3))]
    [NotifyPropertyChangedFor(nameof(IsRunStep))]
    [NotifyPropertyChangedFor(nameof(ShowStep2Quick))]
    [NotifyPropertyChangedFor(nameof(ShowStep2Full))]
    [NotifyPropertyChangedFor(nameof(ShowStep3Quick))]
    [NotifyPropertyChangedFor(nameof(ShowStep3Full))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    [NotifyPropertyChangedFor(nameof(ShowBack))]
    [NotifyPropertyChangedFor(nameof(ShowNext))]
    [NotifyPropertyChangedFor(nameof(ShowRun))]
    [NotifyPropertyChangedFor(nameof(ShowCancel))]
    private int _currentStep = 1;

    public bool IsOverviewStep => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsRunStep => CurrentStep == 4;

    // Step 2 / 3 panels are mode-specific — show only the matching wizard's panel
    public bool ShowStep2Quick => IsStep2 && IsQuickMode;
    public bool ShowStep2Full => IsStep2 && !IsQuickMode;
    public bool ShowStep3Quick => IsStep3 && IsQuickMode;
    public bool ShowStep3Full => IsStep3 && !IsQuickMode;

    public string Step1Label => _step1Label;
    public string Step2Label => _quickMode ? "X-Files" : "App";
    public string Step3Label => _quickMode ? "Confirm" : "Action";
    public string Step4Label => _step4Label;

    // ---------- App / action state ----------

    public ObservableCollection<InstalledPackage> Packages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPreview))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    [NotifyPropertyChangedFor(nameof(SelectedFamilyName))]
    private InstalledPackage? _selectedPackage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPreview))]
    [NotifyPropertyChangedFor(nameof(RunLabel))]
    private bool _applyExemption = true;

    // ---------- Quick mode: X-Files ----------

    public bool XFilesDetected => _xFilesPackage is not null;
    public string? XFilesDisplayName => _xFilesPackage?.DisplayName ?? _xFilesPackage?.Name;
    public string? XFilesPfn => GetCheckNetIsolationName(_xFilesPackage);
    public string? SelectedFamilyName => GetCheckNetIsolationName(SelectedPackage);

    // Detection borders are hidden while the initial load is running (quick mode now
    // opens directly on step 2), otherwise the "not found" panel would flash red.
    public bool ShowXFilesDetected => XFilesDetected && !IsBusy;
    public bool ShowXFilesMissing => !XFilesDetected && !IsBusy;
    public bool IsDetectingXFiles => IsQuickMode && IsStep2 && IsBusy;

    // ---------- Busy / run state ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    [NotifyPropertyChangedFor(nameof(ShowBack))]
    [NotifyPropertyChangedFor(nameof(ShowNext))]
    [NotifyPropertyChangedFor(nameof(ShowRun))]
    [NotifyPropertyChangedFor(nameof(ShowCancel))]
    [NotifyPropertyChangedFor(nameof(ShowXFilesDetected))]
    [NotifyPropertyChangedFor(nameof(ShowXFilesMissing))]
    [NotifyPropertyChangedFor(nameof(IsDetectingXFiles))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _runComplete;

    [ObservableProperty]
    private bool _runSuccess;

    [ObservableProperty]
    private string? _runResultMessage;

    [ObservableProperty]
    private string? _runVerificationText;

    // ---------- Status banner (step 3 check + errors) ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    [NotifyPropertyChangedFor(nameof(StatusIconPath))]
    [NotifyPropertyChangedFor(nameof(StatusForeground))]
    [NotifyPropertyChangedFor(nameof(StatusBackground))]
    [NotifyPropertyChangedFor(nameof(StatusBorderBrush))]
    private StatusSeverity _statusType;

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasStatus => StatusType != StatusSeverity.None;

    public string StatusIconPath => StatusType switch
    {
        StatusSeverity.Success => "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-status-success-20.png",
        StatusSeverity.Error => "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-status-error-20.png",
        StatusSeverity.Info => "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-status-info-20.png",
        _ => string.Empty
    };

    public string StatusForeground => StatusType switch
    {
        StatusSeverity.Success => "#55FF55",
        StatusSeverity.Error => "#FF5555",
        StatusSeverity.Info => "#3399FF",
        _ => "Transparent"
    };

    public string StatusBackground => StatusType switch
    {
        StatusSeverity.Success => "#3355FF55",
        StatusSeverity.Error => "#33FF5555",
        StatusSeverity.Info => "#333399FF",
        _ => "Transparent"
    };

    public string StatusBorderBrush => StatusType switch
    {
        StatusSeverity.Success => "#5555FF55",
        StatusSeverity.Error => "#55FF5555",
        StatusSeverity.Info => "#553399FF",
        _ => "Transparent"
    };

    // ---------- Derived footer / command text ----------

    public string CommandPreview
    {
        get
        {
            var sw = IsQuickMode || ApplyExemption ? "-a" : "-d";
            return $"checknetisolation loopbackexempt {sw} -n={TargetPfn ?? "<PFN>"}";
        }
    }

    public string RunLabel => IsQuickMode || ApplyExemption ? "Enable" : "Remove";

    public bool CanGoBack => CurrentStep > 1 && !IsRunStep && !IsBusy && !RunComplete;
    public bool CanGoNext => !RunComplete && !IsBusy && (CurrentStep == 1 || (CurrentStep == 2 && (IsQuickMode ? XFilesDetected : SelectedPackage is not null)));
    public bool CanRun => IsStep3 && !IsBusy && !string.IsNullOrEmpty(TargetPfn);
    public bool ShowBack => !RunComplete && !IsRunStep && CurrentStep > 1;
    public bool ShowNext => !RunComplete && (CurrentStep == 1 || CurrentStep == 2);
    public bool ShowRun => IsStep3 && !RunComplete;
    public bool ShowCancel => !RunComplete && !IsRunStep;

    private string? TargetPfn => IsQuickMode ? GetCheckNetIsolationName(_xFilesPackage) : GetCheckNetIsolationName(SelectedPackage);

    /// <summary>
    /// Resolve the value accepted by 'checknetisolation loopbackexempt -n='.
    /// The console's PackageFamilyName field is the bare name (e.g. "XFiles.Xbox") and is
    /// rejected by checknetisolation, which needs the full family name including the publisher
    /// hash (e.g. "XFiles.Xbox_jgz7qwhvc5jpc").
    /// Preferred source: PackageRelativeId ("PFN!AppId") → PFN part. Fallback: derive from the
    /// PackageFullName by stripping the "<version>_<arch>" segment.
    /// </summary>
    private static string? GetCheckNetIsolationName(InstalledPackage? p)
    {
        if (p is null) return null;

        if (!string.IsNullOrEmpty(p.PackageRelativeId))
        {
            var bang = p.PackageRelativeId.IndexOf('!');
            if (bang > 0)
                return p.PackageRelativeId[..bang];
        }

        if (!string.IsNullOrEmpty(p.FullName))
        {
            var m = Regex.Match(p.FullName,
                @"^(?<name>.+)_(?<ver>\d+\.\d+\.\d+\.\d+)_[^_]+__?(?<hash>[A-Za-z0-9]+)$");
            if (m.Success)
                return m.Groups["name"].Value + "_" + m.Groups["hash"].Value;
        }

        return p.PackageFamilyName;
    }

    // ---------- Commands ----------

    [RelayCommand]
    private void GoBack()
    {
        if (!CanGoBack) return;
        CurrentStep--;
    }

    [RelayCommand]
    private void GoNext()
    {
        if (!CanGoNext) return;
        CurrentStep++;
    }

    [RelayCommand]
    private void Close() => CloseAction?.Invoke();

    [RelayCommand]
    private void OpenProjectLink()
    {
        try
        {
            OpenProjectLinkAction?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "LoopbackExempt: open project link failed");
            StatusMessage = "Failed to open the project link";
            StatusType = StatusSeverity.Error;
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            StatusMessage = null;
            StatusType = StatusSeverity.None;

            var packages = (await _packageService.GetInstalledPackagesAsync())
                .Where(p => !string.IsNullOrEmpty(p.PackageFamilyName))
                .OrderBy(p => p.DisplayName ?? p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Logger.Info($"LoopbackExempt: {packages.Count} package(s) loaded (quickMode={_quickMode})");
            foreach (var p in packages)
            {
                Logger.Info($"  - Name='{p.Name}' DisplayName='{p.DisplayName}' FullName='{p.FullName}' " +
                            $"PackageFamilyName='{p.PackageFamilyName}' PackageRelativeId='{p.PackageRelativeId}' Origin={p.Origin}");
            }

            if (IsQuickMode)
            {
                _xFilesPackage = packages.FirstOrDefault(IsXFilesPackage);
                Logger.Info(_xFilesPackage is not null
                    ? $"LoopbackExempt: X-Files resolved → checknetisolation PFN='{GetCheckNetIsolationName(_xFilesPackage)}'"
                    : "LoopbackExempt: X-Files NOT found");
                OnPropertyChanged(nameof(XFilesDetected));
                OnPropertyChanged(nameof(XFilesDisplayName));
                OnPropertyChanged(nameof(XFilesPfn));
                OnPropertyChanged(nameof(ShowXFilesDetected));
                OnPropertyChanged(nameof(ShowXFilesMissing));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanRun));
                OnPropertyChanged(nameof(CommandPreview));
            }
            else
            {
                var previous = SelectedPackage;
                Packages.Clear();
                foreach (var p in packages)
                    Packages.Add(p);

                SelectedPackage = previous is not null
                    ? packages.FirstOrDefault(p => p.FullName == previous.FullName)
                    : packages.FirstOrDefault(IsXFilesPackage)
                      ?? packages.FirstOrDefault(p => p.Origin == 3)
                      ?? packages.FirstOrDefault();

                if (packages.Count == 0)
                {
                    StatusMessage = "No installed packages found on the console";
                    StatusType = StatusSeverity.Info;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "LoopbackExempt: load failed");
            StatusMessage = $"Failed to load packages: {ex.Message}";
            StatusType = StatusSeverity.Error;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckStatusAsync()
    {
        if (IsBusy) return;

        var pfn = TargetPfn;
        if (string.IsNullOrEmpty(pfn))
        {
            StatusMessage = "Select an app first";
            StatusType = StatusSeverity.Info;
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = null;
            StatusType = StatusSeverity.None;

            await EnsureSftpConnectedAsync();
            Logger.Info($"LoopbackExempt: check status for PFN '{pfn}'");
            var exempted = await IsExemptedAsync(pfn);
            Logger.Info($"LoopbackExempt: check status → exempted={exempted}");
            StatusMessage = exempted ? "Already applied ✓" : "Not exempted";
            StatusType = exempted ? StatusSeverity.Success : StatusSeverity.Info;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "LoopbackExempt: check failed");
            StatusMessage = $"Command failed: {ex.Message}";
            StatusType = StatusSeverity.Error;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsBusy) return;

        var pfn = TargetPfn;
        var isApply = IsQuickMode || ApplyExemption;
        if (string.IsNullOrEmpty(pfn))
        {
            StatusMessage = "Select an app first";
            StatusType = StatusSeverity.Info;
            return;
        }

        var actionVerb = isApply ? "Enable" : "Remove";
        if (ShowConfirmAsync is not null)
        {
            var description = isApply
                ? $"Enable loopback for {XFilesOrAppName()} to browse LocalAppData / DevelopmentFiles on the console?\n\nCommand:\n{CommandPreview}"
                : $"Remove loopback from {XFilesOrAppName()}?\n\nCommand:\n{CommandPreview}";
            var ok = await ShowConfirmAsync("Loopback Exempt", description, actionVerb, "Cancel", null,
                "avares://XBVault/Assets/Views/LoopbackExemptWindow/loopback-shield-48.png");
            if (!ok) return;
        }

        IsRunning = true;
        RunComplete = false;
        RunSuccess = false;
        RunResultMessage = null;
        RunVerificationText = null;
        CurrentStep = 4;
        try
        {
            await EnsureSftpConnectedAsync();

            var sw = isApply ? "-a" : "-d";
            var command = $"checknetisolation loopbackexempt {sw} -n={pfn}";
            Logger.Info($"LoopbackExempt: running command → '{command}' (quickMode={_quickMode}, applyExemption={ApplyExemption})");
            var result = await _sftpService.RunShellCommandAsync(command);
            Logger.Info($"LoopbackExempt: command exit → Success={result.Success}\n  Output:\n{result.Output ?? "<null>"}\n  Error:\n{result.Error ?? "<null>"}");
            var expected = isApply;
            if (!result.Success)
            {
                // -a/-d can error when the state is already the target state; fall back to verification
                var exemptedNow = await IsExemptedAsync(pfn);
                if (exemptedNow == expected)
                {
                    SetRunResult(true,
                        isApply
                            ? "Already enabled. Relaunch the app to browse LocalAppData / DevelopmentFiles."
                            : "Already removed.",
                        $"Verified: 'checknetisolation loopbackexempt -s' {(expected ? "lists" : "does not list")} {pfn}");
                    return;
                }
                SetRunResult(false, $"Command failed: {result.Error ?? result.Output}", null);
                return;
            }

            // Command exited 0 → treat as applied/removed. The console SSH shell may not
            // echo 'checknetisolation loopbackexempt -s' output, so verification is best-effort.
            var verified = await IsExemptedAsync(pfn);
            Logger.Info($"LoopbackExempt: post-run verify → {(verified == expected ? "CONFIRMED" : "not confirmed by -s")} (expected={expected})");
            SetRunResult(true,
                isApply
                    ? "Loopback enabled. Relaunch the app to browse LocalAppData / DevelopmentFiles."
                    : "Loopback removed. The app can no longer reach the console's Dev Portal.",
                verified == expected
                    ? $"Verified: 'checknetisolation loopbackexempt -s' {(expected ? "lists" : "does not list")} {pfn}"
                    : "Note: the command succeeded, but 'checknetisolation loopbackexempt -s' did not confirm it.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "LoopbackExempt: run failed");
            SetRunResult(false, $"Failed: {ex.Message}", null);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private string XFilesOrAppName()
    {
        if (IsQuickMode)
            return XFilesDisplayName ?? "X-Files";
        return SelectedPackage?.DisplayName ?? SelectedPackage?.Name ?? "the app";
    }

    private static bool IsXFilesPackage(InstalledPackage p) =>
        ContainsNormalized(p.Name, "xfiles")
        || ContainsNormalized(p.DisplayName, "xfiles")
        || ContainsNormalized(p.PackageFamilyName, "xfiles");

    private static bool ContainsNormalized(string? value, string needle)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var normalized = value
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(".", string.Empty);
        return normalized.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private void SetRunResult(bool success, string message, string? verification)
    {
        RunSuccess = success;
        RunResultMessage = message;
        RunVerificationText = verification;
        RunComplete = true;
    }

    private async Task EnsureSftpConnectedAsync()
    {
        if (_sftpService.IsConnected) return;

        await _authService.FetchSmbPasswordAsync();
        var creds = _authService.GetSshCredentials();
        await _sftpService.ConnectAsync(creds.Host, creds.Port, creds.Username, creds.Password);
    }

    private async Task<bool> IsExemptedAsync(string pfn)
    {
        var result = await _sftpService.RunShellCommandAsync("checknetisolation loopbackexempt -s");
        Logger.Info($"LoopbackExempt: '-s' exit={result.Success}\n  Output:\n{result.Output ?? "<null>"}\n  Error:\n{result.Error ?? "<null>"}");
        return result.Success && (result.Output?.Contains(pfn, StringComparison.OrdinalIgnoreCase) == true);
    }
}

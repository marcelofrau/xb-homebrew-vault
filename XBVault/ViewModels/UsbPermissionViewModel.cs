using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;
using Avalonia.Input;

namespace XBVault.ViewModels;

public partial class UsbPermissionViewModel : ObservableObject
{
    private const int MinSpinnerDelayMs = 1000;

    public Action? CloseAction;

    public UsbPermissionViewModel()
    {
    }

    // Step mapping: 0=Welcome, 1=Format, 2=Select, 3=Apply, 4=Done

    [ObservableProperty]
    private int _currentStep;

    [ObservableProperty]
    private ObservableCollection<string> _usbDriveNames = [];

    private List<UsbDriveInfo> _loadedDrives = [];

    [ObservableProperty]
    private int _selectedDriveIndex = -1;

    [ObservableProperty]
    private UsbDriveInfo? _selectedDrive;

    [ObservableProperty]
    private string? _driveLetter;

    [ObservableProperty]
    private string? _driveLabel;

    [ObservableProperty]
    private string? _driveSize;

    [ObservableProperty]
    private string? _driveTypeLabel;

    [ObservableProperty]
    private string? _driveFileSystem;

    [ObservableProperty]
    private bool _isDriveValid;

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isApplying;

    public Cursor? Cursor => IsApplying ? WaitCursor : null;

    private static readonly Cursor WaitCursor = new(StandardCursorType.Wait);

    [ObservableProperty]
    private string? _applyProgressText;

    [ObservableProperty]
    private bool _applySuccess;

    [ObservableProperty]
    private bool _applyComplete;

    [ObservableProperty]
    private string? _resultMessage;

    [ObservableProperty]
    private string? _resultDetails;

    [ObservableProperty]
    private bool _isNoDrives;

    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public bool IsWelcomeStep => CurrentStep == 0;
    public bool IsFormatStep => CurrentStep == 1;
    public bool IsSelectStep => CurrentStep == 2;
    public bool IsApplyStep => CurrentStep == 3;
    public bool IsDoneStep => CurrentStep == 4;

    public string? DriveSummary => string.IsNullOrEmpty(DriveLabel) ? DriveLetter : $"{DriveLetter} - {DriveLabel}";

    public bool IsSuccess => ApplySuccess && ApplyComplete;
    public bool IsFailure => !ApplySuccess && ApplyComplete;

    public bool CanGoNext => CurrentStep switch
    {
        0 => true,
        1 => true,
        2 => IsDriveValid && SelectedDrive is not null,
        _ => false
    };

    public bool CanGoBack => CurrentStep > 0 && CurrentStep < 4 && !IsApplying && !ApplyComplete;

    public bool CanCancel => !IsApplying && !ApplyComplete;

    partial void OnDriveLetterChanged(string? value) => OnPropertyChanged(nameof(DriveSummary));
    partial void OnDriveLabelChanged(string? value) => OnPropertyChanged(nameof(DriveSummary));

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsWelcomeStep));
        OnPropertyChanged(nameof(IsFormatStep));
        OnPropertyChanged(nameof(IsSelectStep));
        OnPropertyChanged(nameof(IsApplyStep));
        OnPropertyChanged(nameof(IsDoneStep));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanCancel));
    }

    partial void OnIsApplyingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanCancel));
    }

    partial void OnApplyCompleteChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(IsFailure));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanCancel));
    }

    partial void OnSelectedDriveIndexChanged(int value)
    {
        if (value >= 0 && value < _loadedDrives.Count)
        {
            var drive = _loadedDrives[value];
            SelectedDrive = drive;
            DriveLetter = drive.DriveLetter;
            DriveLabel = drive.VolumeLabel;
            DriveSize = drive.FormattedSize;
            DriveTypeLabel = drive.DriveTypeLabel;
            DriveFileSystem = drive.FileSystem;

            var isNtfs = string.Equals(drive.FileSystem, "NTFS", StringComparison.OrdinalIgnoreCase);
            IsDriveValid = true;
            ValidationMessage = isNtfs
                ? null
                : "Drive is not NTFS. Formatting to NTFS is optional but recommended for Xbox compatibility.";
        }
        else
        {
            SelectedDrive = null;
            DriveLetter = null;
            DriveLabel = null;
            DriveSize = null;
            DriveTypeLabel = null;
            DriveFileSystem = null;
            IsDriveValid = false;
            ValidationMessage = null;
        }
        OnPropertyChanged(nameof(CanGoNext));
    }

    partial void OnIsNoDrivesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoNext));
    }

    [RelayCommand]
    private void GoNext()
    {
        if (CurrentStep < 3 && CanGoNext)
            CurrentStep++;
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStep > 0 && CurrentStep < 4)
            CurrentStep--;
    }

    [RelayCommand]
    private void OpenDiskManagement()
    {
        if (IsWindows)
        {
            try
            {
                Process.Start(new ProcessStartInfo("diskmgmt.msc") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open Disk Management");
            }
        }
    }

    [RelayCommand]
    private async Task LoadDrivesAsync()
    {
        Logger.Info("LoadDrivesAsync: starting drive detection");
        var drives = await Task.Run(() => UsbDriveDetector.ListUsbDrives());
        _loadedDrives = drives;
        UsbDriveNames = new ObservableCollection<string>(drives.Select(d => d.DisplayName));
        IsNoDrives = drives.Count == 0;
        Logger.Info($"LoadDrivesAsync: found {drives.Count} drives, IsNoDrives={IsNoDrives}");

        if (drives.Count > 0)
            SelectedDriveIndex = 0;
        else
            SelectedDriveIndex = -1;
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (SelectedDrive is null) return;

        Logger.Info($"ApplyAsync: starting for drive {SelectedDrive.DriveLetter}");
        IsApplying = true;
        ApplyProgressText = "Applying permissions...";
        CurrentStep = 3;

        // Minimum 1s delay so spinner is visible
        await Task.Delay(MinSpinnerDelayMs);

        try
        {
            var driveRoot = SelectedDrive.DriveLetter.TrimEnd('\\');
            var protectedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "System Volume Information",
                "$Recycle.Bin",
                "$RECYCLE.BIN"
            };

            var errors = new List<string>();

            // Step 1: grant on root (no /T) — sets ACL + inheritance flags
            ApplyProgressText = "Setting root permissions...";
            var (code1, _, stderr1) = await RunIcaclsAsync(
                $"{driveRoot}\\ /grant \"ALL APPLICATION PACKAGES:(OI)(CI)(F)\" /Q");
            if (code1 > 1)
                errors.Add($"Root: {stderr1}");

            // Step 2: grant recursively on each top-level item, skipping protected system dirs
            var entries = Directory.EnumerateFileSystemEntries(driveRoot).ToList();
            var processed = 0;
            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                if (protectedDirs.Contains(name))
                {
                    Logger.Info($"ApplyAsync: skipping protected entry '{name}'");
                    continue;
                }

                processed++;
                ApplyProgressText = $"Processing {name}...";
                var (code, _, stderr) = await RunIcaclsAsync(
                    $"\"{entry}\" /grant \"ALL APPLICATION PACKAGES:(OI)(CI)(F)\" /T /Q");
                if (code > 1)
                    errors.Add($"{name}: {stderr}");
            }

            Logger.Info($"ApplyAsync: {processed} items processed, {errors.Count} errors");

            if (errors.Count == 0)
            {
                ApplySuccess = true;
                ResultMessage = "Drive ready for Xbox!";
                ResultDetails = null;
            }
            else
            {
                ApplySuccess = false;
                ResultMessage = "Failed to apply permissions on some items";
                ResultDetails = string.Join("\n", errors.Take(10));
                if (errors.Count > 10)
                    ResultDetails += $"\n... and {errors.Count - 10} more errors";
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ApplyAsync: exception");
            ApplySuccess = false;
            ResultMessage = "Failed to apply permissions";
            ResultDetails = ex.Message;
        }

        ApplyComplete = true;
        IsApplying = false;
        CurrentStep = 4;
        Logger.Info($"ApplyAsync: complete success={ApplySuccess}");
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunIcaclsAsync(string arguments)
    {
        var psi = new ProcessStartInfo("icacls", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
            throw new InvalidOperationException("Failed to start icacls process");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Logger.Info($"RunIcaclsAsync: exit={process.ExitCode} args={arguments}");
        if (!string.IsNullOrEmpty(stdout)) Logger.Info($"RunIcaclsAsync: stdout=\n{stdout}");
        if (!string.IsNullOrEmpty(stderr)) Logger.Warn($"RunIcaclsAsync: stderr=\n{stderr}");

        return (process.ExitCode, stdout, stderr);
    }

    [RelayCommand]
    private void Close()
    {
        CloseAction?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseAction?.Invoke();
    }
}

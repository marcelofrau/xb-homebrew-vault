using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

            // Step 1: enumerate top-level items (skip protected system dirs)
            ApplyProgressText = "Enumerating drive contents...";
            var entries = Directory.EnumerateFileSystemEntries(driveRoot).ToList();
            var items = new List<string>();
            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                if (protectedDirs.Contains(name))
                {
                    Logger.Info($"ApplyAsync: skipping protected entry '{name}'");
                    continue;
                }
                items.Add(entry);
            }
            Logger.Info($"ApplyAsync: {items.Count} items to process");

            // Step 2: run every icacls in ONE elevated batch (single UAC prompt).
            // Volume-root ACL changes need admin; per-item elevation would pop
            // UAC once per entry. Output goes to a temp file we parse after.
            ApplyProgressText = "Setting permissions...";
            var (ok, message) = await RunElevatedIcaclsAsync(driveRoot, items);
            if (ok)
            {
                ApplySuccess = true;
                ResultMessage = "Drive ready for Xbox!";
                ResultDetails = null;
            }
            else
            {
                ApplySuccess = false;
                ResultMessage = "Failed to apply permissions on some items";
                ResultDetails = message;
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

    /// <summary>
    /// Runs icacls elevated once for the drive root and every top-level item.
    /// Requires administrator (volume-root ACLs), so this triggers a single UAC
    /// prompt. Commands are serialized to a temp batch file and output is
    /// captured to a temp log, then parsed per-target.
    /// </summary>
    private static async Task<(bool Ok, string? Message)> RunElevatedIcaclsAsync(
        string driveRoot, List<string> items)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "XBVault");
        Directory.CreateDirectory(tempDir);
        var batPath = Path.Combine(tempDir, $"icacls-{Guid.NewGuid():N}.cmd");
        var logPath = Path.Combine(tempDir, $"icacls-{Guid.NewGuid():N}.log");

        try
        {
            // echo a marker before each icacls so we can attribute failures later.
            var lines = new List<string> { "@echo off" };
            void Add(string cmd) => lines.Add(cmd);

            Add("echo ===TARGET:ROOT===");
            Add($"icacls \"{driveRoot}\\\" /grant \"ALL APPLICATION PACKAGES:(OI)(CI)(F)\" /Q");

            for (int i = 0; i < items.Count; i++)
            {
                Add($"echo ===TARGET:{i}===");
                Add($"icacls \"{items[i]}\" /grant \"ALL APPLICATION PACKAGES:(OI)(CI)(F)\" /T /Q");
            }

            await File.WriteAllLinesAsync(batPath, lines);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{batPath}\" > \"{logPath}\" 2>&1\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Logger.Info($"RunElevatedIcaclsAsync: elevating batch for {driveRoot}");
            using (var process = Process.Start(psi))
            {
                if (process is null)
                    throw new InvalidOperationException("Failed to start elevated icacls process");
                await process.WaitForExitAsync();
            }

            var output = File.Exists(logPath) ? await File.ReadAllTextAsync(logPath) : "";
            Logger.Info($"RunElevatedIcaclsAsync: icacls output:\n{output}");

            return ParseIcaclsOutput(output, driveRoot, items);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Logger.Warn("RunElevatedIcaclsAsync: UAC declined by user");
            return (false, "Administrator permission was declined. The drive was not modified.");
        }
        finally
        {
            TryDelete(batPath);
            TryDelete(logPath);
        }
    }

    private static (bool Ok, string? Message) ParseIcaclsOutput(
        string output, string driveRoot, List<string> items)
    {
        var errors = new List<string>();

        // Root marker
        var rootBlock = ExtractBlock(output, "===TARGET:ROOT===");
        if (rootBlock is not null && BlockFailed(rootBlock))
            errors.Add($"Root ({driveRoot}\\: {(BlockReason(rootBlock) ?? "Access is denied")})");

        for (int i = 0; i < items.Count; i++)
        {
            var block = ExtractBlock(output, $"===TARGET:{i}===");
            if (block is null || !BlockFailed(block)) continue;
            var name = Path.GetFileName(items[i]);
            errors.Add($"{name}: {BlockReason(block) ?? "Access is denied"}");
        }

        return (errors.Count == 0, errors.Count == 0 ? null : string.Join("\n", errors.Take(10)));
    }

    private static string? ExtractBlock(string output, string marker)
    {
        var start = output.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += marker.Length;
        var end = output.IndexOf("===TARGET:", start + 1, StringComparison.OrdinalIgnoreCase);
        if (end < 0) end = output.Length;
        return output[start..end];
    }

    private static bool BlockFailed(string block)
    {
        // icacls prints "Successfully processed N files; Failed processing M files"
        var failIdx = block.IndexOf("Failed processing", StringComparison.OrdinalIgnoreCase);
        if (failIdx < 0) return false;
        var rest = block[(failIdx + "Failed processing".Length)..];
        var num = new string(rest.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(num, out var count) && count > 0;
    }

    private static string? BlockReason(string block)
    {
        return block.Contains("denied", StringComparison.OrdinalIgnoreCase) ? "Access is denied" : null;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best effort cleanup */ }
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

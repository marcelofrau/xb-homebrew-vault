#if WINDOWS_BUILD
using System.Management;
#pragma warning disable CA1416
#endif
using System.Globalization;
using System.Runtime.InteropServices;
using XBVault.Models;

namespace XBVault.Services;

public static class UsbDriveDetector
{
    public static List<UsbDriveInfo> ListUsbDrives()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Logger.Warn("UsbDriveDetector: not Windows, returning empty");
            return [];
        }
        return ListUsbDrivesWindows();
    }

#if WINDOWS_BUILD
    private static List<UsbDriveInfo> ListUsbDrivesWindows()
    {
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\');
        Logger.Info($"UsbDriveDetector: systemDrive = {systemDrive}");
        var drives = new List<UsbDriveInfo>();

        // Primary: physical USB disks (stick, external HDD/SSD). External USB HDDs
        // report Win32_LogicalDisk.DriveType=3 (Local Disk), NOT 2, so filtering by
        // DriveType=2 misses them. Map Win32_DiskDrive (InterfaceType='USB' or
        // MediaType external/removable) -> partitions -> letters.
        var usbDrives = ListUsbDiskDrives();
        foreach (var (letter, label) in usbDrives)
        {
            if (IsSystemDrive(letter, systemDrive)) continue;
            var info = BuildDriveInfo(letter, label);
            if (info is not null)
                drives.Add(info);
        }

        // Fallback: removable logical disks (DriveType=2) not already mapped,
        // in case the physical-disk mapping yields nothing.
        foreach (var letter in ListRemovableLetters())
        {
            if (IsSystemDrive(letter, systemDrive)) continue;
            if (drives.Any(d => d.DriveLetter.Equals(letter, StringComparison.OrdinalIgnoreCase))) continue;
            var info = BuildDriveInfo(letter, "USB Stick");
            if (info is not null)
                drives.Add(info);
        }

        var result = drives
            .OrderBy(d => d.DriveLetter)
            .ToList();
        Logger.Info($"UsbDriveDetector: returning {result.Count} drives: {string.Join(", ", result.Select(d => d.DriveLetter))}");
        return result;
    }

    private static List<(string Letter, string Label)> ListUsbDiskDrives()
    {
        var drives = new List<(string, string)>();
        try
        {
            // USB SATA/NVMe bridges often report InterfaceType='SCSI' instead of
            // 'USB', so also match by MediaType ('External hard disk media' for
            // external HDDs, 'Removable Media' for sticks) to catch every USB disk.
            // These are driver-enum values, not localized OS strings, so the query
            // is safe across pt-BR/en/... systems.
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_DiskDrive " +
                "WHERE InterfaceType='USB' OR MediaType LIKE '%External%' OR MediaType LIKE '%Removable%'");
            foreach (var disk in searcher.Get())
            {
                var deviceId = disk["DeviceID"]?.ToString();
                if (string.IsNullOrEmpty(deviceId)) continue;
                var letter = FindLetterForDisk(deviceId);
                if (letter is not null)
                {
                    var label = ClassifyLabel(disk);
                    Logger.Info($"UsbDriveDetector: USB disk {deviceId} -> {letter} ({label})");
                    drives.Add((letter, label));
                }
                else
                {
                    Logger.Info($"UsbDriveDetector: USB disk {deviceId} has no mapped drive letter");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "UsbDriveDetector: USB disk query failed");
        }
        return drives
            .GroupBy(d => d.Item1, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static string ClassifyLabel(ManagementBaseObject disk)
    {
        var mediaType = disk["MediaType"]?.ToString() ?? "";
        var interfaceType = disk["InterfaceType"]?.ToString() ?? "";

        if (mediaType.Contains("Removable", StringComparison.OrdinalIgnoreCase))
            return "USB Stick";
        if (mediaType.Contains("External", StringComparison.OrdinalIgnoreCase))
            return "External HDD";
        if (interfaceType.Equals("USB", StringComparison.OrdinalIgnoreCase))
            return "USB Drive";
        return "USB Drive";
    }

    private static string? FindLetterForDisk(string diskDeviceId)
    {
        try
        {
            using var partitionSearcher = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{diskDeviceId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");
            foreach (var partition in partitionSearcher.Get())
            {
                var partitionId = partition["DeviceID"]?.ToString();
                if (string.IsNullOrEmpty(partitionId)) continue;

                using var logicalSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionId}'}} WHERE AssocClass = Win32_LogicalDiskToPartition");
                foreach (var logical in logicalSearcher.Get())
                {
                    var deviceId = logical["DeviceID"]?.ToString();
                    if (!string.IsNullOrEmpty(deviceId))
                        return deviceId.TrimEnd(':') + ":";
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"UsbDriveDetector: partition mapping failed for {diskDeviceId}");
        }
        return null;
    }

    private static List<string> ListRemovableLetters()
    {
        var letters = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID FROM Win32_LogicalDisk WHERE DriveType=2");
            foreach (var disk in searcher.Get())
            {
                var deviceId = disk["DeviceID"]?.ToString() ?? "";
                var letter = deviceId.TrimEnd(':') + ":";
                if (deviceId.Length > 0)
                    letters.Add(letter);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "UsbDriveDetector: removable logical disk query failed");
        }
        return letters;
    }

    private static bool IsSystemDrive(string driveLetter, string? systemDrive)
    {
        return !string.IsNullOrEmpty(systemDrive) &&
               driveLetter.TrimEnd('\\').Equals(systemDrive, StringComparison.OrdinalIgnoreCase);
    }

    private static UsbDriveInfo? BuildDriveInfo(string driveLetter, string driveTypeLabel)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_LogicalDisk WHERE DeviceID='{driveLetter.TrimEnd(':')}:'");
            foreach (var disk in searcher.Get())
            {
                var volumeName = disk["VolumeName"]?.ToString() ?? "";
                var fs = disk["FileSystem"]?.ToString() ?? "";
                var size = disk["Size"] is ulong sz ? (long)sz : 0L;

                return new UsbDriveInfo
                {
                    DriveLetter = driveLetter,
                    VolumeLabel = string.IsNullOrEmpty(volumeName) ? "(No Label)" : volumeName,
                    SizeBytes = size,
                    FormattedSize = FormatSize(size),
                    FileSystem = fs,
                    DriveTypeLabel = driveTypeLabel,
                    IsSystemDrive = false
                };
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"UsbDriveDetector: logical disk detail query failed for {driveLetter}");
        }
        return null;
    }
#else
    private static List<UsbDriveInfo> ListUsbDrivesWindows()
    {
        Logger.Warn("UsbDriveDetector: WMI not available on this platform");
        return [];
    }
#endif

    private static string FormatSize(long bytes)
    {
        // InvariantCulture: "1.5 GB" regardless of pt-BR comma vs en-US dot
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0).ToString("F1", CultureInfo.InvariantCulture)} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{(bytes / (1024.0 * 1024)).ToString("F1", CultureInfo.InvariantCulture)} MB";
        if (bytes < 1024L * 1024 * 1024 * 1024) return $"{(bytes / (1024.0 * 1024 * 1024)).ToString("F1", CultureInfo.InvariantCulture)} GB";
        return $"{(bytes / (1024.0 * 1024 * 1024 * 1024)).ToString("F1", CultureInfo.InvariantCulture)} TB";
    }
}

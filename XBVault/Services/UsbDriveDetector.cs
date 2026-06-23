using System.Management;
using System.Runtime.InteropServices;
using XBVault.Models;

namespace XBVault.Services;

public static class UsbDriveDetector
{
    public static List<UsbDriveInfo> ListUsbDrives()
    {
        var drives = new List<UsbDriveInfo>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Logger.Warn("UsbDriveDetector: not Windows, returning empty");
            return drives;
        }

        Logger.Info("UsbDriveDetector: starting WMI USB drive scan");
        try
        {
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\');
            Logger.Info($"UsbDriveDetector: systemDrive = {systemDrive}");

            // Query logical disks directly — avoids fragile ASSOCIATORS OF chain
            // that fails on many removable USB drives.
            // DriveType 2 = Removable (USB sticks, flash drives)
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_LogicalDisk WHERE DriveType=2");

            Logger.Info("UsbDriveDetector: querying Win32_LogicalDisk WHERE DriveType=2");
            foreach (var disk in searcher.Get())
            {
                try
                {
                    var deviceId = disk["DeviceID"]?.ToString() ?? "";
                    var volumeName = disk["VolumeName"]?.ToString() ?? "";
                    var fs = disk["FileSystem"]?.ToString() ?? "";
                    var size = disk["Size"] is ulong sz ? (long)sz : 0L;
                    var driveLetter = deviceId.TrimEnd(':') + ":";

                    var isSystemDrive = driveLetter.TrimEnd('\\').Equals(systemDrive, StringComparison.OrdinalIgnoreCase);

                    Logger.Info($"UsbDriveDetector: found logical drive {driveLetter} vol='{volumeName}' fs={fs} size={size} isSystem={isSystemDrive}");

                    drives.Add(new UsbDriveInfo
                    {
                        DriveLetter = driveLetter,
                        VolumeLabel = string.IsNullOrEmpty(volumeName) ? "(No Label)" : volumeName,
                        SizeBytes = size,
                        FormattedSize = FormatSize(size),
                        FileSystem = fs,
                        DriveTypeLabel = "USB Stick",
                        IsSystemDrive = isSystemDrive
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "UsbDriveDetector: logical disk iteration error");
                }
            }

            Logger.Info($"UsbDriveDetector: found {drives.Count} total removable logical drives via WMI");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "UsbDriveDetector: WMI query failed");
        }

        var result = drives
            .Where(d => !d.IsSystemDrive)
            .OrderBy(d => d.DriveLetter)
            .ToList();
        Logger.Info($"UsbDriveDetector: returning {result.Count} drives (filtered {drives.Count - result.Count} system drives)");
        return result;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}


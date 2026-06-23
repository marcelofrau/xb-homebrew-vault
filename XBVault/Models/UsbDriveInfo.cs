namespace XBVault.Models;

public record UsbDriveInfo
{
    public string DriveLetter { get; init; } = string.Empty;
    public string VolumeLabel { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string FormattedSize { get; init; } = string.Empty;
    public string FileSystem { get; init; } = string.Empty;
    public string DriveTypeLabel { get; init; } = string.Empty;
    public bool IsSystemDrive { get; init; }

    public string DisplayName => $"{DriveLetter} - {VolumeLabel} ({DriveTypeLabel})";
}

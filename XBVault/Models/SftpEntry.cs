using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Renci.SshNet.Sftp;
using XBVault.Services;

namespace XBVault.Models;

public class SftpEntry : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string prop = "")
    {
        Logger.Trace($"SftpEntry.Notify: {prop} on '{FullPath ?? "<new>"}'");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            Notify();
        }
    }

    private bool _isLastChild;
    public bool IsLastChild
    {
        get => _isLastChild;
        set
        {
            if (_isLastChild == value) return;
            _isLastChild = value;
            Notify();
        }
    }

    private static readonly Dictionary<string, Bitmap> _iconCache = [];

    private Bitmap? _iconPath;
    private Bitmap? _treeIconPath;

    public string Name { get; set; }
    public string FullPath { get; set; }
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string? Extension { get; set; }
    public bool IsJunction { get; set; }
    public bool IsDrive { get; set; }
    public string? IconName { get; set; }
    public string? ToolTip { get; set; }

    public string FormattedSize => IsDirectory ? "" : FormatSize(Size);

    public Bitmap? IconPath
    {
        get
        {
            _iconPath ??= LoadIcon(IsDirectory ? "folder" : GetFileIconName());
            return _iconPath;
        }
    }

    public Bitmap? TreeIconPath
    {
        get
        {
            if (IsPlaceholder) return null;
            _treeIconPath ??= LoadIcon(GetTreeIconName());
            return _treeIconPath;
        }
    }

    public ObservableCollection<SftpEntry> Children { get; set; } = [];

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            Notify();
        }
    }

    public bool HasLoaded { get; set; }
    public bool IsPlaceholder { get; set; }
    public bool ShowIcon => !IsPlaceholder;

    public Thickness HeaderMargin => !IsDrive && !IsPlaceholder && Children.Count == 0
        ? new Thickness(23, 0, 0, 0)
        : new Thickness(0);

    public SftpEntry()
    {
        Name = string.Empty;
        FullPath = string.Empty;
        Children.CollectionChanged += (_, _) => Notify(nameof(HeaderMargin));
    }

    private string GetTreeIconName() => IconName ?? (IsDrive || IsJunction ? "drive" : IsDirectory ? "folder" : "file");

    private string GetFileIconName()
    {
        if (Extension is null) return "file";
        var ext = Extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "iso" => "disc",
            "zip" or "7z" or "rar" or "tar" or "gz" => "archive",
            "xex" or "xbe" or "exe" => "game",
            "jpg" or "jpeg" or "png" or "gif" or "bmp" or "webp" => "image",
            "mp3" or "wav" or "flac" or "ogg" or "wma" or "aac" or "m4a" => "audio",
            "mp4" or "avi" or "mkv" or "wmv" or "mov" or "webm" or "m4v" => "video",

            _ => "file"
        };
    }

    private static Bitmap LoadIcon(string name)
    {
        if (_iconCache.TryGetValue(name, out var cached))
        {
            Logger.Trace($"LoadIcon: cache hit '{name}'");
            return cached;
        }

        var uri = $"avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-{name}-24.png";
        Logger.Trace($"LoadIcon: cache miss '{name}', loading from {uri}");
        var bitmap = new Bitmap(AssetLoader.Open(new Uri(uri)));
        _iconCache[name] = bitmap;
        return bitmap;
    }

    public static SftpEntry FromSftpFile(ISftpFile file)
    {
        var entry = new SftpEntry
        {
            Name = file.Name,
            FullPath = file.FullName,
            IsDirectory = file.IsDirectory || file.IsSymbolicLink,
            Size = file.Length,
            LastModified = file.LastWriteTimeUtc,
            Extension = file.IsDirectory ? null : Path.GetExtension(file.Name),
            IsJunction = file.IsSymbolicLink
        };
        if (entry.IsDirectory)
            entry.Children.Add(new SftpEntry { Name = "" });
        Logger.Debug($"FromSftpFile: '{entry.FullPath}' dir={entry.IsDirectory} size={entry.Size}");
        return entry;
    }

    private static string FormatSize(long bytes)
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
}

public class SftpShellResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string? Error { get; set; }
}

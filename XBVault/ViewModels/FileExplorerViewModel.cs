using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public enum ToolbarStatusSeverity { None, Info, Success, Warning, Error }

public partial class FileExplorerViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;
    private readonly SftpService _sftpService;
    private string? _sftpPassword;
    private DateTime _transferStartTime;
    private long _transferBytesTotal;
    private CancellationTokenSource? _currentTransferCts;

    public FileExplorerViewModel(XboxDeviceService xboxService, SftpService sftpService)
    {
        _xboxService = xboxService;
        _sftpService = sftpService;
        _xboxService.ConnectionChanged += OnBoxConnectionChanged;
        _sftpService.ConnectionChanged += OnSftpConnectionChanged;
        IsConnected = _xboxService.IsConnected;
        UpdateStatusText();
        Logger.Debug("FileExplorerViewModel initialized");
    }

    public Func<IReadOnlyList<SftpEntry>, Task<bool>>? ShowDeleteConfirmAsync { get; set; }
    public Func<SftpEntry, Task<string?>>? ShowSaveFileDialogAsync { get; set; }
    public Func<string, string, string, int, Task>? ShowConnectionInfoAsync { get; set; }
    public Func<Task<bool>>? ShowConnectAction { get; set; }
    public Func<Task<string?>>? ShowFolderPickerAsync { get; set; }
    public Action<SftpEntry>? ScrollToEntry { get; set; }
    public Action? FocusFileList { get; set; }
    public Action<string, string, string>? ShowErrorDialog { get; set; }
    public Func<string, string, string, string?, Task<string?>>? ShowInputDialogAsync { get; set; }

    private void OnBoxConnectionChanged(bool connected)
    {
        Logger.Trace($"OnBoxConnectionChanged: connected={connected}");
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = connected;
            if (!connected)
                _sftpService.Disconnect();
            UpdateStatusText();
        });
    }

    private void OnSftpConnectionChanged(object? sender, bool connected)
    {
        Logger.Trace($"OnSftpConnectionChanged: connected={connected}");
        Dispatcher.UIThread.Post(() =>
        {
            if (!connected)
            {
                TreeRoots.Clear();
                CurrentEntries.Clear();
                ErrorMessage = null;
                StatusSeverity = ToolbarStatusSeverity.None;
                StatusMessage = string.Empty;
                StatusText = "Ready to browse";
            }
            else
            {
                StatusText = "Connected";
            }
            NotifyStateDependentProperties();
        });
    }

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private SftpEntry? _selectedEntry;

    public ObservableCollection<SftpEntry> SelectedEntries { get; } = [];

    public bool CanDeleteMultiple => SelectedEntries.Count > 0;
    public bool CanDownloadMultiple => SelectedEntries.Count(e => !e.IsDrive) > 0;
    public bool CanRenameSingle => SelectedEntries.Count == 1;

    [ObservableProperty]
    private string _currentPath = @"D:\";

    public bool CanGoUp => GetParentPath(CurrentPath) is not null;

    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(CanDeleteMultiple));
        OnPropertyChanged(nameof(CanDownloadMultiple));
        OnPropertyChanged(nameof(CanRenameSingle));
    }

    private string FormatSpeed(double fraction)
    {
        if (_transferBytesTotal <= 0) return string.Empty;
        var elapsed = (DateTime.UtcNow - _transferStartTime).TotalSeconds;
        if (elapsed < 0.5) return string.Empty;
        var bps = fraction * _transferBytesTotal / elapsed;
        if (bps >= 1024 * 1024)
            return $" {(bps / (1024 * 1024)):F1} MB/s";
        if (bps >= 1024)
            return $" {(bps / 1024):F1} KB/s";
        return $" {bps:F0} B/s";
    }

    public bool CanCancelTransfer => IsUploading || IsDownloading;

    [RelayCommand]
    private void CancelTransfer()
    {
        Logger.Debug("CancelTransfer: requesting cancellation");
        _currentTransferCts?.Cancel();
        _currentTransferCts = null;
    }

    [ObservableProperty]
    private string _uploadStatusText = string.Empty;

    [ObservableProperty]
    private double _uploadProgress;

    [ObservableProperty]
    private bool _isUploading;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatusText = string.Empty;

    public bool ShowActivity => IsUploading || IsDownloading;
    public bool ShowIdle => !ShowActivity;
    public double ActivityProgress => IsUploading ? UploadProgress : DownloadProgress;
    public string ActivityText => IsUploading ? UploadStatusText : DownloadStatusText;

    partial void OnIsUploadingChanged(bool value) { OnPropertyChanged(nameof(ShowActivity)); OnPropertyChanged(nameof(ShowIdle)); OnPropertyChanged(nameof(CanCancelTransfer)); }
    partial void OnIsDownloadingChanged(bool value) { OnPropertyChanged(nameof(ShowActivity)); OnPropertyChanged(nameof(ShowIdle)); OnPropertyChanged(nameof(CanCancelTransfer)); }
    partial void OnUploadProgressChanged(double value) => OnPropertyChanged(nameof(ActivityProgress));
    partial void OnDownloadProgressChanged(double value) => OnPropertyChanged(nameof(ActivityProgress));
    partial void OnUploadStatusTextChanged(string value) => OnPropertyChanged(nameof(ActivityText));
    partial void OnDownloadStatusTextChanged(string value) => OnPropertyChanged(nameof(ActivityText));

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    [NotifyPropertyChangedFor(nameof(IsStatusError))]
    [NotifyPropertyChangedFor(nameof(IsStatusWarning))]
    [NotifyPropertyChangedFor(nameof(IsStatusSuccess))]
    [NotifyPropertyChangedFor(nameof(IsStatusInfo))]
    [NotifyPropertyChangedFor(nameof(StatusIconPath))]
    [NotifyPropertyChangedFor(nameof(StatusBackground))]
    [NotifyPropertyChangedFor(nameof(StatusBorderBrush))]
    [NotifyPropertyChangedFor(nameof(StatusForeground))]
    private ToolbarStatusSeverity _statusSeverity = ToolbarStatusSeverity.None;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _statusText = "Not connected";

    [ObservableProperty]
    private string _initStepText = string.Empty;

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnIsConnectedChanged(bool value)
    {
        Logger.Trace($"OnIsConnectedChanged: value={value}");
        UpdateStatusText();
        if (!value)
        {
            TreeRoots.Clear();
            CurrentEntries.Clear();
            ErrorMessage = null;
            StatusSeverity = ToolbarStatusSeverity.None;
            StatusMessage = string.Empty;
        }
        NotifyStateDependentProperties();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        Logger.Trace($"OnIsLoadingChanged: value={value}");
        NotifyStateDependentProperties();
    }

    public bool HasSelectedEntry => SelectedEntry is not null && !SelectedEntry.IsPlaceholder;

    partial void OnSelectedEntryChanged(SftpEntry? value)
    {
        Logger.Trace($"OnSelectedEntryChanged: '{value?.FullPath ?? "null"}'");
        OnPropertyChanged(nameof(HasSelectedEntry));
    }

    partial void OnCurrentPathChanged(string value)
    {
        Logger.Trace($"OnCurrentPathChanged: '{value}'");
        OnPropertyChanged(nameof(BreadcrumbSegments));
        OnPropertyChanged(nameof(CanGoUp));
        StatusSeverity = ToolbarStatusSeverity.None;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void NavigateToParent()
    {
        var parent = GetParentPath(CurrentPath);
        if (parent is not null)
            NavigateToPathCommand.Execute(parent);
    }

    public bool ShowDisconnectedContent => !IsConnected;
    public bool ShowReadyContent => IsConnected && !IsLoading && _sftpService.IsConnected;
    public bool ShowPromptContent => IsConnected && !IsLoading && !_sftpService.IsConnected;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasStatus => StatusSeverity != ToolbarStatusSeverity.None;
    public bool IsStatusError => StatusSeverity == ToolbarStatusSeverity.Error;
    public bool IsStatusWarning => StatusSeverity == ToolbarStatusSeverity.Warning;
    public bool IsStatusSuccess => StatusSeverity == ToolbarStatusSeverity.Success;
    public bool IsStatusInfo => StatusSeverity == ToolbarStatusSeverity.Info;
    public string StatusIconPath => StatusSeverity switch
    {
        ToolbarStatusSeverity.Error => "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-status-error-20.png",
        ToolbarStatusSeverity.Warning => "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-status-warning-20.png",
        ToolbarStatusSeverity.Success => "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-status-success-20.png",
        ToolbarStatusSeverity.Info => "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-status-info-20.png",
        _ => string.Empty
    };
    public string StatusBackground => StatusSeverity switch
    {
        ToolbarStatusSeverity.Error => "#33FF5555",
        ToolbarStatusSeverity.Warning => "#33FFAA33",
        ToolbarStatusSeverity.Success => "#3355FF55",
        ToolbarStatusSeverity.Info => "#333399FF",
        _ => "Transparent"
    };
    public string StatusBorderBrush => StatusSeverity switch
    {
        ToolbarStatusSeverity.Error => "#55FF5555",
        ToolbarStatusSeverity.Warning => "#55FFAA33",
        ToolbarStatusSeverity.Success => "#5555FF55",
        ToolbarStatusSeverity.Info => "#553399FF",
        _ => "Transparent"
    };
    public string StatusForeground => StatusSeverity switch
    {
        ToolbarStatusSeverity.Error => "#FF5555",
        ToolbarStatusSeverity.Warning => "#FFAA33",
        ToolbarStatusSeverity.Success => "#55FF55",
        ToolbarStatusSeverity.Info => "#3399FF",
        _ => "Transparent"
    };
    public bool CanBrowse => IsConnected && !IsLoading;
    public bool CanRefresh => _sftpService.IsConnected && TreeRoots.Count > 0;
    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public string[] BreadcrumbSegments
    {
        get
        {
            var parts = CurrentPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return [];
            var segments = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var joined = string.Join("\\", parts, 0, i + 1);
                if (joined.EndsWith(":"))
                    joined += "\\";
                segments[i] = joined;
            }
            return segments;
        }
    }

    public ObservableCollection<SftpEntry> TreeRoots { get; } = [];
    public ObservableCollection<SftpEntry> CurrentEntries { get; } = [];

    private void NotifyStateDependentProperties()
    {
        OnPropertyChanged(nameof(ShowDisconnectedContent));
        OnPropertyChanged(nameof(ShowReadyContent));
        OnPropertyChanged(nameof(ShowPromptContent));
        OnPropertyChanged(nameof(CanBrowse));
        OnPropertyChanged(nameof(CanRefresh));
    }

    private void UpdateStatusText()
    {
        if (!IsConnected)
            StatusText = "Not connected";
        else if (_sftpService.IsConnected)
            StatusText = "Connected";
        else
            StatusText = "Ready to browse";
    }

    private static void InsertSorted(ObservableCollection<SftpEntry> list, SftpEntry entry)
    {
        var i = 0;
        for (; i < list.Count; i++)
        {
            var e = list[i];
            if (e.IsPlaceholder) continue;
            if (entry.IsDirectory && !e.IsDirectory) break;
            if (!entry.IsDirectory && e.IsDirectory) continue;
            if (string.Compare(entry.Name, e.Name, StringComparison.OrdinalIgnoreCase) < 0) break;
        }
        list.Insert(i, entry);
        UpdateLastChildFlag(list);
    }

    private static void UpdateLastChildFlag(ObservableCollection<SftpEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
            entries[i].IsLastChild = i >= entries.Count - 1;
    }

    private static void UpdateChildrenPathsRecursive(SftpEntry entry, string oldPath)
    {
        foreach (var child in entry.Children)
        {
            if (child.IsPlaceholder) continue;
            child.FullPath = child.FullPath.Replace(oldPath, entry.FullPath);
            if (child.IsDirectory)
                UpdateChildrenPathsRecursive(child, oldPath);
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (ShowConnectAction is not null)
        {
            var ok = await ShowConnectAction();
            if (ok)
                _xboxService.MarkConnected();
        }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (!_xboxService.IsConnected || IsLoading || _sftpService.IsConnected)
        {
            Logger.Debug($"InitializeAsync: skipped (connected={_xboxService.IsConnected}, loading={IsLoading}, sftp={_sftpService.IsConnected})");
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            InitStepText = "Discovering credentials...";
            Logger.Debug("Fetching SMB password...");
            await _xboxService.FetchSmbPasswordAsync();

            InitStepText = "Connecting to Xbox via SFTP...";
            Logger.Debug("InitializeAsync: getting SSH credentials...");
            var creds = _xboxService.GetSshCredentials();
            _sftpPassword = creds.Password;
            Logger.Debug($"InitializeAsync: connecting to {creds.Host}:{creds.Port} as {creds.Username}");

            await _sftpService.ConnectAsync(creds.Host, creds.Port, creds.Username, creds.Password);
            Logger.Debug("InitializeAsync: SFTP connected, loading tree roots...");

            InitStepText = "Listing available drives...";
            await LoadTreeRootsAsync();
            Logger.Debug("InitializeAsync: tree roots loaded successfully");

            InitStepText = "Ready";
            StatusText = "Connected";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize file explorer");
            ErrorMessage = $"Connection failed: {ex.Message}";
            InitStepText = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadTreeRootsAsync()
    {
        Logger.Debug("LoadTreeRootsAsync: detecting drives...");
        var drives = await DetectDrivesAsync();
        Dispatcher.UIThread.Post(() =>
        {
            TreeRoots.Clear();
            foreach (var d in drives)
                TreeRoots.Add(d);
            Logger.Debug($"LoadTreeRootsAsync: added {drives.Count} drive roots");
            OnPropertyChanged(nameof(CanRefresh));
        });
    }

    private static void SetIsLastChild(List<SftpEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
            entries[i].IsLastChild = i >= entries.Count - 1;
    }

    private static Task<List<SftpEntry>> DetectDrivesAsync()
    {
        var drives = new[] { "C", "D", "E", "Q" }.Select(l =>
        {
            var e = new SftpEntry
            {
                Name = $"{l}:\\",
                FullPath = $"{l}:\\",
                IsDirectory = true,
                IsDrive = true,
                LastModified = DateTime.MinValue,
                ToolTip = l == "E" ? "Usually external drive" : null
            };
            e.Children.Add(new SftpEntry { Name = "" });
            return e;
        }).ToList();
        SetIsLastChild(drives);
        return Task.FromResult(drives);
    }

    [RelayCommand]
    private async Task ExpandFolderAsync(string path)
    {
        Logger.Debug($"ExpandFolderAsync: '{path}'");
        SftpEntry? target = null;
        try
        {
            target = FindEntry(TreeRoots, path);
            if (target is null || target.HasLoaded)
            {
                Logger.Trace($"ExpandFolderAsync: '{path}' skipped (found={target is not null}, loaded={target?.HasLoaded})");
                return;
            }

            target.HasLoaded = true;
            target.Children.Clear();

            var children = await _sftpService.ListDirectoryAsync(path);
            var folders = children.Where(c => c.IsDirectory).ToList();

            Logger.Debug($"ExpandFolderAsync: '{path}' got {children.Count} children, {folders.Count} folders");
            if (folders.Count == 0)
            {
                target.Children.Add(new SftpEntry
                {
                    Name = "<empty>",
                    FullPath = "",
                    IsDirectory = false,
                    IsPlaceholder = true,
                    IsLastChild = true
                });
            }
            else
            {
                for (int i = 0; i < folders.Count; i++)
                {
                    folders[i].IsLastChild = i >= folders.Count - 1;
                    target.Children.Add(folders[i]);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"ExpandFolderAsync: could not list '{path}' — {ex.Message}");
            target?.Children.Add(new SftpEntry
            {
                Name = "<unavailable>",
                FullPath = "",
                IsDirectory = false,
                IsPlaceholder = true,
                IsLastChild = true
            });
        }
    }

    [RelayCommand]
    private async Task NavigateToPathAsync(string? path)
    {
        Logger.Debug($"NavigateToPathAsync: '{path}'");
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusSeverity = ToolbarStatusSeverity.Warning;
            StatusMessage = "Navigation failed: path is empty";
            return;
        }

        try
        {
            StatusSeverity = ToolbarStatusSeverity.None;
            StatusMessage = string.Empty;
            CurrentPath = path;
            var entries = await _sftpService.ListDirectoryAsync(path);

            Dispatcher.UIThread.Post(() =>
            {
                CurrentEntries.Clear();
                var parentDir = GetParentPath(path);
                if (parentDir is not null)
                {
                    CurrentEntries.Add(new SftpEntry
                    {
                        Name = "..",
                        FullPath = parentDir,
                        IsDirectory = true,
                        IsPlaceholder = true,
                        IsLastChild = true
                    });
                }
                foreach (var e in entries)
                    CurrentEntries.Add(e);
                Logger.Debug($"NavigateToPathAsync: loaded {entries.Count} entries for '{path}'");
                OnPropertyChanged(nameof(CanRefresh));
            });

            Dispatcher.UIThread.Post(() =>
            {
                Logger.Trace("NavigateToPathAsync: post-nav focus");
                FocusFileList?.Invoke();
            });

            var targetEntry = FindEntry(TreeRoots, path);
            if (targetEntry is not null)
                ScrollToEntry?.Invoke(targetEntry);
        }
        catch (Exception ex)
        {
            Logger.Warn($"NavigateToPathAsync: could not navigate to '{path}' — {ex.Message}");
            StatusSeverity = ToolbarStatusSeverity.Warning;
            StatusMessage = $"Could not open: {path}";
        }
    }

    public async Task ExpandTreeToPathAsync(string path)
    {
        Logger.Debug($"ExpandTreeToPathAsync: '{path}'");
        var norm = path.TrimEnd('\\');
        var parts = norm.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var built = parts[0] + "\\";
        var current = TreeRoots.FirstOrDefault(e =>
            e.FullPath.Equals(built, StringComparison.OrdinalIgnoreCase));
        if (current is null)
        {
            Logger.Trace($"ExpandTreeToPathAsync: no root matched '{built}'");
            return;
        }

        for (int i = 1; i < parts.Length; i++)
        {
            Logger.Trace($"ExpandTreeToPathAsync: level {i}, expanding '{built}'");
            if (!current.HasLoaded)
                await ExpandFolderAsync(built);

            current.IsExpanded = true;

            built = built.TrimEnd('\\') + "\\" + parts[i];
            current = current.Children.FirstOrDefault(e =>
                e.FullPath.Equals(built, StringComparison.OrdinalIgnoreCase));
            if (current is null)
            {
                Logger.Trace($"ExpandTreeToPathAsync: path break at '{built}'");
                break;
            }
        }

        if (current is not null && current.IsDirectory)
        {
            if (!current.HasLoaded && current.Children.Count > 0)
                await ExpandFolderAsync(built);
            current.IsExpanded = true;
        }

        Logger.Debug($"ExpandTreeToPathAsync: done for '{path}'");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Logger.Debug($"RefreshAsync: CurrentPath='{CurrentPath}'");
        if (string.IsNullOrWhiteSpace(CurrentPath)) return;

        try
        {
            StatusSeverity = ToolbarStatusSeverity.None;
            StatusMessage = string.Empty;

            // Save expanded paths before clearing tree cache
            var expandedPaths = CollectExpandedPaths(TreeRoots);
            ClearTreeCache(TreeRoots);
            Logger.Debug($"RefreshAsync: {expandedPaths.Count} expanded paths saved, cache cleared");

            // Reload current file list
            var entries = await _sftpService.ListDirectoryAsync(CurrentPath);

            Dispatcher.UIThread.Post(() =>
            {
                CurrentEntries.Clear();
                foreach (var e in entries)
                    CurrentEntries.Add(e);
                Logger.Debug($"RefreshAsync: reloaded {entries.Count} entries");
                OnPropertyChanged(nameof(CanRefresh));
            });

            // Re-expand previously expanded paths (parents before children)
            expandedPaths.Sort((a, b) => a.Length.CompareTo(b.Length));
            foreach (var path in expandedPaths)
            {
                await ExpandTreeToPathAsync(path);
            }

            Logger.Debug("RefreshAsync: tree refreshed");
        }
        catch (Exception ex)
        {
            Logger.Warn($"RefreshAsync: failed for '{CurrentPath}' — {ex.Message}");
            StatusSeverity = ToolbarStatusSeverity.Warning;
            StatusMessage = $"Refresh failed: {CurrentPath}";
        }
    }

    private static List<string> CollectExpandedPaths(ObservableCollection<SftpEntry> entries)
    {
        var paths = new List<string>();
        foreach (var e in entries)
        {
            if (e.IsExpanded)
                paths.Add(e.FullPath);
            if (e.IsExpanded && e.Children.Count > 0)
                paths.AddRange(CollectExpandedPaths(e.Children));
        }
        return paths;
    }

    private static void ClearTreeCache(ObservableCollection<SftpEntry> entries)
    {
        foreach (var e in entries)
        {
            e.HasLoaded = false;
            if (e.Children.Count > 0)
                ClearTreeCache(e.Children);
        }
    }

    [RelayCommand]
    private async Task UploadFilesAsync(string[]? filePaths)
    {
        if (filePaths is null || filePaths.Length == 0) return;

        _currentTransferCts = new CancellationTokenSource();
        var ct = _currentTransferCts.Token;

        try
        {
            foreach (var filePath in filePaths)
            {
                if (ct.IsCancellationRequested) break;

                string? remotePath = null;
                try
                {
                    var fileName = Path.GetFileName(filePath);
                    IsUploading = true;
                    UploadProgress = 0;
                    _transferStartTime = DateTime.UtcNow;
                    _transferBytesTotal = 0;
                    UploadStatusText = $"Uploading {fileName}... (0%)";

                    remotePath = CurrentPath.TrimEnd('\\') + "\\" + fileName;

                    await using var stream = File.OpenRead(filePath);
                    _transferBytesTotal = stream.Length;
                    var progress = new Progress<double>(p =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            UploadProgress = p;
                            UploadStatusText = $"Uploading {fileName}... ({p * 100:F0}%){FormatSpeed(p)}";
                        });
                    });

                    await _sftpService.UploadFileAsync(stream, remotePath, progress, ct);

                    var newEntry = new SftpEntry
                    {
                        Name = fileName,
                        FullPath = remotePath,
                        IsDirectory = false,
                        Size = 0,
                        LastModified = DateTime.MinValue
                    };

                    var existing = CurrentEntries.FirstOrDefault(e => !e.IsDirectory && e.Name == fileName);
                    if (existing is not null) CurrentEntries.Remove(existing);
                    var ph = CurrentEntries.FirstOrDefault(e => e.IsPlaceholder);
                    if (ph is not null) CurrentEntries.Remove(ph);
                    InsertSorted(CurrentEntries, newEntry);

                    var parentNode = FindEntry(TreeRoots, CurrentPath);
                    if (parentNode is not null && parentNode.HasLoaded)
                    {
                    var existing2 = parentNode.Children.FirstOrDefault(e => !e.IsDirectory && e.Name == fileName);
                    if (existing2 is not null) parentNode.Children.Remove(existing2);
                    var ph2 = parentNode.Children.FirstOrDefault(e => e.IsPlaceholder);
                    if (ph2 is not null) parentNode.Children.Remove(ph2);
                    InsertSorted(parentNode.Children, newEntry);
                }

                StatusSeverity = ToolbarStatusSeverity.Success;
                StatusMessage = $"{fileName} uploaded";
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"Upload cancelled: {Path.GetFileName(filePath)}");
                if (remotePath is not null)
                {
                    try { await _sftpService.DeleteFileAsync(remotePath); }
                    catch { /* best-effort cleanup */ }
                }
                StatusSeverity = ToolbarStatusSeverity.None;
                StatusMessage = $"{Path.GetFileName(filePath)} cancelled";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Upload failed: {filePath}");
                StatusSeverity = ToolbarStatusSeverity.Error;
                StatusMessage = $"Upload failed: {Path.GetFileName(filePath)}: {ex.Message}";
            }
        }

        IsUploading = false;
        UploadStatusText = string.Empty;
        }
        finally
        {
            _currentTransferCts?.Dispose();
            _currentTransferCts = null;
        }
    }

    [RelayCommand]
    private async Task DownloadSelectedAsync()
    {
        var entries = SelectedEntries.Where(e => !e.IsDrive && !e.IsPlaceholder).ToList();
        if (entries.Count == 0) return;

        // Single entry: file → save dialog, folder → folder picker
        if (entries.Count == 1)
        {
            if (entries[0].IsDirectory)
                await DownloadFolderAsync(entries[0]);
            else
                await DownloadSingleFileAsync(entries[0]);
            return;
        }

        var localDir = ShowFolderPickerAsync is not null ? await ShowFolderPickerAsync() : null;
        if (string.IsNullOrEmpty(localDir)) return;

        Directory.CreateDirectory(localDir);

        // Build unified file list: direct files + recursive from folders
        var fileList = new List<(SftpEntry Entry, string RelativePath)>();
        _currentTransferCts = new CancellationTokenSource();
        var ct = _currentTransferCts.Token;
        IsDownloading = true;
        DownloadProgress = 0;
        DownloadStatusText = "Scanning folders...";

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (!entry.IsDirectory)
            {
                fileList.Add((entry, entry.Name));
            }
            else
            {
                try
                {
                    DownloadStatusText = $"Scanning {entry.Name}...";
                    var all = await _sftpService.RecursiveListAsync(entry.FullPath);
                    var folderRoot = entry.FullPath.TrimEnd('\\');
                    foreach (var file in all.Where(e => !e.IsDirectory))
                    {
                        var relative = file.FullPath.Substring(folderRoot.Length).TrimStart('\\');
                        fileList.Add((file, relative));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to scan folder: {entry.Name}");
                    StatusSeverity = ToolbarStatusSeverity.Error;
                    StatusMessage = $"Failed to scan {entry.Name}: {ex.Message}";
                }
            }
        }

        var totalFiles = fileList.Count;
        if (totalFiles == 0)
        {
            IsDownloading = false;
            _currentTransferCts?.Dispose();
            _currentTransferCts = null;
            StatusSeverity = ToolbarStatusSeverity.Info;
            StatusMessage = "Nothing to download";
            return;
        }

        string? partialPath = null;
        try
        {
            for (int i = 0; i < totalFiles; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (file, relative) = fileList[i];
                DownloadStatusText = $"Downloading {file.Name}... ({i + 1}/{totalFiles})";
                DownloadProgress = (double)i / totalFiles;

                partialPath = Path.Combine(localDir, relative);
                var parentDir = Path.GetDirectoryName(partialPath);
                if (!string.IsNullOrEmpty(parentDir))
                    Directory.CreateDirectory(parentDir);

                await using var stream = File.Create(partialPath);
                await _sftpService.DownloadFileAsync(file.FullPath, stream, null, ct);

                DownloadProgress = (double)(i + 1) / totalFiles;
                StatusSeverity = ToolbarStatusSeverity.Success;
                StatusMessage = $"{file.Name} downloaded ({i + 1}/{totalFiles})";
                partialPath = null;
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Warn("Multi-file download cancelled");
            if (partialPath is not null && File.Exists(partialPath))
                File.Delete(partialPath);
            StatusSeverity = ToolbarStatusSeverity.None;
            StatusMessage = "Download cancelled";
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
            DownloadStatusText = string.Empty;
            _currentTransferCts?.Dispose();
            _currentTransferCts = null;
        }
    }

    private async Task DownloadSingleFileAsync(SftpEntry entry)
    {
        string? savePath = null;
        try
        {
            savePath = ShowSaveFileDialogAsync is not null ? await ShowSaveFileDialogAsync(entry) : null;
            if (string.IsNullOrEmpty(savePath)) return;

            _currentTransferCts = new CancellationTokenSource();
            var ct = _currentTransferCts.Token;

            IsDownloading = true;
            DownloadProgress = 0;
            _transferStartTime = DateTime.UtcNow;
            _transferBytesTotal = 0;
            DownloadStatusText = $"Downloading {entry.Name}... (0%)";

            await using var stream = File.Create(savePath);
            var progress = new Progress<double>(p =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    DownloadProgress = p;
                    DownloadStatusText = $"Downloading {entry.Name}... ({p * 100:F0}%){FormatSpeed(p)}";
                });
            });

            _transferBytesTotal = await _sftpService.DownloadFileAsync(entry.FullPath, stream, progress, ct);
            StatusSeverity = ToolbarStatusSeverity.Success;
            StatusMessage = $"{entry.Name} downloaded";
        }
        catch (OperationCanceledException)
        {
            Logger.Warn($"Download cancelled: {entry.Name}");
            if (savePath is not null && File.Exists(savePath))
                File.Delete(savePath);
            StatusSeverity = ToolbarStatusSeverity.None;
            StatusMessage = $"{entry.Name} cancelled";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Download failed: {entry.Name}");
            StatusSeverity = ToolbarStatusSeverity.Error;
            StatusMessage = $"Download failed: {entry.Name}: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
            DownloadStatusText = string.Empty;
            _currentTransferCts?.Dispose();
            _currentTransferCts = null;
        }
    }

    private async Task DownloadFolderAsync(SftpEntry entry)
    {
        var localRoot = ShowFolderPickerAsync is not null ? await ShowFolderPickerAsync() : null;
        if (string.IsNullOrEmpty(localRoot)) return;

        try
        {
            IsDownloading = true;
            DownloadProgress = 0;
            DownloadStatusText = "Listing files...";

            var allEntries = await _sftpService.RecursiveListAsync(entry.FullPath);
            var files = allEntries.Where(e => !e.IsDirectory).ToList();
            var totalFiles = files.Count;

            if (totalFiles == 0)
            {
                StatusSeverity = ToolbarStatusSeverity.Info;
                StatusMessage = "Empty folder — nothing to download";
                return;
            }

            _currentTransferCts = new CancellationTokenSource();
            var ct = _currentTransferCts.Token;

            Directory.CreateDirectory(localRoot);
            var rootPath = entry.FullPath.TrimEnd('\\');
            string? partialPath = null;

            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var file = files[i];
                    var relative = file.FullPath.Substring(rootPath.Length).TrimStart('\\');
                    partialPath = Path.Combine(localRoot, relative);
                    var localDir = Path.GetDirectoryName(partialPath);
                    if (!string.IsNullOrEmpty(localDir))
                        Directory.CreateDirectory(localDir);

                    var idx = i;
                    Dispatcher.UIThread.Post(() =>
                    {
                        DownloadStatusText = $"Downloading {file.Name}... ({idx + 1}/{totalFiles})";
                    });

                    await using var stream = File.Create(partialPath);
                    await _sftpService.DownloadFileAsync(file.FullPath, stream, null, ct);

                    Dispatcher.UIThread.Post(() =>
                    {
                        DownloadProgress = (double)(idx + 1) / totalFiles;
                    });
                    partialPath = null;
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"Folder download cancelled: {entry.Name}");
                if (partialPath is not null && File.Exists(partialPath))
                    File.Delete(partialPath);
                StatusSeverity = ToolbarStatusSeverity.None;
                StatusMessage = $"{entry.Name} cancelled";
                return;
            }

            StatusSeverity = ToolbarStatusSeverity.Success;
            StatusMessage = $"{totalFiles} files downloaded";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Folder download failed: {entry.Name}");
            StatusSeverity = ToolbarStatusSeverity.Error;
            StatusMessage = $"Folder download failed: {entry.Name}: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
            DownloadStatusText = string.Empty;
            _currentTransferCts?.Dispose();
            _currentTransferCts = null;
        }
    }

    private void RemoveFromTreeAndList(SftpEntry entry)
    {
        var treeEntry = FindEntry(TreeRoots, entry.FullPath);
        if (treeEntry is not null)
        {
            var parent = FindParent(TreeRoots, treeEntry);
            if (parent is not null)
            {
                parent.Children.Remove(treeEntry);
                if (parent.Children.Count == 0)
                {
                    parent.Children.Add(new SftpEntry
                    {
                        Name = "<empty>", FullPath = "",
                        IsDirectory = false, IsPlaceholder = true, IsLastChild = true
                    });
                }
                else
                {
                    UpdateLastChildFlag(parent.Children);
                }
            }
        }
        var listEntry = CurrentEntries.FirstOrDefault(e => e.FullPath == entry.FullPath);
        if (listEntry is not null)
            CurrentEntries.Remove(listEntry);
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var entries = SelectedEntries.ToList();
        if (entries.Count == 0) return;

        var confirmed = ShowDeleteConfirmAsync is not null
            ? await ShowDeleteConfirmAsync(entries)
            : false;
        if (!confirmed) return;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            try
            {
                StatusSeverity = ToolbarStatusSeverity.None;
                StatusMessage = $"Deleting {entry.Name}... ({i + 1}/{entries.Count})";

                if (entry.IsDirectory)
                    await _sftpService.DeleteDirectoryAsync(entry.FullPath);
                else
                    await _sftpService.DeleteFileAsync(entry.FullPath);

                RemoveFromTreeAndList(entry);

                StatusSeverity = ToolbarStatusSeverity.Success;
                StatusMessage = $"{entry.Name} deleted ({i + 1}/{entries.Count})";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Delete failed: {entry.Name}");
                StatusSeverity = ToolbarStatusSeverity.Error;
                StatusMessage = $"Delete failed: {entry.Name}: {ex.Message}";
            }
        }

        StatusMessage = $"{entries.Count} item(s) deleted";
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        var parentPath = SelectedEntry?.IsDirectory == true
            ? SelectedEntry.FullPath
            : CurrentPath;
        Logger.Debug($"CreateFolderAsync: parentPath='{parentPath}'");

        var name = ShowInputDialogAsync is not null
            ? await ShowInputDialogAsync("New Folder", $"Enter folder name:\nLocation: {parentPath}", "New Folder",
                "avares://XBVault/Assets/Views/InputDialog/inputdialog-newfolder-48.png")
            : null;

        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            var dir = parentPath.TrimEnd('\\') + "\\" + name;
            await _sftpService.CreateDirectoryAsync(dir);

            var newFolder = new SftpEntry
            {
                Name = name, FullPath = dir,
                IsDirectory = true,
                Children = { new SftpEntry { Name = "" } }
            };

            if (parentPath == CurrentPath)
            {
                var ph = CurrentEntries.FirstOrDefault(e => e.IsPlaceholder);
                if (ph is not null) CurrentEntries.Remove(ph);
                InsertSorted(CurrentEntries, newFolder);
            }

            var parentNode = FindEntry(TreeRoots, parentPath);
            if (parentNode is not null && parentNode.HasLoaded)
            {
                var ph = parentNode.Children.FirstOrDefault(e => e.IsPlaceholder);
                if (ph is not null) parentNode.Children.Remove(ph);
                InsertSorted(parentNode.Children, newFolder);
            }

            StatusSeverity = ToolbarStatusSeverity.Success;
            StatusMessage = $"Folder \"{name}\" created";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Create folder failed: {name}");
            StatusSeverity = ToolbarStatusSeverity.Error;
            StatusMessage = $"Create folder failed: {name}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RenameEntryAsync()
    {
        var entry = SelectedEntries.Count == 1 ? SelectedEntries[0] : null;
        entry ??= SelectedEntry;
        Logger.Debug($"RenameEntryAsync: '{entry?.FullPath}'");
        if (entry is null || entry.IsPlaceholder || entry.IsDrive) return;

        var newName = ShowInputDialogAsync is not null
            ? await ShowInputDialogAsync("Rename", $"Enter new name for \"{entry.Name}\":", entry.Name,
                "avares://XBVault/Assets/Views/InputDialog/inputdialog-rename-48.png")
            : null;

        if (string.IsNullOrWhiteSpace(newName) || newName == entry.Name) return;

        try
        {
            var parentDir = Path.GetDirectoryName(entry.FullPath)?.Replace('/', '\\') ?? "";
            var newPath = parentDir.TrimEnd('\\') + "\\" + newName;
            var oldPath = entry.FullPath;
            await _sftpService.RenameAsync(entry.FullPath, newPath);

            entry.Name = newName;
            entry.FullPath = newPath;

            if (entry.IsDirectory)
                UpdateChildrenPathsRecursive(entry, oldPath);

            var parentNode = FindParent(TreeRoots, entry);
            if (parentNode is not null)
            {
                parentNode.Children.Remove(entry);
                InsertSorted(parentNode.Children, entry);
            }

            if (CurrentEntries.Contains(entry))
            {
                CurrentEntries.Remove(entry);
                InsertSorted(CurrentEntries, entry);
            }

            StatusSeverity = ToolbarStatusSeverity.Success;
            StatusMessage = $"Renamed to \"{newName}\"";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Rename failed: {entry.Name}");
            StatusSeverity = ToolbarStatusSeverity.Error;
            StatusMessage = $"Rename failed: {entry.Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenConnectionInfoAsync()
    {
        if (_xboxService.IsConnected)
        {
            var creds = _xboxService.GetSshCredentials();
            var pw = _xboxService.SmbPassword;
            if (string.IsNullOrEmpty(pw))
                pw = await _xboxService.FetchSmbPasswordAsync();
            pw ??= creds.Password;
            if (ShowConnectionInfoAsync is not null)
                await ShowConnectionInfoAsync(creds.Host, creds.Username, pw, creds.Port);
        }
    }

    [RelayCommand]
    private Task OpenWinScpAsync()
    {
        if (!_xboxService.IsConnected || !IsWindows) return Task.CompletedTask;
        var creds = _xboxService.GetSshCredentials();
        var pw = _sftpPassword ?? creds.Password;
        var url = $"sftp://{creds.Username}:{pw}@{creds.Host}:{creds.Port}/";

        var exe = FindWinScp();
        if (exe is null)
        {
            Logger.Warn("WinSCP executable not found, offering download");
            ShowWinScpNotFoundDialog?.Invoke(
                "WinSCP not found",
                "Could not find WinSCP installation. Click Download to install WinSCP, then try again.",
                url);
            return Task.CompletedTask;
        }

        try
        {
            System.Diagnostics.Process.Start(exe, url);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to launch WinSCP");
            ShowErrorDialog?.Invoke(
                "Failed to launch WinSCP",
                $"WinSCP was found at {exe} but could not be launched.",
                $"Path: {exe}\n\nSFTP URL: {url}\n\nError: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public Action<string, string, string>? ShowWinScpNotFoundDialog { get; set; }

    private static string? FindWinScp()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinSCP", "WinSCP.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinSCP", "WinSCP.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinSCP", "WinSCP.exe"),
        };

        foreach (var p in candidates)
        {
            if (File.Exists(p))
                return p;
        }

        try
        {
            using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "where",
                Arguments = "winscp.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (proc is not null)
            {
                var line = proc.StandardOutput.ReadLine();
                proc.WaitForExit(2000);
                if (!string.IsNullOrEmpty(line) && File.Exists(line))
                    return line;
            }
        }
        catch { }

        return null;
    }

    private static SftpEntry? FindEntry(ObservableCollection<SftpEntry> entries, string path)
    {
        Logger.Trace($"FindEntry: searching for '{path}' in {entries.Count} root entries");
        foreach (var e in entries)
        {
            if (e.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Trace($"FindEntry: found '{e.FullPath}'");
                return e;
            }
            if (e.Children.Count > 0)
            {
                var found = FindEntry(e.Children, path);
                if (found is not null)
                    return found;
            }
        }
        Logger.Trace($"FindEntry: not found '{path}'");
        return null;
    }

    private static string? GetParentPath(string path)
    {
        var trimmed = path.TrimEnd('\\');
        var idx = trimmed.LastIndexOf('\\');
        if (idx <= 0) return null;
        return trimmed[..idx] + "\\";
    }

    private static SftpEntry? FindParent(ObservableCollection<SftpEntry> entries, SftpEntry target)
    {
        foreach (var e in entries)
        {
            if (e.Children.Contains(target))
                return e;
            if (e.Children.Count > 0)
            {
                var found = FindParent(e.Children, target);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }
}

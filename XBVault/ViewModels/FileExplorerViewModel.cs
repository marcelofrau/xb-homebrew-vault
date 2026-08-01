using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Helpers;
using XBVault.Models;
using XBVault.Services;
using static XBVault.Helpers.FileSystemPathParser;

namespace XBVault.ViewModels;

public enum ToolbarStatusSeverity { None, Info, Success, Warning, Error }

public partial class FileExplorerViewModel : ObservableObject, IDisposable
{
    private readonly IXboxAuthService _authService;
    private readonly SftpService _sftpService;
    private readonly SftpTransferService _transfer;
    internal SftpService SftpService => _sftpService;
    private string? _sftpPassword;
    private CancellationTokenSource? _deleteCts;
    private string? _uploadTargetPath;

    public void Dispose()
    {
        _deleteCts?.Cancel();
        _deleteCts?.Dispose();
        GC.SuppressFinalize(this);
    }

    public FileExplorerViewModel(IXboxAuthService authService, SftpService sftpService, SftpTransferService transfer)
    {
        _authService = authService;
        _sftpService = sftpService;
        _transfer = transfer;
        _authService.ConnectionChanged += OnBoxConnectionChanged;
        _sftpService.ConnectionChanged += OnSftpConnectionChanged;
        IsConnected = _authService.IsConnected;
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
    public Func<string, string, string, string, Task<bool>>? ShowConfirmAction { get; set; }
    public Func<string, Task>? OpenCustomInstallWithFileAction { get; set; }

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
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isLoading;

    [ObservableProperty]
    private SftpEntry? _selectedEntry;

    public ObservableCollection<SftpEntry> SelectedEntries { get; } = [];

    public bool CanModifyFiles => !ShowActivity;
    public bool CanDeleteMultiple => SelectedEntries.Count > 0 && CanModifyFiles;
    public bool CanDownloadMultiple => SelectedEntries.Any(e => !e.IsDrive) && CanModifyFiles;
    public bool CanRenameSingle => SelectedEntries.Count == 1 && CanModifyFiles;
    public string OperationLockedTooltip => ShowActivity ? "Waiting for current operation to finish..." : string.Empty;

    [ObservableProperty]
    private string _currentPath = @"D:\";

    public bool CanGoUp => GetParentPath(CurrentPath) is not null;

    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(CanDeleteMultiple));
        OnPropertyChanged(nameof(CanDownloadMultiple));
        OnPropertyChanged(nameof(CanRenameSingle));
    }

    public bool CanCancelTransfer => IsUploading || IsDownloading || IsDeleting;

    [RelayCommand]
    private void CancelTransfer()
    {
        Logger.Debug("CancelTransfer: requesting cancellation");
        _transfer.CancelTransfer();
        _deleteCts?.Cancel();
        _deleteCts?.Dispose();
        _deleteCts = null;
    }

    [ObservableProperty]
    private string _uploadStatusText = string.Empty;

    [ObservableProperty]
    private double _uploadProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isUploading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatusText = string.Empty;

    [ObservableProperty]
    private bool _showAllDrives;

    partial void OnShowAllDrivesChanged(bool value)
    {
        Logger.Debug($"ShowAllDrives changed to {value}, reloading drives...");
        _ = LoadTreeRootsAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isDeleting;

    [ObservableProperty]
    private double _deleteProgress;

    [ObservableProperty]
    private string _deleteStatusText = string.Empty;

    public bool ShowActivity => IsUploading || IsDownloading || IsDeleting;
    public bool ShowIdle => !ShowActivity;
    public double ActivityProgress => IsDeleting ? DeleteProgress : IsUploading ? UploadProgress : DownloadProgress;
    public string ActivityText => IsDeleting ? DeleteStatusText : IsUploading ? UploadStatusText : DownloadStatusText;

    public Cursor? Cursor => (IsLoading || IsUploading || IsDownloading || IsDeleting) ? AppStartingCursor : null;

    private static readonly Cursor AppStartingCursor = new(StandardCursorType.AppStarting);

    private void NotifyFileLockProperties()
    {
        OnPropertyChanged(nameof(CanModifyFiles));
        OnPropertyChanged(nameof(CanDeleteMultiple));
        OnPropertyChanged(nameof(CanDownloadMultiple));
        OnPropertyChanged(nameof(CanRenameSingle));
        OnPropertyChanged(nameof(OperationLockedTooltip));
        OnPropertyChanged(nameof(CanBrowse));
        OnPropertyChanged(nameof(CanRefresh));
    }

    partial void OnIsUploadingChanged(bool value) { OnPropertyChanged(nameof(ShowActivity)); OnPropertyChanged(nameof(ShowIdle)); OnPropertyChanged(nameof(CanCancelTransfer)); NotifyFileLockProperties(); }
    partial void OnIsDownloadingChanged(bool value) { OnPropertyChanged(nameof(ShowActivity)); OnPropertyChanged(nameof(ShowIdle)); OnPropertyChanged(nameof(CanCancelTransfer)); NotifyFileLockProperties(); }
    partial void OnIsDeletingChanged(bool value) { OnPropertyChanged(nameof(ShowActivity)); OnPropertyChanged(nameof(ShowIdle)); OnPropertyChanged(nameof(CanCancelTransfer)); NotifyFileLockProperties(); }
    partial void OnUploadProgressChanged(double value) => OnPropertyChanged(nameof(ActivityProgress));
    partial void OnDownloadProgressChanged(double value) => OnPropertyChanged(nameof(ActivityProgress));
    partial void OnDeleteProgressChanged(double value) => OnPropertyChanged(nameof(ActivityProgress));
    partial void OnUploadStatusTextChanged(string value) => OnPropertyChanged(nameof(ActivityText));
    partial void OnDownloadStatusTextChanged(string value) => OnPropertyChanged(nameof(ActivityText));
    partial void OnDeleteStatusTextChanged(string value) => OnPropertyChanged(nameof(ActivityText));

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
    public bool CanBrowse => IsConnected && !IsLoading && CanModifyFiles;
    public bool CanRefresh => _sftpService.IsConnected && TreeRoots.Count > 0 && CanModifyFiles;
    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public string[] BreadcrumbSegments => BuildBreadcrumbSegments(CurrentPath);

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

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (ShowConnectAction is not null)
        {
            var ok = await ShowConnectAction();
            if (ok)
                _authService.MarkConnected();
        }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (!_authService.IsConnected || IsLoading || _sftpService.IsConnected)
        {
            Logger.Debug($"InitializeAsync: skipped (connected={_authService.IsConnected}, loading={IsLoading}, sftp={_sftpService.IsConnected})");
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            InitStepText = "Discovering credentials...";
            Logger.Debug("Fetching SMB password...");
            await _authService.FetchSmbPasswordAsync();

            InitStepText = "Connecting to Xbox via SFTP...";
            Logger.Debug("InitializeAsync: getting SSH credentials...");
            var creds = _authService.GetSshCredentials();
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

    private Task<List<SftpEntry>> DetectDrivesAsync()
    {
        var all = new[] { "C", "D", "E", "G", "J", "L", "M", "N", "Q", "S", "T", "U", "V", "X", "Y" };
        var letters = ShowAllDrives ? all : new[] { "D", "E" };
        var drives = letters.Select(l =>
        {
            var name = l == "E" ? "E:\\ (external)" : $"{l}:\\";
            var e = new SftpEntry
            {
                Name = name,
                FullPath = $"{l}:\\",
                IsDirectory = true,
                IsDrive = true,
                LastModified = DateTime.MinValue,
                IconName = l == "E" ? null : "ssd",
                ToolTip = l == "E" ? "External USB storage drive" : null
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
                Logger.Trace($"ExpandFolderAsync: '{path}' no folders found, collapsing");
                target.IsExpanded = false;
                return;
            }
            for (int i = 0; i < folders.Count; i++)
            {
                folders[i].IsLastChild = i >= folders.Count - 1;
                target.Children.Add(folders[i]);
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
            FocusFileList?.Invoke();
            Logger.Trace("NavigateToPathAsync: post-nav focus");

            await ExpandTreeToPathAsync(path);

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

    [RelayCommand]
    private async Task UploadFilesAsync(string[]? filePaths)
    {
        if (filePaths is null || filePaths.Length == 0) return;

        Logger.Info($"UploadFilesAsync: uploading {filePaths.Length} file(s) to '{CurrentPath}'");
        for (int i = 0; i < filePaths.Length; i++)
            Logger.Trace($"UploadFilesAsync: file[{i}] = '{filePaths[i]}'");

        IsUploading = true;
        UploadProgress = 0;
        _uploadTargetPath = CurrentPath;
        try
        {
            var result = await _transfer.UploadFilesAsync(filePaths, CurrentPath,
                TransferProgress(u => { UploadProgress = u.Progress; UploadStatusText = u.StatusText; }));
            ApplyUploadResult(result);
        }
        finally
        {
            _uploadTargetPath = null;
            IsUploading = false;
            UploadProgress = 0;
            UploadStatusText = string.Empty;
        }
    }

    [RelayCommand]
    private async Task UploadFolderAsync(string? folderPath = null)
    {
        var localFolder = folderPath ?? (ShowFolderPickerAsync is not null ? await ShowFolderPickerAsync() : null);
        if (string.IsNullOrEmpty(localFolder)) return;

        Logger.Info($"UploadFolderAsync: uploading folder '{localFolder}' to '{CurrentPath}'");

        IsUploading = true;
        UploadProgress = 0;
        _uploadTargetPath = CurrentPath;
        try
        {
            var result = await _transfer.UploadFolderAsync(localFolder, CurrentPath,
                TransferProgress(u => { UploadProgress = u.Progress; UploadStatusText = u.StatusText; }));
            ApplyUploadResult(result);
        }
        finally
        {
            _uploadTargetPath = null;
            IsUploading = false;
            UploadProgress = 0;
            UploadStatusText = string.Empty;
        }
    }

    public async Task UploadMixedAsync(string[] filePaths, string[] folderPaths)
    {
        if ((filePaths is null || filePaths.Length == 0) && (folderPaths is null || folderPaths.Length == 0))
            return;

        var fCount = filePaths?.Length ?? 0;
        var dCount = folderPaths?.Length ?? 0;
        Logger.Info($"UploadMixedAsync: {fCount} file(s), {dCount} folder(s) to '{CurrentPath}'");

        IsUploading = true;
        UploadProgress = 0;
        _uploadTargetPath = CurrentPath;
        try
        {
            var result = await _transfer.UploadMixedAsync(filePaths, folderPaths, CurrentPath,
                TransferProgress(u => { UploadProgress = u.Progress; UploadStatusText = u.StatusText; }));
            ApplyUploadResult(result);
        }
        finally
        {
            _uploadTargetPath = null;
            IsUploading = false;
            UploadProgress = 0;
            UploadStatusText = string.Empty;
        }
    }

    private void AddToCurrentAndTree(SftpEntry newEntry)
    {
        if (_uploadTargetPath is not null && CurrentPath != _uploadTargetPath)
        {
            Logger.Trace($"AddToCurrentAndTree: skipped (path changed '{CurrentPath}' != '{_uploadTargetPath}')");
            return;
        }
        Logger.Trace($"AddToCurrentAndTree: '{newEntry.FullPath}' (IsDir={newEntry.IsDirectory})");
        var existing = CurrentEntries.FirstOrDefault(e => !e.IsPlaceholder && e.Name == newEntry.Name && e.IsDirectory == newEntry.IsDirectory);
        if (existing is not null) { CurrentEntries.Remove(existing); Logger.Trace($"AddToCurrentAndTree: removed existing '{existing.FullPath}' from CurrentEntries"); }
        var ph = CurrentEntries.FirstOrDefault(e => e.IsPlaceholder);
        if (ph is not null) { CurrentEntries.Remove(ph); Logger.Trace("AddToCurrentAndTree: removed placeholder from CurrentEntries"); }
        InsertSorted(CurrentEntries, newEntry);

        if (newEntry.IsDirectory)
        {
            var parentNode = FindEntry(TreeRoots, CurrentPath);
            if (parentNode is not null && parentNode.HasLoaded)
            {
                var existing2 = parentNode.Children.FirstOrDefault(e => !e.IsPlaceholder && e.Name == newEntry.Name && e.IsDirectory == newEntry.IsDirectory);
                if (existing2 is not null) { parentNode.Children.Remove(existing2); Logger.Trace($"AddToCurrentAndTree: removed existing '{existing2.FullPath}' from tree children"); }
                var ph2 = parentNode.Children.FirstOrDefault(e => e.IsPlaceholder);
                if (ph2 is not null) { parentNode.Children.Remove(ph2); Logger.Trace("AddToCurrentAndTree: removed placeholder from tree children"); }
                InsertSorted(parentNode.Children, newEntry);
            }
        }
        Logger.Trace($"AddToCurrentAndTree: '{newEntry.FullPath}' inserted");
    }

    private Progress<TransferUpdate> TransferProgress(Action<TransferUpdate> apply) =>
        new(apply);

    private void ApplyUploadResult(TransferResult result)
    {
        if (result.Cancelled)
        {
            StatusSeverity = ToolbarStatusSeverity.None;
            StatusMessage = result.StatusMessage ?? "Upload cancelled";
            return;
        }
        if (result.IsEmptyResult)
        {
            StatusSeverity = ToolbarStatusSeverity.Info;
            StatusMessage = result.StatusMessage ?? string.Empty;
            return;
        }
        if (result.Success)
        {
            StatusSeverity = ToolbarStatusSeverity.Success;
            StatusMessage = result.StatusMessage ?? string.Empty;
            foreach (var e in result.NewEntries)
                AddToCurrentAndTree(e);
            return;
        }
        StatusSeverity = ToolbarStatusSeverity.Error;
        StatusMessage = result.StatusMessage ?? "Upload failed";
    }

    private void ApplyDownloadResult(TransferResult result)
    {
        if (result.Cancelled)
        {
            StatusSeverity = ToolbarStatusSeverity.None;
            StatusMessage = result.StatusMessage ?? "Download cancelled";
            return;
        }
        if (result.IsEmptyResult)
        {
            StatusSeverity = ToolbarStatusSeverity.Info;
            StatusMessage = result.StatusMessage ?? string.Empty;
            return;
        }
        if (result.Success)
        {
            StatusSeverity = ToolbarStatusSeverity.Success;
            StatusMessage = result.StatusMessage ?? string.Empty;
            return;
        }
        StatusSeverity = ToolbarStatusSeverity.Error;
        StatusMessage = result.StatusMessage ?? "Download failed";
    }

    public async Task UploadZipExtractAsync(string zipPath)
    {
        Logger.Info($"UploadZipExtractAsync: extracting '{zipPath}' to '{CurrentPath}'");

        IsUploading = true;
        UploadProgress = 0;
        _uploadTargetPath = CurrentPath;
        try
        {
            var result = await _transfer.UploadZipExtractAsync(zipPath, CurrentPath,
                TransferProgress(u => { UploadProgress = u.Progress; UploadStatusText = u.StatusText; }));
            ApplyUploadResult(result);
        }
        finally
        {
            _uploadTargetPath = null;
            IsUploading = false;
            UploadProgress = 0;
            UploadStatusText = string.Empty;
        }
    }

    [RelayCommand]
    private async Task DownloadSelectedAsync()
    {
        var entries = SelectedEntries.Where(e => !e.IsDrive && !e.IsPlaceholder).ToList();
        if (entries.Count == 0 && SelectedEntry is not null && !SelectedEntry.IsPlaceholder)
        {
            Logger.Debug($"DownloadSelectedAsync: using TreeView SelectedEntry '{SelectedEntry.FullPath}'");
            entries = [SelectedEntry];
        }
        if (entries.Count == 0)
        {
            var trimmed = CurrentPath.TrimEnd('\\');
            if (trimmed.Length <= 2)
            {
                Logger.Info("DownloadSelectedAsync: no selection and at drive root — aborting");
                StatusSeverity = ToolbarStatusSeverity.Info;
                StatusMessage = "Select a folder to download";
                return;
            }
            Logger.Info($"DownloadSelectedAsync: fallback to current path '{CurrentPath}'");
            var fallback = new SftpEntry
            {
                Name = trimmed.Split('\\').Last(),
                FullPath = CurrentPath,
                IsDirectory = true
            };
            await DownloadFolderAsync(fallback);
            return;
        }

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
        Logger.Info($"DownloadSelectedAsync: multi-file download to '{localDir}'");

        IsDownloading = true;
        DownloadProgress = 0;
        try
        {
            var result = await _transfer.DownloadFilesAsync(entries, localDir,
                TransferProgress(u => { DownloadProgress = u.Progress; DownloadStatusText = u.StatusText; }));
            ApplyDownloadResult(result);
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
            DownloadStatusText = string.Empty;
        }
    }

    private async Task DownloadSingleFileAsync(SftpEntry entry)
    {
        Logger.Info($"DownloadSingleFileAsync: '{entry.FullPath}'");
        var savePath = ShowSaveFileDialogAsync is not null ? await ShowSaveFileDialogAsync(entry) : null;
        if (string.IsNullOrEmpty(savePath)) return;

        IsDownloading = true;
        DownloadProgress = 0;
        try
        {
            var result = await _transfer.DownloadSingleFileAsync(entry, savePath,
                TransferProgress(u => { DownloadProgress = u.Progress; DownloadStatusText = u.StatusText; }));
            ApplyDownloadResult(result);
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
            DownloadStatusText = string.Empty;
        }
    }

    private async Task DownloadFolderAsync(SftpEntry entry)
    {
        var localRoot = ShowFolderPickerAsync is not null ? await ShowFolderPickerAsync() : null;
        if (string.IsNullOrEmpty(localRoot)) return;

        IsDownloading = true;
        DownloadProgress = 0;
        try
        {
            var result = await _transfer.DownloadFolderAsync(entry, localRoot,
                TransferProgress(u => { DownloadProgress = u.Progress; DownloadStatusText = u.StatusText; }));
            ApplyDownloadResult(result);
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
            DownloadStatusText = string.Empty;
        }
    }

    private void RemoveFromTreeAndList(SftpEntry entry)
    {
        Logger.Trace($"RemoveFromTreeAndList: '{entry.FullPath}'");
        var parentPath = GetParentPath(entry.FullPath);
        if (parentPath is not null)
        {
            var parentNode = FindEntry(TreeRoots, parentPath);
            if (parentNode is not null && parentNode.HasLoaded)
            {
                var child = parentNode.Children.FirstOrDefault(e =>
                    e.FullPath.Equals(entry.FullPath, StringComparison.OrdinalIgnoreCase));
                if (child is not null)
                {
                    parentNode.Children.Remove(child);
                    if (parentNode.Children.Count > 0)
                        UpdateLastChildFlag(parentNode.Children);
                }
            }
        }
        var listEntry = CurrentEntries.FirstOrDefault(e => e.FullPath == entry.FullPath);
        if (listEntry is not null)
        {
            CurrentEntries.Remove(listEntry);
            Logger.Trace($"RemoveFromTreeAndList: removed from CurrentEntries");
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var entries = SelectedEntries.Where(e => !e.IsPlaceholder).ToList();
        if (entries.Count == 0 && SelectedEntry is not null && !SelectedEntry.IsPlaceholder && !SelectedEntry.IsDrive)
        {
            Logger.Debug($"DeleteSelectedAsync: no ListBox entries, using TreeView SelectedEntry '{SelectedEntry.FullPath}'");
            entries = [SelectedEntry];
        }
        if (entries.Count == 0)
        {
            Logger.Trace("DeleteSelectedAsync: no valid entries to delete");
            return;
        }

        var confirmed = ShowDeleteConfirmAsync is not null
            ? await ShowDeleteConfirmAsync(entries)
            : false;
        if (!confirmed) return;

        Logger.Info($"DeleteSelectedAsync: deleting {entries.Count} item(s)");

        _deleteCts = new CancellationTokenSource();
        var ct = _deleteCts.Token;

        try
        {
            IsDeleting = true;
            DeleteProgress = 0;
            DeleteStatusText = string.Empty;

            for (int i = 0; i < entries.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var entry = entries[i];
                Logger.Trace($"DeleteSelectedAsync: [{i + 1}/{entries.Count}] '{entry.FullPath}' (IsDir={entry.IsDirectory})");
                DeleteStatusText = $"Deleting {entry.Name}... ({i + 1}/{entries.Count})";
                DeleteProgress = (double)i / entries.Count;

                try
                {
                    if (entry.IsDirectory)
                        await _sftpService.DeleteDirectoryAsync(entry.FullPath);
                    else
                        await _sftpService.DeleteFileAsync(entry.FullPath);

                    RemoveFromTreeAndList(entry);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Delete failed: {entry.Name}");
                    StatusSeverity = ToolbarStatusSeverity.Error;
                    StatusMessage = $"Delete failed: {entry.Name}: {ex.Message}";
                }
            }

            DeleteProgress = 1;
            StatusSeverity = ToolbarStatusSeverity.Success;
            StatusMessage = $"{entries.Count} item(s) deleted";
        }
        catch (OperationCanceledException)
        {
            Logger.Warn("DeleteSelectedAsync cancelled");
            StatusSeverity = ToolbarStatusSeverity.None;
            StatusMessage = "Delete cancelled";
        }
        finally
        {
            IsDeleting = false;
            DeleteProgress = 0;
            DeleteStatusText = string.Empty;
            _deleteCts?.Dispose();
            _deleteCts = null;
        }
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        var parentPath = CurrentPath;
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

            Logger.Trace($"CreateFolderAsync: folder '{dir}' created");
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
                UpdateChildrenPathsRecursive(entry, oldPath, newPath);

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
        if (_authService.IsConnected)
        {
            var creds = _authService.GetSshCredentials();
            var pw = _authService.SmbPassword;
            if (string.IsNullOrEmpty(pw))
                pw = await _authService.FetchSmbPasswordAsync();
            pw ??= creds.Password;
            if (ShowConnectionInfoAsync is not null)
                await ShowConnectionInfoAsync(creds.Host, creds.Username, pw, creds.Port);
        }
    }

    [RelayCommand]
    private Task OpenWinScpAsync()
    {
        if (!_authService.IsConnected || !IsWindows) return Task.CompletedTask;
        var creds = _authService.GetSshCredentials();
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
        catch (Exception ex)
        {
            // `where` probe can fail if WinSCP not on PATH or the shell is unavailable — fall back to null
            Logger.Trace($"FindWinScp: where probe failed — {ex.Message}");
        }

        return null;
    }
}

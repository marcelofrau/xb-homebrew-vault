using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class FileExplorerViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;
    private readonly SftpService _sftpService;
    private string? _sftpPassword;

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

    public Func<SftpEntry, Task<bool>>? ShowDeleteConfirmAsync { get; set; }
    public Func<SftpEntry, Task<string?>>? ShowRenamePromptAsync { get; set; }
    public Func<Task<string?>>? ShowCreateFolderPromptAsync { get; set; }
    public Func<SftpEntry, Task<string?>>? ShowSaveFileDialogAsync { get; set; }
    public Action<string, string>? ShowToast { get; set; }
    public Func<string, string, string, int, Task>? ShowConnectionInfoAsync { get; set; }
    public Func<Task<bool>>? ShowConnectAction { get; set; }
    public Func<Task<string?>>? ShowFolderPickerAsync { get; set; }
    public Action<string, string, string>? ShowErrorDialog { get; set; }

    private void OnBoxConnectionChanged(bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = connected;
            UpdateStatusText();
        });
    }

    private void OnSftpConnectionChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!connected)
            {
                TreeRoots.Clear();
                CurrentEntries.Clear();
                ErrorMessage = null;
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

    [ObservableProperty]
    private string _currentPath = @"D:\";

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

    partial void OnIsUploadingChanged(bool value) => OnPropertyChanged(nameof(ShowActivity));
    partial void OnIsDownloadingChanged(bool value) { OnPropertyChanged(nameof(ShowActivity)); OnPropertyChanged(nameof(ShowIdle)); }
    partial void OnUploadProgressChanged(double value) => OnPropertyChanged(nameof(ActivityProgress));
    partial void OnDownloadProgressChanged(double value) => OnPropertyChanged(nameof(ActivityProgress));
    partial void OnUploadStatusTextChanged(string value) => OnPropertyChanged(nameof(ActivityText));
    partial void OnDownloadStatusTextChanged(string value) => OnPropertyChanged(nameof(ActivityText));

    [ObservableProperty]
    private string? _errorMessage;

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
        UpdateStatusText();
        if (!value)
        {
            TreeRoots.Clear();
            CurrentEntries.Clear();
            ErrorMessage = null;
        }
        NotifyStateDependentProperties();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        NotifyStateDependentProperties();
    }

    partial void OnSelectedEntryChanged(SftpEntry? value)
    {
        if (value?.IsDirectory == true && !value.IsDrive)
        {
            NavigateToPathCommand.Execute(value.FullPath);
        }
    }

    partial void OnCurrentPathChanged(string value)
    {
        OnPropertyChanged(nameof(BreadcrumbSegments));
    }

    public bool ShowDisconnectedContent => !IsConnected;
    public bool ShowReadyContent => IsConnected && !IsLoading && _sftpService.IsConnected;
    public bool ShowPromptContent => IsConnected && !IsLoading && !_sftpService.IsConnected;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool CanBrowse => IsConnected && !IsLoading;
    public bool CanCreateFolder => IsConnected && !IsLoading && _sftpService.IsConnected;
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
        OnPropertyChanged(nameof(CanCreateFolder));
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
        var drives = await DetectDrivesAsync();
        Dispatcher.UIThread.Post(() =>
        {
            TreeRoots.Clear();
            foreach (var d in drives)
                TreeRoots.Add(d);
        });
    }

    private async Task<List<SftpEntry>> DetectDrivesAsync()
    {
        try
        {
            var result = await _sftpService.RunShellCommandAsync("fsutil fsinfo drives");
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                var match = System.Text.RegularExpressions.Regex.Match(result.Output, @"Drives:\s*(.+?)(?:\r|$)");
                if (match.Success)
                {
                    var letters = match.Groups[1].Value
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(d => d.TrimEnd('\\').TrimEnd(':'))
                        .Where(l => l.Length == 1);

                    return letters.Select(l =>
                    {
                        var e = new SftpEntry
                        {
                            Name = $"{l}:\\",
                            FullPath = $"{l}:\\",
                            IsDirectory = true,
                            IsDrive = true,
                            LastModified = DateTime.MinValue
                        };
                        e.Children.Add(new SftpEntry { Name = "" });
                        return e;
                    }).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Drive detection via fsutil failed: {ex.Message}. Falling back to common drives.");
        }

        var fallback = new[] { "C", "D", "E", "Q" };
        return fallback.Select(l =>
        {
            var e = new SftpEntry
            {
                Name = $"{l}:\\",
                FullPath = $"{l}:\\",
                IsDirectory = true,
                IsJunction = false,
                LastModified = DateTime.MinValue
            };
            e.Children.Add(new SftpEntry { Name = "" });
            return e;
        }).ToList();
    }

    [RelayCommand]
    private async Task ExpandFolderAsync(string path)
    {
        try
        {
            var target = FindEntry(TreeRoots, path);
            if (target is null || target.HasLoaded) return;

            var children = await _sftpService.ListDirectoryAsync(path);
            target.HasLoaded = true;
            target.Children.Clear();

            foreach (var c in children)
                target.Children.Add(c);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to expand folder: {path}");
            ErrorMessage = $"Failed to load folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task NavigateToPathAsync(string path)
    {
        try
        {
            IsLoading = true;
            CurrentPath = path;
            var entries = await _sftpService.ListDirectoryAsync(path);
            Dispatcher.UIThread.Post(() =>
            {
                CurrentEntries.Clear();
                foreach (var e in entries)
                    CurrentEntries.Add(e);
                OnPropertyChanged(nameof(CanRefresh));
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to navigate to: {path}");
            ErrorMessage = $"Failed to load: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!string.IsNullOrEmpty(CurrentPath))
            await NavigateToPathAsync(CurrentPath);
    }

    [RelayCommand]
    private async Task UploadFilesAsync(string[]? filePaths)
    {
        if (filePaths is null || filePaths.Length == 0) return;

        foreach (var filePath in filePaths)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                IsUploading = true;
                UploadStatusText = $"Uploading {fileName}... (0%)";
                UploadProgress = 0;

                var remotePath = CurrentPath.TrimEnd('\\') + "\\" + fileName;

                await using var stream = File.OpenRead(filePath);
                var progress = new Progress<double>(p =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        UploadProgress = p;
                        UploadStatusText = $"Uploading {fileName}... ({p * 100:F0}%)";
                    });
                });

                await _sftpService.UploadFileAsync(stream, remotePath, progress);
                ShowToast?.Invoke("Upload Complete", $"{fileName} uploaded successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Upload failed: {filePath}");
                ShowToast?.Invoke("Upload Failed", $"{Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        IsUploading = false;
        UploadStatusText = string.Empty;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DownloadFileAsync(SftpEntry? entry)
    {
        if (entry is null) return;

        if (entry.IsDirectory)
            await DownloadFolderAsync(entry);
        else
            await DownloadSingleFileAsync(entry);
    }

    private async Task DownloadSingleFileAsync(SftpEntry entry)
    {
        try
        {
            var savePath = ShowSaveFileDialogAsync is not null ? await ShowSaveFileDialogAsync(entry) : null;
            if (string.IsNullOrEmpty(savePath)) return;

            IsDownloading = true;
            DownloadProgress = 0;
            DownloadStatusText = $"Downloading {entry.Name}... (0%)";

            await using var stream = File.Create(savePath);
            var progress = new Progress<double>(p =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    DownloadProgress = p;
                    DownloadStatusText = $"Downloading {entry.Name}... ({p * 100:F0}%)";
                });
            });

            await _sftpService.DownloadFileAsync(entry.FullPath, stream, progress);
            ShowToast?.Invoke("Download Complete", $"{entry.Name} saved");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Download failed: {entry.Name}");
            ShowToast?.Invoke("Download Failed", ex.Message);
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
                ShowToast?.Invoke("Download Complete", "Empty folder");
                return;
            }

            Directory.CreateDirectory(localRoot);
            var rootPath = entry.FullPath.TrimEnd('\\');

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var relative = file.FullPath.Substring(rootPath.Length).TrimStart('\\');
                var localPath = Path.Combine(localRoot, relative);
                var localDir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(localDir))
                    Directory.CreateDirectory(localDir);

                var idx = i;
                Dispatcher.UIThread.Post(() =>
                {
                    DownloadStatusText = $"Downloading {file.Name}... ({idx + 1}/{totalFiles})";
                });

                await using var stream = File.Create(localPath);
                await _sftpService.DownloadFileAsync(file.FullPath, stream, null);

                Dispatcher.UIThread.Post(() =>
                {
                    DownloadProgress = (double)(idx + 1) / totalFiles;
                });
            }

            ShowToast?.Invoke("Download Complete", $"{totalFiles} files saved");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Folder download failed: {entry.Name}");
            ShowToast?.Invoke("Download Failed", ex.Message);
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
            DownloadStatusText = string.Empty;
        }
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(SftpEntry? entry)
    {
        if (entry is null) return;

        var confirmTask = ShowDeleteConfirmAsync?.Invoke(entry);
        var confirmed = confirmTask is not null ? await confirmTask : false;
        if (!confirmed) return;

        try
        {
            if (entry.IsDirectory)
                await _sftpService.DeleteDirectoryAsync(entry.FullPath);
            else
                await _sftpService.DeleteFileAsync(entry.FullPath);

            ShowToast?.Invoke("Deleted", $"{entry.Name} deleted");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Delete failed: {entry.Name}");
            ShowToast?.Invoke("Delete Failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RenameEntryAsync(SftpEntry? entry)
    {
        if (entry is null) return;

        var renameTask = ShowRenamePromptAsync?.Invoke(entry);
        var newName = renameTask is not null ? await renameTask : null;
        if (string.IsNullOrEmpty(newName) || newName == entry.Name) return;

        try
        {
            var parentDir = Path.GetDirectoryName(entry.FullPath)?.Replace('/', '\\') ?? "";
            var newPath = parentDir.TrimEnd('\\') + "\\" + newName;
            await _sftpService.RenameAsync(entry.FullPath, newPath);
            ShowToast?.Invoke("Renamed", $"{entry.Name} → {newName}");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Rename failed: {entry.Name}");
            ShowToast?.Invoke("Rename Failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        var createTask = ShowCreateFolderPromptAsync?.Invoke();
        var folderName = createTask is not null ? await createTask : null;
        if (string.IsNullOrEmpty(folderName)) return;

        try
        {
            var newPath = CurrentPath.TrimEnd('\\') + "\\" + folderName;
            await _sftpService.CreateDirectoryAsync(newPath);
            ShowToast?.Invoke("Folder Created", folderName);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Create folder failed: {folderName}");
            ShowToast?.Invoke("Create Failed", ex.Message);
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
        foreach (var e in entries)
        {
            if (e.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                return e;
            if (e.Children.Count > 0)
            {
                var found = FindEntry(e.Children, path);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }
}

#nullable enable
using System.Collections.Specialized;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using XBVault.Models;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileFileExplorerView : UserControl
{
    public MobileFileExplorerView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var listBox = this.FindControl<ListBox>("FileListBox");
        if (listBox is not null)
            listBox.Tapped += OnListBoxTapped;

        if (DataContext is FileExplorerViewModel vm)
        {
            vm.RefreshConnectionState();
            vm.PropertyChanged += OnVmPropertyChanged;
            vm.TreeRoots.CollectionChanged += OnTreeRootsChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        var listBox = this.FindControl<ListBox>("FileListBox");
        if (listBox is not null)
            listBox.Tapped -= OnListBoxTapped;

        if (DataContext is FileExplorerViewModel vm)
        {
            vm.PropertyChanged -= OnVmPropertyChanged;
            vm.TreeRoots.CollectionChanged -= OnTreeRootsChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm) return;
        if (e.PropertyName == nameof(FileExplorerViewModel.IsLoading)
            && !vm.IsLoading
            && vm.CurrentEntries.Count == 0
            && vm.TreeRoots.Count > 0)
        {
            CopyTreeRootsToCurrentEntries(vm);
        }
    }

    private void OnTreeRootsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm) return;
        if (vm.CurrentEntries.Count == 0 && vm.TreeRoots.Count > 0 && !vm.IsLoading)
        {
            CopyTreeRootsToCurrentEntries(vm);
        }
    }

    private static void CopyTreeRootsToCurrentEntries(FileExplorerViewModel vm)
    {
        vm.CurrentEntries.Clear();
        foreach (var root in vm.TreeRoots)
            vm.CurrentEntries.Add(root);
    }

    private string? _selectedName;

    private void OnListBoxTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm) return;
        if (e.Source is not StyledElement se) return;
        var item = se.DataContext as SftpEntry;
        if (item is null) return;

        if (item.Name == "..")
        {
            _selectedName = null;
            _ = vm.ExpandTreeToPathAsync(item.FullPath);
            vm.NavigateToPathCommand.Execute(item.FullPath);
            e.Handled = true;
            return;
        }

        if (item.Name == _selectedName)
        {
            _selectedName = null;
            if (item.IsDirectory)
            {
                _ = vm.ExpandTreeToPathAsync(item.FullPath);
                vm.NavigateToPathCommand.Execute(item.FullPath);
            }
            e.Handled = true;
        }
        else
        {
            _selectedName = item.Name;
            vm.SelectedEntry = item;
        }
    }

    private void OnBrowseDrivesClick(object? sender, RoutedEventArgs e)
    {
        async Task Handler()
        {
            if (DataContext is not FileExplorerViewModel vm) return;
            if (!vm.IsConnected)
            {
                await vm.InitializeCommand.ExecuteAsync(null);
                return;
            }
            vm.RefreshCommand.Execute(null);
        }

        Handler().FireAndForget("MobileFileExplorerView.OnBrowseDrivesClick");
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FileExplorerViewModel vm)
            vm.RefreshCommand.Execute(null);
    }

    private void OnNewFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FileExplorerViewModel vm)
            vm.CreateFolderCommand.Execute(null);
    }

    private void OnRenameClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FileExplorerViewModel vm)
            vm.RenameEntryCommand.Execute(null);
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FileExplorerViewModel vm)
            vm.DeleteSelectedCommand.Execute(null);
    }

    private void OnUploadFilesClick(object? sender, RoutedEventArgs e)
    {
        async Task Handler()
        {
            if (DataContext is not FileExplorerViewModel vm) return;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select files to upload",
                AllowMultiple = true
            });
            if (files.Count == 0) return;

            var paths = new List<string>();
            string? tempDir = null;
            try
            {
                foreach (var f in files)
                {
                    var localPath = f.TryGetLocalPath();
                    if (!string.IsNullOrEmpty(localPath))
                    {
                        paths.Add(localPath);
                    }
                    else
                    {
                        tempDir ??= Path.Combine(Path.GetTempPath(), $"xbv_upload_{Guid.NewGuid():N}");
                        Directory.CreateDirectory(tempDir);
                        var tempPath = Path.Combine(tempDir, f.Name);
                        await using var srcStream = await f.OpenReadAsync();
                        await using var dstStream = File.Create(tempPath);
                        await srcStream.CopyToAsync(dstStream);
                        paths.Add(tempPath);
                    }
                }
                if (paths.Count == 0) return;
                await vm.UploadMixedAsync(paths.ToArray(), []);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Upload files failed");
                vm.StatusSeverity = ToolbarStatusSeverity.Error;
                vm.StatusMessage = $"Upload failed: {ex.Message}";
            }
            finally
            {
                if (tempDir is not null)
                    try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort cleanup */ }
            }
        }

        Handler().FireAndForget("MobileFileExplorerView.OnUploadFilesClick");
    }

    private void OnUploadFolderClick(object? sender, RoutedEventArgs e)
    {
        async Task Handler()
        {
            if (DataContext is not FileExplorerViewModel vm) return;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select folders to upload",
                AllowMultiple = true
            });
            if (folders.Count == 0) return;

            var paths = new List<string>();
            string? tempDir = null;
            try
            {
                foreach (var f in folders)
                {
                    var localPath = f.TryGetLocalPath();
                    if (!string.IsNullOrEmpty(localPath))
                    {
                        paths.Add(localPath);
                    }
                    else
                    {
                        tempDir ??= Path.Combine(Path.GetTempPath(), $"xbv_upload_{Guid.NewGuid():N}");
                        var folderPath = Path.Combine(tempDir, f.Name);
                        await CopyFolderRecursiveAsync(f, folderPath);
                        paths.Add(folderPath);
                    }
                }
                if (paths.Count == 0) return;
                await vm.UploadMixedAsync([], paths.ToArray());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Upload folder failed");
                vm.StatusSeverity = ToolbarStatusSeverity.Error;
                vm.StatusMessage = $"Upload failed: {ex.Message}";
            }
            finally
            {
                if (tempDir is not null)
                    try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort cleanup */ }
            }
        }

        Handler().FireAndForget("MobileFileExplorerView.OnUploadFolderClick");
    }

    private static async Task CopyFolderRecursiveAsync(IStorageFolder source, string destDir)
    {
        Directory.CreateDirectory(destDir);
        await foreach (var item in source.GetItemsAsync())
        {
            if (item is IStorageFile file)
            {
                var localPath = file.TryGetLocalPath();
                if (!string.IsNullOrEmpty(localPath))
                    File.Copy(localPath, Path.Combine(destDir, file.Name), overwrite: true);
                else
                {
                    await using var src = await file.OpenReadAsync();
                    await using var dst = File.Create(Path.Combine(destDir, file.Name));
                    await src.CopyToAsync(dst);
                }
            }
            else if (item is IStorageFolder subFolder)
            {
                await CopyFolderRecursiveAsync(subFolder, Path.Combine(destDir, subFolder.Name));
            }
        }
    }

    private void OnUploadZipClick(object? sender, RoutedEventArgs e)
    {
        async Task Handler()
        {
            if (DataContext is not FileExplorerViewModel vm) return;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;
            var zipTypes = new FilePickerFileType[] { new("ZIP Archive") { Patterns = ["*.zip"] } };
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select ZIP file to extract and upload",
                AllowMultiple = false,
                FileTypeFilter = zipTypes
            });
            if (files.Count == 0) return;
            var zipFile = files[0];
            var zipPath = zipFile.TryGetLocalPath();
            if (string.IsNullOrEmpty(zipPath))
            {
                var tempDir = Path.Combine(Path.GetTempPath(), $"xbv_upload_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);
                zipPath = Path.Combine(tempDir, zipFile.Name);
                await using var src = await zipFile.OpenReadAsync();
                await using var dst = File.Create(zipPath);
                await src.CopyToAsync(dst);
                try { await vm.UploadZipExtractAsync(zipPath); }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Upload ZIP failed");
                    vm.StatusSeverity = ToolbarStatusSeverity.Error;
                    vm.StatusMessage = $"Upload failed: {ex.Message}";
                }
                finally
                {
                    try { File.Delete(zipPath); } catch { /* best-effort cleanup */ }
                    try { Directory.Delete(tempDir); } catch { /* best-effort cleanup */ }
                }
            }
            else
            {
                try { await vm.UploadZipExtractAsync(zipPath); }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Upload ZIP failed");
                    vm.StatusSeverity = ToolbarStatusSeverity.Error;
                    vm.StatusMessage = $"Upload failed: {ex.Message}";
                }
            }
        }

        Handler().FireAndForget("MobileFileExplorerView.OnUploadZipClick");
    }

    private void OnDownloadConfirmClick(object? sender, RoutedEventArgs e)
    {
        async Task Handler()
        {
            if (DataContext is not FileExplorerViewModel vm) return;

            var entries = vm.SelectedEntries.Where(x => !x.IsDrive && !x.IsPlaceholder).ToList();
            if (entries.Count == 0 && vm.SelectedEntry is not null && !vm.SelectedEntry.IsPlaceholder && !vm.SelectedEntry.IsDrive)
                entries = [vm.SelectedEntry];

            if (entries.Count == 0) return;

            string fileName;
            if (entries.Count == 1)
                fileName = entries[0].Name ?? "file";
            else
                fileName = $"{entries.Count} files";

            var message = entries.Count == 1
                ? $"Download \"{fileName}\" to your device?"
                : $"Download {entries.Count} files to your device?";

            if (vm.ShowConfirmAction is not null)
            {
                var confirmed = await vm.ShowConfirmAction("Download", message, "Download", "Cancel");
                if (!confirmed) return;
            }

            await vm.DownloadSelectedCommand.ExecuteAsync(null);
        }

        Handler().FireAndForget("MobileFileExplorerView.OnDownloadConfirmClick");
    }
}

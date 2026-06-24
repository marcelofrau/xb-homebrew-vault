using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XBVault.Models;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class FileExplorerView : UserControl
{
    private FileExplorerViewModel? _vm;

    public FileExplorerView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm is not null)
        {
            _vm.ShowDeleteConfirmAsync = null;
            _vm.ShowRenamePromptAsync = null;
            _vm.ShowSaveFileDialogAsync = null;
            _vm.ShowToast = null;
            _vm.ShowConnectionInfoAsync = null;
            _vm.ShowFolderPickerAsync = null;
            _vm.ShowErrorDialog = null;
            _vm.ShowWinScpNotFoundDialog = null;
        }

        _vm = DataContext as FileExplorerViewModel;

        if (_vm is null) return;

        _vm.ShowDeleteConfirmAsync = ShowDeleteConfirmAsync;
        _vm.ShowRenamePromptAsync = ShowRenamePromptAsync;
        _vm.ShowSaveFileDialogAsync = ShowSaveFileDialogAsync;
        _vm.ShowToast = ShowToast;
        _vm.ShowConnectionInfoAsync = ShowConnectionInfoAsync;
        _vm.ShowFolderPickerAsync = ShowFolderPickerAsync;
        _vm.ShowErrorDialog = ShowErrorDialog;
        _vm.ShowWinScpNotFoundDialog = ShowWinScpNotFoundDialog;

        BrowseFilesBtn.Click += OnBrowseFilesClick;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var treeView = this.FindControl<TreeView>("FolderTree");
        if (treeView is not null)
        {
            treeView.AddHandler(TreeViewItem.ExpandedEvent, OnTreeItemExpanded);
            treeView.SelectionChanged += OnTreeSelectionChanged;
        }
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach (var added in e.AddedItems)
            if (added is SftpEntry entry)
                entry.IsSelected = true;

        foreach (var removed in e.RemovedItems)
            if (removed is SftpEntry entry)
                entry.IsSelected = false;
    }

    private async void OnTreeItemExpanded(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (e.Source is TreeViewItem tvi && tvi.DataContext is SftpEntry entry)
            await _vm.ExpandFolderCommand.ExecuteAsync(entry.FullPath);
    }

    private async void OnBrowseFilesClick(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select files to upload",
            AllowMultiple = true
        });

        var paths = files.Select(f => f.TryGetLocalPath()).Where(p => p is not null).Cast<string>().ToArray();
        if (paths.Length > 0)
            await _vm.UploadFilesCommand.ExecuteAsync(paths);
    }

    private async Task<bool> ShowDeleteConfirmAsync(SftpEntry entry)
    {
        var vm = new ConfirmViewModel(
            "Delete",
            $"Are you sure you want to delete {entry.Name}?",
            "Delete", "Cancel",
            "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-delete-24.png",
            "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-delete-24.png");

        var win = new ConfirmWindow { DataContext = vm };
        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel is not null)
            await win.ShowDialog(topLevel);

        return vm.Confirmed;
    }

    private async Task<string?> ShowRenamePromptAsync(SftpEntry entry)
    {
        var vm = new ConfirmViewModel(
            "Rename",
            $"Enter new name for {entry.Name}:",
            "Rename", "Cancel",
            "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-rename-24.png",
            "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-rename-24.png");

        var win = new ConfirmWindow { DataContext = vm };
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null)
            await win.ShowDialog(owner);

        return vm.Confirmed ? entry.Name : null;
    }

    private async Task<string?> ShowSaveFileDialogAsync(SftpEntry entry)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = entry.Name
        });

        return file?.TryGetLocalPath();
    }

    private async Task<string?> ShowFolderPickerAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select destination folder",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private async Task ShowConnectionInfoAsync(string host, string user, string password, int port)
    {
        var win = new SftpInfoWindow();
        win.SetConnectionInfo(host, user, password, port);
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null)
            await win.ShowDialog(owner);
    }

    private void ShowErrorDialog(string title, string description, string details)
    {
        var win = new ErrorDialog(title, description, details, ErrorDialogType.Error);
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null)
            win.ShowDialog(owner);
    }

    private void ShowWinScpNotFoundDialog(string title, string description, string url)
    {
        var vm = new ConfirmViewModel(title, description, "Download", "Close",
            "avares://XBVault/Assets/Views/ConnectionWindow/connection-connect-20.png",
            "avares://XBVault/Assets/Views/FileExplorerView/fileexplorer-winscp-24.png");
        var win = new ConfirmWindow { DataContext = vm };
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        vm.Completed += (confirmed) =>
        {
            if (confirmed)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                    { UseShellExecute = true });
                }
                catch { }
            }
            win.Close();
        };

        win.ShowDialog(owner);
    }

    private static void ShowToast(string title, string message)
    {
        Logger.Info($"[{title}] {message}");
    }
}

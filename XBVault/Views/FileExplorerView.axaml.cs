using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XBVault.Models;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class FileExplorerView : UserControl
{
    private FileExplorerViewModel? _vm;
    private DispatcherTimer? _cdTimer;
    private double _cdAngle;
    private DispatcherTimer? _loadingTimer;
    private double _loadingAngle;
    private bool _isKeyboardNav;
    private bool _suppressListBoxFocus;

    public FileExplorerView()
    {
        Logger.Trace("FileExplorerView ctor: start");
        InitializeComponent();
        Logger.Trace("FileExplorerView ctor: after InitializeComponent");
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        Logger.Debug("FileExplorerView.OnDataContextChanged: start");
        if (_vm is not null)
        {
            Logger.Trace("OnDataContextChanged: cleaning old VM");
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.ShowDeleteConfirmAsync = null;
            _vm.ShowSaveFileDialogAsync = null;
            _vm.ShowConnectionInfoAsync = null;
            _vm.ShowFolderPickerAsync = null;
            _vm.ShowErrorDialog = null;
            _vm.ShowWinScpNotFoundDialog = null;
            _vm.ScrollToEntry = null;
            _vm.FocusFileList = null;
            _vm.ShowInputDialogAsync = null;
        }

        _vm = DataContext as FileExplorerViewModel;
        Logger.Trace($"OnDataContextChanged: vm={( _vm is not null ? "set" : "null" )}");

        if (_vm is null) return;

        Logger.Trace("OnDataContextChanged: assigning delegates");
        _vm.ShowDeleteConfirmAsync = ShowDeleteConfirmAsync;
        Logger.Trace("OnDataContextChanged: ShowDeleteConfirmAsync assigned");
        _vm.ShowSaveFileDialogAsync = ShowSaveFileDialogAsync;
        _vm.ShowConnectionInfoAsync = ShowConnectionInfoAsync;
        _vm.ShowFolderPickerAsync = ShowFolderPickerAsync;
        _vm.ShowErrorDialog = ShowErrorDialog;
        _vm.ShowWinScpNotFoundDialog = ShowWinScpNotFoundDialog;
        _vm.ScrollToEntry = ScrollToEntry;
        _vm.FocusFileList = () =>
        {
            if (_suppressListBoxFocus)
            {
                _suppressListBoxFocus = false;
                return;
            }
            if (FileListBox is null) return;
            if (FileListBox.ItemCount > 0)
                FileListBox.SelectedIndex = 0;
            FileListBox.Focus();
        };
        _vm.ShowInputDialogAsync = ShowInputDialogAsync;
        Logger.Trace("OnDataContextChanged: all delegates assigned");

        Logger.Trace("OnDataContextChanged: attaching PropertyChanged");
        _vm.PropertyChanged += OnVmPropertyChanged;
        Logger.Trace("OnDataContextChanged: PropertyChanged attached");

        Logger.Trace($"OnDataContextChanged: BrowseFilesBtn is null? {BrowseFilesBtn is null}");
        if (BrowseFilesBtn is not null)
            BrowseFilesBtn.Click += OnBrowseFilesClick;
        else
            Logger.Error("OnDataContextChanged: BrowseFilesBtn is NULL!");

        Logger.Trace($"OnDataContextChanged: UploadButton is null? {UploadButton is null}");
        if (UploadButton is not null)
            UploadButton.Click += OnBrowseFilesClick;
        else
            Logger.Error("OnDataContextChanged: UploadButton is NULL!");

        Logger.Trace("OnDataContextChanged: buttons wired");

        Logger.Debug("FileExplorerView.OnDataContextChanged: done");
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileExplorerViewModel.ShowActivity))
        {
            if (_vm?.ShowActivity == true)
                StartCdSpinner();
            else
                StopCdSpinner();
        }
        else if (e.PropertyName == nameof(FileExplorerViewModel.IsLoading))
        {
            if (_vm?.IsLoading == true)
                StartLoadingSpinner();
            else
                StopLoadingSpinner();
        }
    }

    private void StartCdSpinner()
    {
        if (_cdTimer is not null) return;
        _cdAngle = 0;
        _cdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _cdTimer.Tick += OnCdTimerTick;
        _cdTimer.Start();
    }

    private void StopCdSpinner()
    {
        if (_cdTimer is null) return;
        _cdTimer.Stop();
        _cdTimer.Tick -= OnCdTimerTick;
        _cdTimer = null;
    }

    private void OnCdTimerTick(object? sender, EventArgs e)
    {
        _cdAngle = (_cdAngle + 6) % 360;
        CdSpinnerImage.RenderTransform = new RotateTransform(_cdAngle);
    }

    private void StartLoadingSpinner()
    {
        if (_loadingTimer is not null) return;
        _loadingAngle = 0;
        _loadingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _loadingTimer.Tick += OnLoadingTick;
        _loadingTimer.Start();
    }

    private void StopLoadingSpinner()
    {
        if (_loadingTimer is null) return;
        _loadingTimer.Stop();
        _loadingTimer.Tick -= OnLoadingTick;
        _loadingTimer = null;
    }

    private void OnLoadingTick(object? sender, EventArgs e)
    {
        _loadingAngle = (_loadingAngle - 6 + 360) % 360;
        LoadingSpinnerImage.RenderTransform = new RotateTransform(_loadingAngle);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Logger.Debug("FileExplorerView.OnAttachedToVisualTree: start");
        Logger.Trace("OnAttachedToVisualTree: looking for FolderTree...");
        var treeView = this.FindControl<TreeView>("FolderTree");
        if (treeView is not null)
        {
            Logger.Trace("OnAttachedToVisualTree: attaching TreeView handlers");
            treeView.AddHandler(TreeViewItem.ExpandedEvent, OnTreeItemExpanded);
            treeView.SelectionChanged += OnTreeSelectionChanged;
            treeView.AddHandler(InputElement.PointerPressedEvent, OnTreeViewPreviewPointerPressed, RoutingStrategies.Tunnel);
            treeView.AddHandler(InputElement.PointerPressedEvent, OnTreeViewDoubleClick, RoutingStrategies.Bubble);
            treeView.AddHandler(InputElement.KeyDownEvent, OnTreeViewPreviewKeyDown, RoutingStrategies.Tunnel);
            treeView.AddHandler(InputElement.KeyDownEvent, OnTreeViewKeyDown, RoutingStrategies.Bubble);
            Logger.Trace("OnAttachedToVisualTree: TreeView handlers attached");
        }
        else
        {
            Logger.Warn("OnAttachedToVisualTree: FolderTree not found");
        }

        Logger.Trace("OnAttachedToVisualTree: looking for FileListBox...");
        var listBox = this.FindControl<ListBox>("FileListBox");
        if (listBox is not null)
        {
            Logger.Trace("OnAttachedToVisualTree: attaching ListBox handlers");
            listBox.SelectionChanged += OnListBoxSelectionChanged;
            listBox.AddHandler(InputElement.PointerPressedEvent, OnListBoxPreviewPointerPressed, RoutingStrategies.Tunnel);
            listBox.AddHandler(InputElement.PointerPressedEvent, OnListBoxPointerPressed, RoutingStrategies.Bubble);
            listBox.AddHandler(InputElement.KeyDownEvent, OnListBoxKeyDown, RoutingStrategies.Bubble, true);
            Logger.Trace("OnAttachedToVisualTree: ListBox handlers attached");
        }
        else
        {
            Logger.Warn("OnAttachedToVisualTree: FileListBox not found");
        }

        Logger.Trace("OnAttachedToVisualTree: setting up drag-drop");
        DragDrop.AddDragOverHandler(DropZoneBorder, OnDropZoneDragOver);
        DragDrop.AddDragLeaveHandler(DropZoneBorder, OnDropZoneDragLeave);
        DragDrop.AddDropHandler(DropZoneBorder, OnDropZoneDrop);
        Logger.Trace("OnAttachedToVisualTree: drag-drop handlers attached");

        Logger.Debug("FileExplorerView.OnAttachedToVisualTree: done");
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Logger.Debug("FileExplorerView.OnDetachedFromVisualTree: detaching TreeView event handlers");
        StopCdSpinner();
        StopLoadingSpinner();
        var treeView = this.FindControl<TreeView>("FolderTree");
        if (treeView is not null)
        {
            treeView.RemoveHandler(TreeViewItem.ExpandedEvent, OnTreeItemExpanded);
            treeView.SelectionChanged -= OnTreeSelectionChanged;
            treeView.RemoveHandler(InputElement.PointerPressedEvent, OnTreeViewPreviewPointerPressed);
            treeView.RemoveHandler(InputElement.PointerPressedEvent, OnTreeViewDoubleClick);
            treeView.RemoveHandler(InputElement.KeyDownEvent, OnTreeViewPreviewKeyDown);
            treeView.RemoveHandler(InputElement.KeyDownEvent, OnTreeViewKeyDown);
        }

        var listBox = this.FindControl<ListBox>("FileListBox");
        if (listBox is not null)
        {
            listBox.SelectionChanged -= OnListBoxSelectionChanged;
            listBox.RemoveHandler(InputElement.PointerPressedEvent, OnListBoxPreviewPointerPressed);
            listBox.RemoveHandler(InputElement.PointerPressedEvent, OnListBoxPointerPressed);
            listBox.RemoveHandler(InputElement.KeyDownEvent, OnListBoxKeyDown);
        }

        DragDrop.RemoveDragOverHandler(DropZoneBorder, OnDropZoneDragOver);
        DragDrop.RemoveDragLeaveHandler(DropZoneBorder, OnDropZoneDragLeave);
        DragDrop.RemoveDropHandler(DropZoneBorder, OnDropZoneDrop);
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var addedNames = string.Join(", ", e.AddedItems.OfType<SftpEntry>().Select(x => x.Name));
        var removedNames = string.Join(", ", e.RemovedItems.OfType<SftpEntry>().Select(x => x.Name));
        Logger.Trace($"OnTreeSelectionChanged: isKeyboardNav={_isKeyboardNav} added=[{addedNames}] removed=[{removedNames}]");

        foreach (var added in e.AddedItems)
            if (added is SftpEntry entry)
            {
                entry.IsSelected = true;
                if (_vm is not null)
                    _vm.SelectedEntry = entry;
            }

        foreach (var removed in e.RemovedItems)
            if (removed is SftpEntry entry)
                entry.IsSelected = false;

        if (_vm?.SelectedEntry is { } entry2 && entry2.IsDirectory)
        {
            Logger.Debug($"OnTreeSelectionChanged: navigating to '{entry2.FullPath}' isKeyboardNav={_isKeyboardNav}");
            if (_isKeyboardNav)
                _suppressListBoxFocus = true;
            _vm.NavigateToPathCommand.Execute(entry2.FullPath);
        }
    }

    private async void OnTreeItemExpanded(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (e.Source is TreeViewItem tvi && tvi.DataContext is SftpEntry entry)
        {
            Logger.Debug($"OnTreeItemExpanded: '{entry.FullPath}'");
            entry.IsExpanded = true;
            await _vm.ExpandFolderCommand.ExecuteAsync(entry.FullPath);
            _vm.NavigateToPathCommand.Execute(entry.FullPath);
        }
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

    private async Task<bool> ShowDeleteConfirmAsync(IReadOnlyList<SftpEntry> entries)
    {
        var msg = entries.Count == 1
            ? $"Are you sure you want to delete {entries[0].Name}?"
            : $"Are you sure you want to delete {entries.Count} items?";

        var vm = new ConfirmViewModel(
            "Delete", msg,
            "Delete", "Cancel",
            "avares://XBVault/Assets/Views/InstalledView/installed-uninstall-20.png",
            "avares://XBVault/Assets/Views/ErrorDialog/errordialog-trash-48.png",
            isDestructive: true);

        var win = new ConfirmWindow { DataContext = vm };
        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel is not null)
            await win.ShowDialog(topLevel);

        return vm.Confirmed;
    }

    private void OnTreeViewPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Logger.Trace("OnTreeViewPreviewPointerPressed: _isKeyboardNav = false");
        _isKeyboardNav = false;
    }

    private void OnTreeViewPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            Logger.Trace($"OnTreeViewPreviewKeyDown: {e.Key} -> _isKeyboardNav = true");
            _isKeyboardNav = true;
        }
    }

    private void OnTreeViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null) return;
        if (e.Key != Key.Enter) return;

        Logger.Debug($"OnTreeViewKeyDown: Enter on {_vm.SelectedEntry?.FullPath ?? "null"}");
        if (_vm.SelectedEntry is SftpEntry entry && entry.IsDirectory)
        {
            Logger.Debug($"OnTreeViewKeyDown: expanding + navigating to {entry.FullPath}");
            entry.IsExpanded = true;
            _vm.NavigateToPathCommand.Execute(entry.FullPath);
            e.Handled = true;
        }
    }

    private void OnTreeViewDoubleClick(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null) return;
        if (e.ClickCount < 2) return;
        var source = e.Source as Visual;
        var tvi = source as TreeViewItem ?? source?.FindAncestorOfType<TreeViewItem>();
        if (tvi?.DataContext is SftpEntry entry && entry.IsDirectory)
        {
            Logger.Debug($"OnTreeViewDoubleClick: double-click on '{entry.FullPath}'");
            entry.IsExpanded = true;
            _vm.NavigateToPathCommand.Execute(entry.FullPath);
            e.Handled = true;
        }
    }

    private void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        if (FileListBox is null) return;

        _vm.SelectedEntries.Clear();
        foreach (SftpEntry entry in FileListBox.SelectedItems!)
            _vm.SelectedEntries.Add(entry);
        _vm.NotifySelectionChanged();
        Logger.Trace($"OnListBoxSelectionChanged: {_vm.SelectedEntries.Count} selected, first=[{_vm.SelectedEntries.FirstOrDefault()?.Name}]");
    }

    private void OnListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Logger.Trace($"OnListBoxPointerPressed: ClickCount={e.ClickCount} Source={e.Source?.GetType().Name}");
        if (_vm is null) return;
        if (e.ClickCount < 2) return;
        var item = (e.Source as StyledElement)?.DataContext;
        Logger.Trace($"OnListBoxPointerPressed: DataContext type={item?.GetType().Name}");
        if (item is SftpEntry entry && entry.IsDirectory && !entry.IsPlaceholder)
        {
            Logger.Debug($"OnListBoxPointerPressed: double-click navigating to '{entry.FullPath}'");
            _ = _vm.ExpandTreeToPathAsync(entry.FullPath);
            _vm.NavigateToPathCommand.Execute(entry.FullPath);
            e.Handled = true;
        }
        else
        {
            Logger.Trace($"OnListBoxPointerPressed: no match — IsDirectory={item is SftpEntry se && se.IsDirectory}, IsPlaceholder={item is SftpEntry se2 && se2.IsPlaceholder}");
        }
    }

    private void OnListBoxPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Tunnel handler catches double-click before ListBoxItem consumes PointerPressed
        Logger.Trace($"OnListBoxPreviewPointerPressed: ClickCount={e.ClickCount} Source={e.Source?.GetType().Name}");
        if (e.ClickCount < 2) return;
        var item = (e.Source as StyledElement)?.DataContext;
        Logger.Trace($"OnListBoxPreviewPointerPressed: DataContext type={item?.GetType().Name}");
        if (_vm is null) return;

        if (item is SftpEntry entry)
        {
            if (entry.IsPlaceholder && entry.Name == "..")
            {
                Logger.Debug("OnListBoxPreviewPointerPressed: '..' clicked, navigating up");
                _ = _vm.ExpandTreeToPathAsync(entry.FullPath);
                _vm.NavigateToPathCommand.Execute(entry.FullPath);
                e.Handled = true;
            }
            else if (entry.IsDirectory)
            {
                Logger.Debug($"OnListBoxPreviewPointerPressed: double-click on '{entry.FullPath}'");
                _ = _vm.ExpandTreeToPathAsync(entry.FullPath);
                _vm.NavigateToPathCommand.Execute(entry.FullPath);
                e.Handled = true;
            }
        }
    }

    private void OnListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null)
        {
            Logger.Trace("OnListBoxKeyDown: _vm is null");
            return;
        }
        Logger.Trace($"OnListBoxKeyDown: Key={e.Key} SelectedEntries.Count={_vm.SelectedEntries.Count}");
        if (e.Key == Key.Enter)
        {
            var first = _vm.SelectedEntries.FirstOrDefault();
            Logger.Trace($"OnListBoxKeyDown: Enter first name=[{first?.Name}] IsDirectory={first?.IsDirectory} IsPlaceholder={first?.IsPlaceholder}");
            if (first is not null && first.IsDirectory)
            {
                Logger.Debug($"OnListBoxKeyDown: Enter navigating to '{first.FullPath}'");
                _ = _vm.ExpandTreeToPathAsync(first.FullPath);
                _vm.NavigateToPathCommand.Execute(first.FullPath);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Back)
        {
            Logger.Debug("OnListBoxKeyDown: Back → navigate up");
            _vm.NavigateToParentCommand.Execute(null);
            e.Handled = true;
        }
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

    private void ScrollToEntry(SftpEntry entry)
    {
        Logger.Debug($"ScrollToEntry: '{entry.FullPath}'");
        var treeView = this.FindControl<TreeView>("FolderTree");
        treeView?.ScrollIntoView(entry);
    }

    private async Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue, string? iconUri)
    {
        var win = new InputDialog(title, message, defaultValue, iconUri);
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null)
            await win.ShowDialog(owner);
        return win.Value;
    }

    private void OnDropZoneDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Formats.Contains(DataFormat.File) && _vm?.ShowIdle == true)
        {
            e.DragEffects = DragDropEffects.Copy;
            DropZoneBorder.Background = new SolidColorBrush(Color.FromArgb(60, 0, 120, 215));
            DropZoneBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 0, 120, 215));
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDropZoneDragLeave(object? sender, DragEventArgs e)
    {
        DropZoneBorder.Background = new SolidColorBrush(Color.FromArgb(13, 255, 255, 255));
        DropZoneBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255));
    }

    private async void OnDropZoneDrop(object? sender, DragEventArgs e)
    {
        OnDropZoneDragLeave(sender, e);

        if (_vm is null) return;
        var dropped = e.DataTransfer.TryGetFiles();
        if (dropped is null) return;
        var files = dropped.ToList();

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => p is not null)
            .Cast<string>()
            .ToArray();

        if (paths.Length > 0)
            await _vm.UploadFilesCommand.ExecuteAsync(paths);
    }

}

## 1. Icons & Assets

- [x] 1.1 Create `Assets/Views/FileExplorerView/` directory
- [x] 1.2 Copy icons from `F:\workspace\icons8-personal-set`:
  - `icons8-folder-{size}.png` → `fileexplorer-folder-16.png` (tree folder closed, 16px)
  - `icons8-opened-folder-{size}.png` → `fileexplorer-folder-open-16.png` (tree folder open, 16px)
  - `icons8-file-2d-{size}.png` → `fileexplorer-file-16.png` (tree file, 16px)
  - `icons8-hdd-{size}.png` → `fileexplorer-drive-16.png` (tree drive junction, 16px)
  - `icons8-usb-{size}.png` → `fileexplorer-usb-16.png` (tree USB, 16px)
  - `icons8-link-{size}.png` → `fileexplorer-link-20.png` (mount button, 20px)
  - `icons8-add-folder-{size}.png` → `fileexplorer-new-folder-20.png` (new folder, 20px)
  - `icons8-refresh-{size}.png` → `fileexplorer-refresh-20.png` (refresh, 20px)
  - `icons8-upload-3d-{size}.png` → `fileexplorer-upload-20.png` (upload, 20px)
  - `icons8-download-{size}.png` → `fileexplorer-download-16.png` (download context, 16px)
  - `icons8-delete-2d-{size}.png` → `fileexplorer-delete-16.png` (delete context, 16px)
  - `icons8-rename-2d-{size}.png` → `fileexplorer-rename-16.png` (rename context, 16px)
  - `icons8-upload-3d-48.png` → `fileexplorer-upload-48.png` (drop zone icon, 48px)
  - `icons8-folder-tree-2d-{size}.png` → `fileexplorer-empty-64.png` (empty state, resize to 64px)
- [x] 1.3 Copy close button from `Assets/Views/ConnectionWindow/connection-close-20.png` → `fileexplorer-close-20.png`

## 2. Model

- [x] 2.1 Create `Models/SftpEntry.cs` with: `Name`, `FullPath`, `IsDirectory`, `Size`, `LastModified`, `Extension`, computed `FormattedSize`, `IsJunction`
- [x] 2.2 Add implicit ordering: directories first, then files, alphabetical within groups

## 3. Service

- [x] 3.1 Add `Renci.SshNet` NuGet package to `XBVault.csproj`
- [x] 3.2 Create `Services/SftpService.cs` implementing `IDisposable`:
  - Private fields: `SshClient? _ssh`, `SftpClient? _sftp`
  - `Task ConnectAsync(string host, int port, string user, string pass)` — create clients, connect, handle `ssh-dss` key exchange
  - `void Disconnect()` — dispose clients
  - `bool IsConnected` — checks `_sftp?.IsConnected`
  - `Task<List<SftpEntry>> ListDirectoryAsync(string path)` — filter `.` and `..`, map to SftpEntry
  - `Task UploadFileAsync(Stream source, string remotePath, IProgress<double>? progress)` — with upload progress callback
  - `Task DownloadFileAsync(string remotePath, Stream destination, IProgress<double>? progress)` — with download progress callback
  - `Task DeleteFileAsync(string path)` — delete single file
  - `Task DeleteDirectoryAsync(string path)` — recursive directory delete via `SftpClient.DeleteDirectory`
  - `Task CreateDirectoryAsync(string path)` — create directory
  - `Task RenameAsync(string oldPath, string newPath)` — rename
  - `Task<SftpShellResult> RunShellCommandAsync(string command)` — open SSH shell, send command, capture output, check exit code
  - `event EventHandler<bool>? ConnectionChanged` — fires on connect/disconnect

## 4. XboxDeviceService changes

- [x] 4.1 Add `SshConnectionInfo GetSshCredentials()` method returning `(string Host, int Port, string Username, string Password)` — reuse stored IP and SMB credentials
- [x] 4.2 Add `SshConnectionInfo` record struct (or use existing pattern) as a return type

## 5. ViewModel

- [x] 5.1 Rewrite `ViewModels/FileExplorerViewModel.cs`:
  - Inject `XboxDeviceService` and `SftpService` via constructor
  - `[ObservableProperty]` fields: `IsConnected`, `IsLoading`, `SelectedEntry`, `CurrentPath` (default `D:\DevelopmentFiles\`), `UploadStatusText`, `UploadProgress`, `IsUploading`, `ErrorMessage`
  - `ObservableCollection<SftpEntry> TreeRoots`
  - `ObservableCollection<SftpEntry> CurrentEntries`
  - Computed: `ShowDisconnected => !IsConnected`, `ShowContent => IsConnected`, `CanMountDrives => IsConnected`, `CanCreateFolder => IsConnected`
- [x] 5.2 Implement `partial void OnIsConnectedChanged(bool value)` — notify computed properties (+ TreeRoots/CurrentEntries clear)
- [x] 5.3 Implement `[RelayCommand] async InitializeAsync()` — if connected call `ConnectAsync` + `LoadTreeRootsAsync`
- [x] 5.4 Implement `async Task LoadTreeRootsAsync()` — call `SftpService.ListDirectoryAsync(CurrentPath)`, populate `TreeRoots`
- [x] 5.5 Implement `[RelayCommand] async ExpandFolderAsync(string path)` — load child entries, single-expand logic (track expanded root, collapse previous)
- [x] 5.6 Implement `[RelayCommand] async NavigateToPathAsync(string path)` — update CurrentPath, load CurrentEntries
- [x] 5.7 Implement `[RelayCommand] async RefreshAsync()` — reload current path
- [x] 5.8 Implement `[RelayCommand] async UploadFilesAsync(string[] filePaths)` — iterate files, upload with progress, refresh on complete
- [x] 5.9 Implement `[RelayCommand] async DownloadFileAsync(SftpEntry entry)` — show SaveFileDialog, download, show result
- [x] 5.10 Implement `[RelayCommand] async DeleteEntryAsync(SftpEntry entry)` — show ConfirmWindow, delete, refresh
- [x] 5.11 Implement `[RelayCommand] async RenameEntryAsync(SftpEntry entry)` — prompt new name, rename, refresh
- [x] 5.12 Implement `[RelayCommand] async CreateFolderAsync()` — prompt name, create, refresh
- [x] 5.13 Implement `[RelayCommand] async MountDrivesAsync()` — run `mklink /J` for C:, D:, E: via shell, parse output, show success/error per drive

## 6. Views

- [x] 6.1 Rewrite `Views/FileExplorerView.axaml` with layout:
  - Row 0: Header bar with title + toolbar buttons (Mount Drives, New Folder, Refresh)
  - Row 1: Two-column split (GridSplitter between)
    - Left: TreeView with single-expand behavior, item template with icon + name
    - Right: Breadcrumb (ItemsControl with clickable TextBlocks) + ListBox (file list with grid columns for name, size, modified)
  - Row 2: Upload card (drop zone Border + Browse Files button + progress area)
  - Context menu on file list items (Download, Delete, Rename, separator, Properties)
  - "Not connected" overlay when ShowDisconnected is true
  - Bind all commands, visibilities, and collections

- [x] 6.2 Rewrite `Views/FileExplorerView.axaml.cs`:
  - Drag-drop event handlers (`OnDragOver`, `OnDrop`) on upload card
  - Wire `ListBox` right-click context menu
  - Register for `SftpService.ConnectionChanged` for cleanup

## 7. Documentation

- [x] 7.1 Rewrite `docs/FEATURE-FILE-EXPLORER.md` with SSH/SFTP architecture, new layout, API reference for SftpService, icon mapping

## 8. Verify

- [x] 8.1 Build project with `dotnet build XBVault/XBVault.csproj` — zero errors
- [x] 8.2 Run app, connect to Xbox, open File Explorer tab — verify tree loads `D:\DevelopmentFiles\`
- [x] 8.3 Expand folders, verify lazy loading and single-expand behavior
- [x] 8.4 Click "Mount Drives" — verify junctions created (check via SSH)
- [x] 8.5 Upload a file via drag-drop — verify progress bar, file appears in list
- [x] 8.6 Upload a file via Browse Files — verify same behavior
- [x] 8.7 Download a file via right-click — verify file saves locally
- [x] 8.8 Delete a file — verify confirm dialog, file removed
- [x] 8.9 Rename a file — verify name changes in list
- [x] 8.10 Create folder — verify new folder appears and is writable
- [x] 8.11 Refresh button — verify list reloads
- [x] 8.12 Disconnect/reconnect — verify SSH connection lifecycle

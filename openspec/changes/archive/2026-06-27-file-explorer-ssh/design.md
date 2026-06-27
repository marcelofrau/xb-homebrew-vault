## Context

Tab index 2 shows placeholder. Existing codebase has `XboxDeviceService` with `TestConnectionAsync()` (calls `/ext/smb/developerfolder` which returns SMB path + `DevToolsUser` creds) — same creds work for SSH on port 22. SSH.NET handles legacy `ssh-dss` key exchange required by Xbox.

SFTP subsystem may be chroot'd to `D:\DevelopmentFiles\`. SSH shell `mklink /J` creates junctions to bypass this — confirmed working by DanielLMcGuire/XboxSeries and Xbox One Research Wiki.

USB media (FAT32/NTFS) mounts as `E:\` — exposed via junction `D:\DevelopmentFiles\E → E:\`. Xbox-formatted XCRD USB (`[XE0:]`) is **not covered** (xrdutil needs testing on real hardware).

## Goals / Non-Goals

**Goals:**
- Browse Xbox filesystem via SFTP with TreeView + file list layout
- Upload files via drag-drop or file picker, with progress indicator
- Download files (right-click → Save As)
- Delete files/folders (right-click → confirm → delete)
- "Mount Drives" button runs `mklink /J` via SSH shell (C:, D:, E:)
- Create new folder (toolbar button)
- Rename file/folder (right-click context menu)
- Refresh current folder contents
- Reuse `DevToolsUser` credentials from XboxDeviceService
- Icons from `F:\workspace\icons8-personal-set` (16px tree items, 20px toolbar)

**Non-Goals:**
- USB XCRD (xrdutil) — needs hardware testing, defer to v2
- File preview (text, image, hex)
- Multi-select in file list
- Drag between tree and file list
- Companion UWP app (SSH replaces it)
- SMB file access (SFTP covers cross-platform)

## Decisions

### Decision 1: SSH.NET (`Renci.SshNet`) for SFTP

- **Choice:** `Renci.SshNet` 2024.2.0 — pure .NET, cross-platform, handles `ssh-dss` key exchange, stable API
- **Alternatives considered:** Raw SSH process (cmd), custom SSH library — rejected due to complexity and cross-platform issues
- **Rationale:** Most popular .NET SSH library (50M+ downloads), MIT license, actively maintained, fits iOS/Android future plans

### Decision 2: SftpService owns SSH lifecycle

- **Choice:** New `SftpService` class manages `SshClient` + `SftpClient` creation, connection, reconnect, disposal
- **Rationale:** Single responsibility, testable, ViewModel never touches SSH.NET directly. `XboxDeviceService.GetSshCredentials()` provides IP + port + user

### Decision 3: Single-expand TreeView

- **Choice:** Expanding a root node collapses any other expanded root
- **Rationale:** Prevents filesystem noise — Xbox Dev Mode has many root-level dirs; single-expand keeps focus

### Decision 4: Mount Drives as a button, not automatic

- **Choice:** "Mount Drives" button in toolbar, runs `mklink /J D:\DevelopmentFiles\{C,D,E} {C:\,D:\,E:\}` via SSH shell. Shows success/error toast
- **Rationale:** User requested non-automatic, with feedback. Junction creation can fail (path exists, permission) — explicit button surfaces errors clearly

### Decision 5: Upload overwrites directly, no confirm

- **Choice:** Upload starts immediately on file drop/select. No "file exists" prompt.
- **Rationale:** User requested direct upload. Xbox Dev Mode use case is developer files — speed > safety.

### Decision 6: Icons mapped from icons8-personal-set

- **Choice:** Icons from `F:\workspace\icons8-personal-set`. 16px for tree items, 20px for toolbar buttons.
- **Rationale:** Existing project uses Icons8 icons. Personal set has all needed icons in 3d-fluency style matching existing assets.

### Decision 7: Reuse close icon from connection assets

- **Choice:** Copy `connection-close-20.png` from `Assets/Views/ConnectionWindow/` for close button (same as other windows)
- **Rationale:** Consistent with existing window pattern, avoids duplicate icons

## Data Flow

```
User connects Xbox → XboxDeviceService stores IP + creds
         ↓
User clicks File Explorer tab → FileExplorerViewModel checks IsConnected
         ↓
Connected → SftpService.ConnectAsync(host, port, user, pass)
         ↓
SftpClient created → ListDirectory(@"D:\DevelopmentFiles\") → populate tree roots
         ↓
User expands folder → SftpClient.ListDirectory(path) → lazy-load children
         ↓
User clicks file → show details in right pane
         ↓
User drops file/s → SftpClient.UploadFile(stream, path) → progress callback → UI update
         ↓
User right-clicks → context menu → Download / Delete / Rename
         ↓
User clicks "Mount Drives" → SSH shell: `mklink /J D:\DevFiles\C C:\` → refresh tree
```

## Service Interface

```
SftpService : IDisposable
├── Task ConnectAsync(string host, int port, string user, string pass)
├── void Disconnect()
├── bool IsConnected
├── Task<List<SftpEntry>> ListDirectoryAsync(string path)
├── Task UploadFileAsync(Stream source, string remotePath, IProgress<double>? progress)
├── Task DownloadFileAsync(string remotePath, Stream destination, IProgress<double>? progress)
├── Task DeleteFileAsync(string path)
├── Task DeleteDirectoryAsync(string path)
├── Task CreateDirectoryAsync(string path)
├── Task RenameAsync(string oldPath, string newPath)
├── Task<SftpShellResult> RunShellCommandAsync(string command)
└── event EventHandler<bool>? ConnectionChanged
```

## ViewModel State

```
FileExplorerViewModel : ObservableObject
├── [ObservableProperty] bool IsConnected
├── [ObservableProperty] bool IsLoading
├── [ObservableProperty] SftpEntry? SelectedEntry
├── [ObservableProperty] string CurrentPath (= "D:\\DevelopmentFiles\\")
├── [ObservableProperty] string UploadStatusText
├── [ObservableProperty] double UploadProgress
├── [ObservableProperty] bool IsUploading
├── [ObservableProperty] string? ErrorMessage
│
├── ObservableCollection<SftpEntry> TreeRoots
├── ObservableCollection<SftpEntry> CurrentEntries
│
├── Computed: ShowDisconnected, ShowContent, CanMountDrives, CanCreateFolder
│
├── [RelayCommand] async ConnectAsync()
├── [RelayCommand] async ExpandFolderAsync(string path)
├── [RelayCommand] async RefreshAsync()
├── [RelayCommand] async UploadFilesAsync(string[] filePaths)
├── [RelayCommand] async DownloadFileAsync(SftpEntry entry)
├── [RelayCommand] async DeleteEntryAsync(SftpEntry entry)
├── [RelayCommand] async RenameEntryAsync(SftpEntry entry)
├── [RelayCommand] async CreateFolderAsync()
├── [RelayCommand] async MountDrivesAsync()
├── [RelayCommand] async NavigateToPathAsync(string path)
```

## AXAML Layout

```
UserControl
└── Grid (connected content, toggled via ShowContent)
    ├── Row 0: Header bar (auto height)
    │   ├── TextBlock "FILE EXPLORER"
    │   └── Toolbar: [Mount Drives] [New Folder] [Refresh]
    │
    ├── Row 1: Split pane (*)
    │   ├── GridSplitter
    │   ├── Left: TreeView (260px, min 180)
    │   │   └── TreeViewItem template: icon + name
    │   │       └── single-expand: expand root collapses others
    │   └── Right: Grid (rows: breadcrumb + file list)
    │       ├── Breadcrumb bar (auto): clickable path segments
    │       └── ListBox / DataGrid (file list)
    │           ├── Columns: icon | name | size | modified
    │           ├── Right-click context menu: Download, Delete, Rename
    │           └── Empty state when no contents
    │
    └── Row 2: Upload card (auto height)
        ├── Drag-and-drop zone (accepts files)
        ├── "Browse Files" button → OpenFileDialog (multi-file)
        ├── Target: <current path>
        ├── ProgressBar + status text (when uploading)
        └── Result toast (success/fail per file)
```

## Icons Mapping

All sourced from `F:\workspace\icons8-personal-set`:

| Usage | Size | Icon file |
|---|---|---|
| Tree folder closed | 16px | `icons8-folder-16.png` |
| Tree folder open | 16px | `icons8-opened-folder-16.png` |
| Tree file | 16px | `icons8-file-2d-16.png` |
| Tree drive (junction) | 16px | `icons8-hdd-16.png` |
| Tree USB drive | 16px | `icons8-usb-16.png` |
| Mount drives button | 20px | `icons8-link-20.png` |
| New folder button | 20px | `icons8-add-folder-20.png` |
| Refresh button | 20px | `icons8-refresh-20.png` |
| Upload button | 20px | `icons8-upload-3d-20.png` |
| Download (context) | 16px | `icons8-download-16.png` |
| Delete (context) | 16px | `icons8-delete-2d-16.png` |
| Rename (context) | 16px | `icons8-rename-2d-16.png` |
| Close button | 20px | `connection-close-20.png` (reuse from ConnectionWindow) |
| Drop zone icon | 48px | `icons8-upload-3d-48.png` |
| Empty state icon | 64px | `icons8-folder-tree-2d-64.png` (resize from 48) |

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| Xbox SSH server chroot'd to `D:\DevelopmentFiles\|` — no `C:\` access without junctions | Mount Drives button runs `mklink /J` via shell to expose other drives. Junction technique confirmed working. |
| SFTP may not support all operations (rename, delete) | SSH.NET SftpClient wraps OpenSSH SFTP subsystem — all standard operations work. Fall back to shell command if SFTP fails. |
| SSH connection drops during long upload | SftpService auto-reconnect on next operation, with timeout and retry. Upload progress preserved. |
| mklink fails if junction path already exists | Shell command checks `if not exist ... mklink /J ...`. Error message surfaces in toast. |
| USB XCRD (xrdutil) not covered | Deferred to v2. Regular USB (FAT32/NTFS as `E:\`) works via junction. |
| `async void` in event handlers (existing pattern) | Use async event handlers with try-catch wrapping (same pattern as rest of codebase). |

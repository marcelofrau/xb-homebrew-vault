# File Explorer — SSH/SFTP Browser

## Goal
Replace the `FileExplorerView` placeholder tab (index 2) with a functional file browser for the Xbox Dev Mode filesystem using SSH/SFTP — no companion app, no REST API.

## Architecture
```
┌─────────────┐     ┌──────────────┐     ┌───────────────────┐
│ XBVault App │────→│ SftpService  │────→│ Xbox (port 22)    │
│             │     │ (SSH.NET)    │     │  ├─ SFTP subsystem │
│ TreeView    │     │ SshClient    │     │  └─ SSH shell      │
│ File List   │     │ SftpClient   │     │     (mklink /J)    │
│ Upload Card │     │ Shell (cmd)  │     └───────────────────┘
└─────────────┘     └──────────────┘
```

**Key insight:** WDP endpoint `/ext/smb/developerfolder` returns SMB path + `DevToolsUser` credentials — same credentials work for SSH on port 22. `mklink /J` via SSH shell bypasses SFTP chroot to expose `C:\`, `D:\`, `E:\`.

## Layout
```
┌──────────────────────────────────────────────────────────┐
│ FILE EXPLORER     [Mount Drives] [New Folder] [Refresh]   │
├──────────────────┬───────────────────────────────────────┤
│ TreeView (260px)  │ Breadcrumb: D:\DevelopmentFiles\...  │
│  D:\Development\  ├───────────────────────────────────────┤
│  ├── C:\ (junc)   │ Name         Size     Modified       │
│  │   ├── Data     │ 📄 pkg.appx  12 MB   2026-06-20      │
│  │   └── Users    │ 📄 test.dll  340 KB  2026-06-19      │
│  ├── D:\ (junc)   │ 📁 Subfolder  —      2026-06-18      │
│  └── E:\ (junc)   │                                       │
│                   │ [right-click: Download / Delete /    │
│ (single-expand)   │  Rename / Properties]                │
├──────────────────┴───────────────────────────────────────┤
│ Upload card: [Drop files here]  or  [Browse Files]        │
│ ████████████░░░░ 60% — package.appx                      │
│ Target: D:\DevelopmentFiles\current\folder\              │
└──────────────────────────────────────────────────────────┘
```

## Key Components

### SftpService (`Services/SftpService.cs`)
Wrapper around `Renci.SshNet` (`SshClient` + `SftpClient`). All file operations go through SFTP; shell commands used only for `mklink /J`.

| Method | Description |
|---|---|
| `ConnectAsync(host, port, user, pass)` | Open SSH + SFTP connection |
| `Disconnect()` | Close connection |
| `ListDirectoryAsync(path)` | List entries, filter `.`/`..` |
| `UploadFileAsync(stream, path, progress)` | Upload with % callback |
| `DownloadFileAsync(path, stream, progress)` | Download with % callback |
| `DeleteFileAsync(path)` | Remove file |
| `DeleteDirectoryAsync(path)` | Remove dir recursively |
| `CreateDirectoryAsync(path)` | mkdir |
| `RenameAsync(old, new)` | Rename/move |
| `RunShellCommandAsync(cmd)` | SSH shell for mklink |

### Mount Drives
Button runs via SSH shell:
```cmd
if not exist D:\DevelopmentFiles\C mklink /J D:\DevelopmentFiles\C C:\
if not exist D:\DevelopmentFiles\D mklink /J D:\DevelopmentFiles\D D:\
if not exist D:\DevelopmentFiles\E mklink /J D:\DevelopmentFiles\E E:\
```
Junctions make `C:\`, `D:\`, `E:\` appear as folders inside `D:\DevelopmentFiles\`, visible to SFTP. USB media (FAT32/NTFS at `E:\`) works via this mechanism. XCRD USB (`[XE0:]`) requires `xcrdutil` — out of scope (v2).

### TreeView
- Single-expand: expanding a root collapses any previously expanded root
- Lazy-load children on expand (calls `ListDirectoryAsync`)
- Icons: closed folder, open folder, file, drive (junction)

### File List
- Grid columns: icon, name, size (formatted), last modified
- Directories before files, alphabetical
- Right-click context menu: Download, Delete, Rename

### Upload Card
- Drag-drop zone (`DragDrop` events)
- Browse Files button (`OpenFileDialog`, multi-file)
- Progress bar per file + status text
- Uploads to currently selected folder path
- No overwrite confirmation (direct upload)

## Files to Create/Modify

**New files:**
| File | Purpose |
|---|---|
| `Services/SftpService.cs` | SSH/SFTP wrapper (SSH.NET) |
| `Models/SftpEntry.cs` | File/dir model: name, path, size, date, isDir |

**Modified files:**
| File | Change |
|---|---|
| `XBVault.csproj` | Add `Renci.SshNet` NuGet |
| `Services/XboxDeviceService.cs` | Add `GetSshCredentials()` |
| `ViewModels/FileExplorerViewModel.cs` | Rewrite (34→~250 lines) |
| `Views/FileExplorerView.axaml` | Rewrite placeholder |
| `Views/FileExplorerView.axaml.cs` | Drag-drop handlers |

**Icons** (from `D:\workspace\_non_work_\icons8-personal-set`):
See `openspec/changes/file-explorer-ssh/tasks.md` §1 for full mapping.

## Non-Goals
- USB XCRD (`xcrdutil`) — delayed to v2 (needs hardware testing)
- File preview (text, image, hex)
- Multi-select in file list
- Companion UWP app (SSH replaces it)

## Reference
- OpenSpec change: `openspec/changes/file-explorer-ssh/`
- SSH.NET: https://github.com/sshnet/SSH.NET (MIT)
- Xbox SSH techniques: DanielLMcGuire/XboxSeries, Xbox One Research Wiki

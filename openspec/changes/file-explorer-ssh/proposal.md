## Why

The File Explorer tab (index 2) shows a placeholder "Not implemented yet." Users manage Xbox files manually — WinSCP for SFTP, browser for Dev Portal, console for USB. No unified native file browser exists for Xbox Dev Mode.

The Dev Portal REST filesystem API (`/api/filesystem/apps/files`) doesn't exist on Xbox (Desktop/HoloLens/IoT only). But Xbox Dev Mode ships with SSH/SFTP natively — same `DevToolsUser` credentials as the SMB share — making a companion file browser possible via SSH.NET without any companion app.

## What Changes

- Replace `FileExplorerView` placeholder with a functional file browser using SSH/SFTP (Renci.SshNet)
- Add `SftpService` — wrapper around SSH.NET's SftpClient (connect, list, upload, download, delete, rename, mkdir, mklink)
- Rewrite `FileExplorerViewModel` (34-line stub → full tree + file list + upload behavior)
- Rewrite `FileExplorerView.axaml` with TreeView (left, 260px), file list (right), upload card (bottom)
- Add "Mount Drives" button — runs `mklink /J` over SSH shell to expose `C:\`, `D:\`, `E:\` via junctions
- SSH.NET NuGet dependency (`Renci.SshNet`, MIT)
- Icons from `F:\workspace\icons8-personal-set` (file, folder, upload, download, delete, refresh, drives)
- New service `SftpService` manages SSH connection lifecycle; shares credentials from XboxDeviceService
- `FEATURE-FILE-EXPLORER.md` rewritten to reflect SSH/SFTP architecture (replaces old REST + companion app plan)

## Capabilities

### New Capabilities

- `ssh-sftp-file-browser`: Browse Xbox filesystem via SFTP, upload/download/delete/rename, TreeView with single-expand, upload card with drag-drop + file picker, Mount Drives via SSH shell junctions

### Modified Capabilities

None — net-new feature, no changes to existing services or views.

## Impact

- **New files:** `Services/SftpService.cs`, `Models/SftpEntry.cs`, rewriting `ViewModels/FileExplorerViewModel.cs`, `Views/FileExplorerView.axaml`, `Views/FileExplorerView.axaml.cs`, new icons under `Assets/Views/FileExplorerView/`
- **Modified files:** `XBVault/XBVault.csproj` (add SSH.NET), `XboxDeviceService.cs` (add `GetSshCredentials()`), `docs/FEATURE-FILE-EXPLORER.md` (rewrite)
- **New dependency:** `Renci.SshNet` 2024.2.0 (MIT, 50M+ downloads, pure .NET, cross-platform)
- **Zero changes to** MainWindow, settings, connection flow, or any other tab

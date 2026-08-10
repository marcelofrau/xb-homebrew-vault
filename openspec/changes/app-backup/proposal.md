## Why

Homebrew apps on the Xbox hold saves, config, and LocalState that users don't want to lose, and re-downloading/installing an app is easy but restoring its *data* is not. XBVault already has the two file-access paths needed (REST filesystem via `PortalAppFilesService` for LocalAppData/LocalState, and SSH/SFTP like the file explorer for arbitrary remote folders) and can pull the `.appx` package itself. This change packages all three into a single backup ZIP per app.

## What Changes

- Add a per-app **backup action** (Installed tab flyout: "Backup app") that produces one timestamped `.xvbk` ZIP on the PC containing **up to three parts**:
  1. **`.appx` package** — the app package pulled from the console when retrievable (research the reliable source: portal filesystem vs SFTP path).
  2. **LocalAppData / LocalState** — the app's data folder pulled via the existing portal REST filesystem (`PortalAppFilesService`), recursive.
  3. **User-selected remote folders** — a multi-select dialog listing remote folders (SSH/SFTP, same style as the file explorer) where the user picks additional paths to include.
- Parts are optional: if the appx is not retrievable or the user selects no extra folders, the ZIP still contains what's available.
- Backup runs as a `BackgroundTaskService` task with progress (REST + SFTP pulls can be slow) and cancels cleanly.
- Per-app backup stays local: destination chosen by the user (default `%USERPROFILE%/XBVault-backups`).
- v1 scope: one app at a time. Bulk/multi-app backup is a follow-up.

## Capabilities

### New Capabilities
- `app-backup-wizard`: the per-app backup flow — source selection dialog (remote folders multi-select), destination, confirmation.
- `app-backup-package`: pull the `.appx` from the console (research-backed source) with progress.
- `app-backup-localdata`: recursive pull of the app's LocalAppData/LocalState via `PortalAppFilesService` (REST) with progress + cancellation.
- `app-backup-custom-dirs`: multi-select remote-folder dialog (SFTP) + recursive pull of the chosen paths.
- `app-backup-zip`: assemble the `.xvbk` ZIP (appx + localdata + custom dirs + manifest) with integrity notes.

### Modified Capabilities
- (none)

## Impact

- **New files**: `XBVault/Services/AppBackupService.cs` (orchestrator + zip), `XBVault/Services/AppBackupPackage.cs` (appx pull), `XBVault/Services/AppBackupLocalData.cs` (REST pull), `XBVault/Services/AppBackupCustomDirs.cs` (SFTP pull), `XBVault/ViewModels/BackupAppViewModel.cs`, `XBVault/Views/BackupAppDialog.axaml`
- **Modified files**: `XBVault/ViewModels/InstalledViewModel.cs` (flyout entry), `XBVault/Views/InstalledView.axaml` (menu item + progress)
- **Reuses existing**: `PortalAppFilesService` (LocalAppData tree), SFTP stack used by file explorer (`ISftpService`/`SftpTransferService`), `BackgroundTaskService` for progress
- **Depends on**: `background-async-tasks` (progress + task center)
- **No breaking changes** — read-only against the console, nothing on the Xbox is modified

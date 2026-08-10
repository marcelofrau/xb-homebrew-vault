## 1. Appx source research

- [ ] 1.1 Research spike: determine reliable `.appx` retrieval source (portal filesystem endpoint vs SFTP `D:\DevelopmentFiles\...` vs download endpoint); record finding in design
- [ ] 1.2 Create `AppBackupPackage` using the confirmed source; best-effort with `NotRetrievable` fallback

## 2. Pullers

- [ ] 2.1 Create `AppBackupLocalData` — recursive pull of LocalAppData/LocalState via `PortalAppFilesService` (REST), progress + cancellation
- [ ] 2.2 Create `AppBackupCustomDirs` — recursive SFTP pull of user-selected paths, progress

## 3. Orchestrator + ZIP

- [ ] 3.1 Create `AppBackupService` — orchestrates pullers into temp staging, weighted progress, `manifest.json`, zip to `.xvbk` via atomic rename
- [ ] 3.2 Add destination free-space check before starting

## 4. Wizard UI

- [ ] 4.1 Create `BackupAppViewModel` + `BackupAppDialog.axaml` — app info, multi-select remote-folder dialog (file-explorer style), destination picker, run as task-center task
- [ ] 4.2 Add "Backup app" flyout item in Installed tab

## 5. Validation

- [ ] 5.1 `dotnet build` passes
- [ ] 5.2 Manual: backup an app with custom folders → open ZIP, verify appx/localdata/custom + manifest
- [ ] 5.3 Manual: cancel mid-backup → no partial ZIP at destination

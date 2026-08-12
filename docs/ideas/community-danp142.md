# Community Idea: DanP142

Ideas submitted by **DanP142** (Discord, 2026-07-04).

---

## 1. Outdated Label + Update Button (Installed View)

**Problem:** User doesn't know if an installed package has a newer version in the catalog. Must remember to manually check.

**Suggestion:** In the installed packages list, show "Outdated" label next to apps with a newer version available + "Update" button to reinstall.

**Status:** Already documented in [version-checker-bulk-update.md](version-checker-bulk-update.md) with broader scope (VersionCheckerService + bulk update). Dan's suggestion covers the UI subset — label + individual update — which could be implemented as an MVP before bulk update.

**Extra ideas from Dan:**
- Visual badge in list (e.g., red pill "OUTDATED v1.2 → v1.5")
- Inline update button on the same row
- Optionally: auto-check on opening Installed tab

---

## 2. Updates & Backups Section

**Problem:** No backup system for saves/configs before updating or reinstalling a package. User may lose game progress or emulator settings.

### Automatic Updates
- Settings toggle: "Enable automatic updates"
- When on: periodically checks updates and installs in background
- Can be restricted to "only auto-update when Xbox is idle" or "only over wired connection"

### Backup System
**Not currently implemented.** No backup code exists in the project.

**Proposal:**

| Type | What to save | Where to save | How |
|------|-------------|---------------|-----|
| Configs | App configuration folders (`LocalState`, `AppData` on Xbox) | `%APPDATA%/XBVault/backups/{app}/` | Recursive SFTP download |
| Saves | Game saves (emulators, UWP) | Same directory | SFTP |
| Full app | Original `.appx`/`.msix` package | Cache | Already in cache |
| Registry | Local `settings.json` backup | Same directory | Local copy |

**Suggested UI:**

```
Settings > Backups
┌─────────────────────────────────────────────┐
│ 💾 Backups                                   │
│─────────────────────────────────────────────│
│ [✔] Enable automatic backups                │
│ Schedule: [Daily ⬎]                         │
│                                             │
│ Backup Location:                             │
│ [C:\Users\...\XBVault\backups\] [Browse]    │
│                                             │
│ Apps to backup:                             │
│ [☐ All apps]                                │
│ [☑ RetroArch]         Last: 2026-07-03      │
│ [☑ PPSSPP]            Last: 2026-07-01      │
│ [☐ DuckStation]       Never                 │
│                                             │
│ [Backup Now] [Restore...]                   │
│                                             │
│ Storage used: 342 MB                        │
└─────────────────────────────────────────────┘
```

**Restore flow:**
1. Select app
2. Choose restore point (by date)
3. Preview files
4. Confirm overwrite
5. Upload via SFTP back to Xbox

### Dependencies
- Recursive SFTP download (already exists in `SftpService`)
- SFTP upload (already exists)
- File preview: reuse `FileExplorerViewModel` pattern
- Schedule timer: `TaskSchedulerService` (see [scheduled-tasks.md](scheduled-tasks.md))

### Files to create
- `Services/BackupService.cs`
- `Models/BackupProfile.cs`
- `Views/BackupSettingsView.axaml` + `.axaml.cs`
- `ViewModels/BackupSettingsViewModel.cs`

### Files to modify
- `SettingsView.axaml` — new "Backups" tab
- `SettingsViewModel.cs` — navigation to backup settings
- `InstalledViewModel.cs` — trigger backup before update

---

## 3. Crash Data from Device Portal

**Problem:** Users want to capture crash dumps from Xbox for debugging when apps fail.

**Status:** ✅ **Already implemented.**

| Component | File |
|-----------|------|
| View | `Views/CrashDataWindow.axaml` |
| ViewModel | `ViewModels/CrashDataViewModel.cs` |
| API calls | `XboxSystemService.GetCrashDumpsAsync()`, `DeleteCrashDumpAsync()` |
| Model | `Models/CrashDumpInfo.cs` |
| Access | ToolsView → "Crash Data" button |

**What already exists:**
- Crash dump list with name, date, size
- Toggle enable/disable crash dump collection
- Individual download
- Individual delete + Delete All
- Status messages

**Possible improvements (following spirit of Dan's suggestion):**
- [ ] Auto-refresh listing (polling every 30s while window is open)
- [ ] "Save All to Folder" button (batch download)
- [ ] Visual sidebar indicator if new crash dump detected
- [ ] Toast notification "New crash dump detected" (see [quick-wins.md](quick-wins.md))

---

## Summary

| Idea | Status |
|------|--------|
| Outdated label + update | Documented in version-checker-bulk-update.md |
| Automatic updates | Documented in version-checker-bulk-update.md |
| Backup system | **New** — no implementation |
| Crash data | ✅ Already exists |
| Crash data improvements | Opportunities listed above |

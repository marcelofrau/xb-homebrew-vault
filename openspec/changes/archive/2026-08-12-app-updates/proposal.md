## Why

Users don't know when an installed homebrew app has a newer catalog version. The comparison logic already exists (Browse tab marks OUTDATED cards, InstalledView has an Update action that opens `ItemDetailWindow` in update mode, and `UpdateVersionCache` suppresses repeat notifications per version pair). What's missing is the *proactive* piece: checking in the background while connected and telling the user without flooding them.

## What Changes

- Add a **background update check** (a `BackgroundTaskService` job) that runs while connected to the Xbox: compares installed apps against the catalog (same PFN/version logic already in `BrowseViewModel`) and collects the list of updatable apps.
- Add a **single consolidated notification**: one notification listing all updatable apps (not one toast per app — avoids toast flooding). Each app in the notification is clickable and opens the existing update flow (`ItemDetailWindow` in update mode, or the Installed tab with the OUTDATED badge highlighted).
- Notifications SHALL be **re-openable**: they land in the notification center (status-bar notification icon + panel added by the foundation change), so the user can follow up later after dismissing.
- Respect `UpdateVersionCache`: don't re-notify for the same installed→catalog version pair already seen.
- Check runs only while connected and at a sensible cadence (on connect + periodic while connected, configurable); catalog must be loaded (reuse the already-loaded catalog when present).
- XBVault's **own** update check is already implemented (`App.axaml.cs` `CheckForUpdatesAsync`, dialog on startup) — this change does NOT touch it.

## Capabilities

### New Capabilities
- `app-update-scan`: background job (on connect + periodic while connected) comparing installed vs catalog versions, using existing PFN match/version logic and `UpdateVersionCache` dedupe.
- `app-update-notify`: single consolidated notification listing updatable apps, each clickable to the existing update dialog; lands in the notification center for follow-up.

### Modified Capabilities
- (none)

## Impact

- **New files**: `XBVault/Services/InstalledAppUpdateService.cs` (scan orchestrator + dedupe), `XBVault/ViewModels/UpdateNotificationViewModel.cs` (notification content/actions)
- **Modified files**: connection flow (schedule job when connected), `XBVault/ViewModels/BrowseViewModel.cs` (extract shared version-match logic, if needed), Installed/`ItemDetailWindow` wiring (accept a "focus this app" entry point)
- **Reuses existing**: catalog comparison in `BrowseViewModel`, `UpdateVersionCache`, `ItemDetailWindow` update mode, OUTDATED badge, `BackgroundTaskService` + notification center (foundation)
- **Depends on**: `background-async-tasks` (job + notification center)
- **Relates to**: `docs/ideas/version-checker-bulk-update.md` (remaining items: VersionCheckerService, Updates badge)
- **No breaking changes** — read-only against catalog/console

## 1. Shared version checker

- [ ] 1.1 Extract `VersionCheckerService` from `BrowseViewModel` comparison logic (PFN match + version compare + outdated) returning `OutdatedPackage[]`; refactor BrowseViewModel to use it
- [ ] 1.2 Ensure compatibility/architecture filter is preserved

## 2. Scan job

- [ ] 2.1 Create `InstalledAppUpdateService` — registered job on `BackgroundTaskService` (on connect + periodic, default 30 min, configurable), connected-gated, catalog-loaded guard
- [ ] 2.2 Wire dedupe through `UpdateVersionCache` (only new version pairs notify)

## 3. Consolidated notification

- [ ] 3.1 Create `UpdateNotificationViewModel` — one notification listing updatable apps, per-app click command
- [ ] 3.2 Add "open update dialog for package X" action (routes to `ItemDetailWindow` update mode / Installed activation)
- [ ] 3.3 Raise notification via notification center; confirm re-open after dismissal works

## 4. Validation

- [ ] 4.1 `dotnet build` passes
- [ ] 4.2 Manual: install older version than catalog → connect → one notification lists it → click opens update dialog
- [ ] 4.3 Manual: three outdated apps → single notification with three entries; dismiss → re-open from notification center
- [ ] 4.4 Manual: disconnect → no scan; same version pair → no duplicate notification

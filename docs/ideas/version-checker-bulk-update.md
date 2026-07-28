# Enhanced Version Checker + Bulk Update

**Impact:** High | **Effort:** Medium | **Suggested priority:** Phase 3

## Status — ✅ Partial (v1.2.0)

**Done:**
- `InstalledPackage.IsOutdated` property
- OUTDATED badge on cards (red rounded)
- Orange accent strip + orange bold version on outdated cards
- Update button in InstalledView toolbar (enabled only when outdated, Accent class)
- "Update" button in InstalledView opens `ItemDetailWindow` in update mode
- Version comparison via `System.Version`
- Hamburger flyout "Update" item (visible when outdated)
- Finish button after successful update in ItemDetailWindow
- Refresh InstalledView on dialog close after update

**Remaining:**
- Sidebar badge "Updates (N)"
- Dedicated Updates tab / window
- Bulk Update / Update All queue
- `VersionCheckerService`

## Problem

Users install packages from the catalog and never know if a newer version exists. They need to manually visit the catalog, compare versions, and reinstall.

## Proposal

### VersionCheckerService
- Compares `InstalledPackage.Version` with `CatalogItem.Version` by `PackageFamilyName` (PFN)
- Uses `PackageOverrideService` to resolve PFN → CatalogId when needed
- Returns list of `OutdatedPackage`: (installed, available, catalogItem, isUpdateSafe)

### UI: "Updates Available"
- Sidebar badge "Updates (3)" when updates exist
- New sub-tab in InstalledView or separate window: "Updates"
- List: PackageName | Installed | Available | [Update] [Update All]
- Each "Update" runs the download + install pipeline
- "Update All" creates queue and executes sequentially

### Safety
- Architecture compatibility check (already exists `FilterByArchitecture`)
- Dependency check before updating
- Option to ignore specific version (like in auto-update)

### Data
```csharp
class OutdatedPackage
{
    InstalledPackage Installed;
    CatalogItem Catalog;
    Version InstalledVersion;
    Version AvailableVersion;
    bool IsCompatible;
}
```

### Dependencies
- `PackageOverrideService` (already exists)
- Version comparison logic (`System.Version` or semver parsing)
- `PackageInstallService` (already exists)

### Files to create
- `Services/VersionCheckerService.cs`
- `Views/UpdatesView.axaml` + `.axaml.cs`
- `ViewModels/UpdatesViewModel.cs`

### Files to modify
- `InstalledView.axaml` — "Updates available" badge or new tab
- `InstalledViewModel.cs` — `CheckForUpdatesCommand`
- `MainWindow.axaml` — sidebar badge "Updates (N)"
- `MainViewModel.cs` — update count

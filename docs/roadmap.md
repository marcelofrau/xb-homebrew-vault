---
layout: default
title: Roadmap
---

# Roadmap

## Current Status

**Latest release: v0.9.4** · [Download](https://github.com/marcelofrau/xb-homebrew-vault/releases/latest)

The app is feature-complete for daily Xbox Dev Mode homebrew management. Core flows — first-run setup, browse, install, uninstall, dev tools, USB permissions — are all shipping and stable. **v0.9.0 shipped a full File Explorer** with dual-pane tree + list view, upload/download with progress bars, delete, and create folder. **v0.9.1–v0.9.4** focused on SFTP performance (60+ MB/s), tech debt reduction, cross-platform hardening, TreeView layout fixes, and CI VirusTotal integration.

---

## Version History

```mermaid
timeline
    title XB Homebrew Vault releases
    v0.1 : Scaffold, theme, splash, sidebar, build scripts
    v0.2 : Connection wizard, settings, obfuscation
    v0.5 : Avalonia 12 migration, button theming
    v0.7 : Accurate link speed detection, FileExplorer placeholder, Tools skeleton
    v0.8 : Launch/suspend packages, crash dumps, network info, performance monitor
    v0.8.1 : Connection link speed detection
    v0.8.2 : Pipe fix, running state indicator
    v0.8.4 : Settings redesign, live screenshot, item-detail overlay, theme polish
    v0.8.5 : catalog.json migration, UWP Port field, cache expiry, confirm dialogs
    v0.8.6 : First-run setup wizard, USB permission wizard, spinner polish
    v0.9.0 : Full File Explorer (dual-pane tree/list, upload/download, delete, create folder)
    v0.9.1 : SFTP performance rewrite (60+ MB/s), title gradient, window close button, magic delays → constants
    v0.9.2 : CatalogApiService DI, WINDOWS_BUILD guard, silent catches → logged, PerformanceChart tweak
    v0.9.4 : TreeView chevron fix (drives vs folders), duplicate handler cleanup, NavigateToPath dispatcher removed, CI VirusTotal integration
```

## What's Shipped

| Phase | Version | Highlights |
|-------|---------|-----------|
| Scaffold | v0.1 | Project structure, Blades theme, splash screen, sidebar navigation, build scripts |
| Connection | v0.2 | Xbox connection wizard, settings persistence, credential obfuscation |
| UI Migration | v0.5 | Avalonia 12 migration, button theming, visual polish |
| Tools skeleton | v0.7 | Link speed detection, File Explorer placeholder, Tools panel skeleton |
| Full tools | v0.8 | Launch/suspend packages, crash dump viewer, network info, performance monitor |
| Bugfixes | v0.8.1–v0.8.2 | Connection link speed, pipe fix, running state indicator |
| Polish | v0.8.4 | Settings redesign, live screenshot capture, item-detail overlay, theme tweaks |
| Catalog API | v0.8.5 | Migrated from HTML scraping to `catalog.json`, UWP Port field, cache expiry, confirm dialogs, dependency selection in wizard |
| Setup & USB | v0.8.6 | First-run setup wizard (3-step), USB permission wizard with WMI detection + icacls, spinner + min-delay polish |
| File Explorer | v0.8.7 | Functional SSH/SFTP file browser — browse, upload/download with progress, drive mounting via `mklink` |
| File Explorer (full) | v0.9.0 | Dual-pane tree + list, folder upload, delete confirm, progress bars, toolbar status, file-type icons |
| SFTP Performance | v0.9.1 | Rewrite: 32 KB loop → native UploadFile/DownloadFile, dynamic buffer (64/256/512 KB), 60+ MB/s |
| Quick wins | v0.9.1 | TitleGradient resource, unified WindowClose button, magic delays → named constants, deleted _Backup |
| Stabilization | v0.9.2 | CatalogApiService constructor injection, WINDOWS_BUILD conditional compile, silent catches → logged, PerformanceChart MaxPoints 30 |
| TreeView & cleanup | v0.9.4 | TreeView chevron offset fix (drives vs folders), duplicate pointer handler consolidation, NavigateToPath dispatcher bottleneck removed, CI VirusTotal integration |

### Feature Delivery Timeline

```mermaid
gantt
    title Feature delivery per version
    dateFormat  YYYY-MM-DD
    section Connection
    Connection wizard            :done, 2026-01, 2026-02
    Connection guard             :done, 2026-04, 2026-04
    Link speed detection         :done, 2026-06, 2026-06
    First-run setup wizard       :done, 2026-06, 2026-06
    section Catalog
    Emulation Revival scraper    :done, 2026-02, 2026-03
    Category/compat filters      :done, 2026-03, 2026-03
    Item details                 :done, 2026-03, 2026-03
    Migrate to catalog.json      :done, 2026-06, 2026-06
    section Package Management
    Install/uninstall            :done, 2026-03, 2026-04
    Custom install wizard        :done, 2026-04, 2026-05
    Dependency selection wizard  :done, 2026-06, 2026-06
    Launch/suspend/terminate     :done, 2026-06, 2026-06
    Running state indicator      :done, 2026-06, 2026-06
    section Tools
    Processes (list/kill)        :done, 2026-05, 2026-06
    Network info                 :done, 2026-05, 2026-06
    System info                  :done, 2026-05, 2026-06
    Crash dumps                  :done, 2026-06, 2026-06
    Screenshot (live capture)    :done, 2026-05, 2026-06
    Performance monitor          :done, 2026-05, 2026-06
    USB permission wizard        :done, 2026-06, 2026-06
    section UI
    Xbox 360 Blades theme        :done, 2026-01, 2026-01
    Settings redesign            :done, 2026-06, 2026-06
    Item-detail overlay          :done, 2026-06, 2026-06
    section File Explorer
    SSH/SFTP file browser        :done, 2026-06, 2026-06
    Upload/download with progress:done, 2026-06, 2026-06
    Drive mounting via mklink    :done, 2026-06, 2026-06
    section v0.9.x Stabilization
    SFTP performance rewrite     :done, 2026-06, 2026-06
    TitleGradient resource       :done, 2026-06, 2026-06
    WindowClose button           :done, 2026-06, 2026-06
    Magic delays to constants    :done, 2026-06, 2026-06
    CatalogApiService DI         :done, 2026-06, 2026-06
    WINDOWS_BUILD cond guard     :done, 2026-06, 2026-06
    Silent catches → log         :done, 2026-06, 2026-06
    Deleted _Backup              :done, 2026-06, 2026-06
    TreeView chevron fix         :done, 2026-06, 2026-06
    VirusTotal CI                :done, 2026-06, 2026-06
```

### Feature Breakdown

| Area | Feature | Status |
|------|---------|--------|
| Connection | Xbox Device Portal connect | ✅ |
| Connection | Saved credentials (obfuscated) | ✅ |
| Connection | Link speed detection | ✅ |
| Connection | First-run setup wizard (3-step) | ✅ v0.8.6 |
| Catalog | Emulation Revival `catalog.json` API | ✅ v0.8.5 |
| Catalog | Category / compatibility filters | ✅ |
| Catalog | Item detail overlay | ✅ v0.8.4 |
| Packages | Install (with dependency resolution) | ✅ |
| Packages | Dependency selection in wizard | ✅ v0.8.5 |
| Packages | Uninstall | ✅ |
| Packages | Custom install wizard (file + URL) | ✅ |
| Packages | Launch / suspend / terminate | ✅ |
| Tools | Process list + kill | ✅ |
| Tools | Network info | ✅ |
| Tools | System info | ✅ |
| Tools | Crash dump viewer | ✅ |
| Tools | Screenshot (live capture) | ✅ v0.8.4 |
| Tools | Real-time performance chart | ✅ |
| Tools | USB permission wizard (WMI + icacls) | ✅ v0.8.6 |
| UI | Xbox 360 Blades dark theme | ✅ |
| UI | Settings redesign | ✅ v0.8.4 |
| UI | Activity log viewer | ✅ |
| File Explorer | SSH/SFTP file browser | ✅ v0.8.7/v0.9.0 |
| File Explorer | Upload / download with progress | ✅ v0.8.7/v0.9.0 |
| File Explorer | Drive mounting via `mklink` | ✅ v0.8.7 |
| File Explorer | Delete / create folder | ✅ v0.9.0 |
| File Explorer | Dual-pane tree + list | ✅ v0.9.0 |
| File Explorer | File-type icons | ✅ v0.9.0 |
| File Explorer | Toolbar status block | ✅ v0.9.0 |
| CI | Windows + Ubuntu + macOS build matrix | ✅ |
| CI | Linux release artifact | ✅ |
| CI | macOS release artifact | ✅ v0.8.6 |
| Stability | TitleGradient resource | ✅ v0.9.1 |
| Stability | WindowClose button unified | ✅ v0.9.1 |
| Stability | Magic delays → named constants | ✅ v0.9.1 |
| Stability | Deleted _Backup directory | ✅ v0.9.1 |
| Stability | SFTP performance (60+ MB/s) | ✅ v0.9.1 |
| Stability | CatalogApiService DI | ✅ v0.9.2 |
| Stability | WINDOWS_BUILD conditional guard | ✅ v0.9.2 |
| Stability | Silent catches → logged | ✅ v0.9.2 |
| Stability | TreeView chevron offset fix | ✅ v0.9.4 |
| Stability | Duplicate pointer handler cleanup | ✅ v0.9.4 |
| Stability | CI VirusTotal integration | ✅ v0.9.4 |

---

## What's Next

### Planned Timeline

```mermaid
gantt
    title Road to v1.0
    dateFormat  YYYY-MM-DD
    section v0.9.x → v1.0.0 — Stabilization
    Split XboxDeviceService     : 2026-07, 14d
    async void fix              : 2026-07, 2d
    Remaining tech debt sweep   : 2026-08, 14d
    section Beyond v1.0
    Community catalog           : 2026-09, 21d
    Enhanced version checker    : 2026-09, 5d
    Storage analyzer            : 2026-10, 10d
```

### v0.9.x → v1.0.0 — Stabilization

The remaining road to **v1.0.0** is dedicated to **bugfixing, refactoring, and tech debt reduction** — no major new features, just hardening toward a stable 1.0.

| Item | Status | Description |
|------|--------|-------------|
| **Split XboxDeviceService** | 🔴 Remaining | Break the 1200-line god class into `XboxPackageService`, `XboxProcessService`, `XboxSystemService`, `XboxNetworkService`, `XboxPerformanceService` |
| **Remove `async void`** | 🟡 Remaining | Fix fire-and-forget event handlers that can crash the process on unhandled exceptions |
| **Remaining tech debt** | 🟡 5 items | TD #3 (composition root), #7 (IDisposable), #8 (Border corner clip), #13 (CTS dispose), #16 (BrowseViewModel size) |
| TitleGradient resource | ✅ v0.9.1 | Extracted duplicated gradient into shared resource |
| WindowClose button | ✅ v0.9.1 | Unified across all windows |
| Magic delays → constants | ✅ v0.9.1 | Named constants for all magic delay values |
| SFTP performance | ✅ v0.9.1 | 32 KB loop → native UploadFile/DownloadFile, 60+ MB/s |
| CatalogApiService DI | ✅ v0.9.2 | Constructor injection instead of self-instantiation |
| WINDOWS_BUILD guard | ✅ v0.9.2 | Conditional compilation for System.Management |
| Silent catches → log | ✅ v0.9.2 | All `catch { }` now log diagnostics |
| Deleted _Backup | ✅ v0.9.1 | Removed stale backup directory |
| TreeView chevron offset | ✅ v0.9.4 | Fixed drive vs folder chevron alignment, eliminated expansion shake |
| Duplicate handler cleanup | ✅ v0.9.4 | Consolidated tunnel/bubble double-click handlers in File Explorer |
| NavigateToPath dispatcher | ✅ v0.9.4 | Removed unnecessary Dispatcher.UIThread.Post bottleneck |
| VirusTotal CI integration | ✅ v0.9.4 | Automatic artifact scanning on release |

### v1.0.0 — First Stable Release

Marks the completion of the stabilization pass: feature-complete, refactored, and tech-debt-reduced.

### Beyond v1.0 (v1.x+) — Ecosystem & Features

| Feature | Notes |
|---------|-------|
| Community catalog | Curated homebrew repo, click-to-install beyond Emulation Revival |
| Enhanced version checker | Compare installed vs catalog version, 1-click update all |
| Scheduled tasks | Recurring restart/shutdown/catalog refresh/backup |
| Storage analyzer | Pie chart per-app storage, temp/cache cleanup |
| System health checks | Ping latency, storage, memory overview dashboard |
| Enhanced log viewer | Real-time Xbox logs, filter, search, export to file |
| Game clip manager | Browse and download Xbox screenshots and game captures |
| Media player streaming | Play Xbox media on PC over network |
| Xbox Remote Play | Stream Xbox screen to PC |

---

## Contributing

Issues and PRs welcome on [GitHub](https://github.com/marcelofrau/xb-homebrew-vault). See [Tech Debt](tech-debt) for known issues prioritized by severity.

---

[← API Reference](api) · [Tech Debt →](tech-debt)

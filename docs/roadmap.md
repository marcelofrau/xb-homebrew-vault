---
layout: default
title: Roadmap
---

# Roadmap

## Current Status

**Latest release: v0.8.6** · [Download](https://github.com/marcelofrau/xb-homebrew-vault/releases/latest) · **v0.8.7 shipping soon**

The app is feature-complete for daily Xbox Dev Mode homebrew management. Core flows — first-run setup, browse, install, uninstall, dev tools, USB permissions — are all shipping and stable. **v0.8.7 (shipping soon) brings a functional File Explorer** (SSH/SFTP browser), replacing the former placeholder tab.

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
    v0.8.7 : Functional File Explorer (SSH/SFTP browser, upload/download, drive mounting)
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
    SSH/SFTP file browser        :done, 2026-07, 2026-07
    Upload/download with progress:done, 2026-07, 2026-07
    Drive mounting via mklink    :done, 2026-07, 2026-07
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
| File Explorer | SSH/SFTP file browser | ✅ v0.8.7 |
| File Explorer | Upload / download with progress | ✅ v0.8.7 |
| File Explorer | Drive mounting via `mklink` | ✅ v0.8.7 |
| CI | Windows + Ubuntu + macOS build matrix | ✅ |
| CI | Linux release artifact | ✅ |
| CI | macOS release artifact | ✅ v0.8.6 |

---

## What's Next

### Planned Timeline

```mermaid
gantt
    title Road to v1.0
    dateFormat  YYYY-MM-DD
    section v0.9.0 — File Explorer
    Context operations (rename/delete) : 2026-07, 7d
    Edge cases & error handling        : 2026-07, 10d
    UX polish & keyboard navigation    : 2026-08, 7d
    section v0.9.x → v1.0.0 — Stabilization
    Split XboxDeviceService     : 2026-08, 14d
    Theme gradient as resource  : 2026-08, 3d
    ConfigureAwait pass         : 2026-08, 3d
    async void fix              : 2026-08, 2d
    Bugfix & refactor sweep     : 2026-09, 21d
    section Beyond v1.0
    Community catalog           : 2026-10, 21d
    Enhanced version checker    : 2026-10, 5d
    Storage analyzer            : 2026-11, 10d
```

### v0.9.0 — File Explorer Consolidation

The functional File Explorer ships in **v0.8.7** (SSH/SFTP browse, upload/download, drive mounting via SSH.NET on port 22 — same credentials as WDP, no companion app). **v0.9.0** is the milestone that marks it complete and polished.

| Item | Description |
|------|-------------|
| Context operations | Rename, delete, new folder from the context menu |
| Error handling | Graceful handling of permission errors, disconnects, large directories |
| UX polish | Keyboard navigation, breadcrumb path bar, drag-and-drop refinements |
| Performance | Lazy-load + virtualization for directories with many entries |

### v0.9.x → v1.0.0 — Stabilization

The road from v0.9.0 to **v1.0.0** is dedicated to **bugfixing, refactoring, and tech debt reduction** — no major new features, just hardening toward a stable 1.0.

| Item | Description |
|------|-------------|
| **Split XboxDeviceService** | Break the 1038-line god class into `XboxPackageService`, `XboxProcessService`, `XboxSystemService`, `XboxNetworkService`, `XboxPerformanceService` |
| **Theme resources** | Extract duplicated title bar gradient + close button into shared `StaticResource` / UserControl (currently copy-pasted in 14+ windows) |
| **`ConfigureAwait(false)` audit** | Add to all service-layer `await` calls |
| **Remove `async void`** | Fix fire-and-forget event handlers that can crash the process on unhandled exceptions |
| **Bugfix & refactor sweep** | Address open issues, reduce duplication, tighten error handling across the app |

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

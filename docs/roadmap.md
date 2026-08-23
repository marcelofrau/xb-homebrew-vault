---
layout: default
title: Roadmap
---

# Roadmap

## Current Status

**Current source version: v1.4.0** · [Download latest release](https://github.com/marcelofrau/xb-homebrew-vault/releases/latest)

The app is feature-complete for daily Xbox Dev Mode homebrew management. Core flows — first-run setup, browse, install, uninstall, dev tools, USB permissions, Inspector — are all shipping and stable. **v1.0.0** marked the stabilization milestone with catalog overlay, multi-strategy package matching, and download flyout. **v1.0.1** added pre-flight checks, CLI parameters, and package manager fixes. **v1.1.0** shipped XRay/Inspector integration (TCP agent discovery, Lua REPL, live log streaming), keyboard shortcuts, performance tuning (Skia GPU cache, dirty-rect clipping), and comprehensive custom install wizard fixes. **v1.1.1** added custom install UX polish, single-instance mutex, and shortcut/view tweaks. **v1.2.0** shipped the auto-update checker (GitHub release comparison, NEW/UPDATE catalog badges, outdated-cache detection, update-flow fixes) plus a `linux-arm64` build-matrix entry. **v1.2.1–v1.3.1** shipped the User Files portal browser, X-Files enablement + Loopback Exempt wizards, UI scale, UI scale fixes, SFTP buffering/read-path performance rewrite (SSH.NET 2025.1), transfer diagnostics, and Window titles. **.NET 10 migration** (Aug 2026) moved the app from `net8.0` to `net10.0` — CI, tooling, and docs all updated; release builds stay self-contained.

**Next: Android enablement + hardening.** The roadmap past v1.4 is focused on Android frontend validation, composition-root cleanup, platform adapters, service-layer tests, and targeted tech-debt reduction — see [Tech Debt](tech-debt), [Developer Architecture Guide](developer-architecture), and [Testing Infrastructure](ideas/testing-infrastructure).

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
    v1.0.0 : Installed card overhaul, catalog overlay, multi-strategy matching, download flyout
    v1.0.1 : Pre-flight checks, CLI params, helper scripts, package manager fix
    v1.1.0 : XRay Inspector (TCP agent, Lua REPL, log streaming), keyboard shortcuts, performance tuning, custom install fixes
    v1.1.1 : Custom install UX, single-instance mutex, shortcut/view tweaks
    v1.2.0 : Auto-update checker, NEW/UPDATE badges, outdated cache, update-flow fixes, linux-arm64 RID
    v1.2.1 : XboxDeviceService split finalized, FileExplorerViewModel split, test infrastructure (172 green), About polish
    v1.3.0 : User Files portal browser, X-Files enablement wizard, Loopback Exempt manager, UI scale to fit screen
    v1.3.1 : SFTP transfer performance rewrite (SSH.NET 2025.1), buffering up to 1 MB, transfer diagnostics, window titles
    v1.3.2 : .NET 10 migration (net8.0 → net10.0), CI/tooling/docs updated
    v1.4.0 : Static-analysis cleanup, nullable context sweep, service docs, window icons, Android planning
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
| v1.0 stabilization | v1.0.0 | Installed card overhaul, catalog overlay, multi-strategy matching, download flyout, disabled icon set, file drop dialog |
| Pre-flight & CLI | v1.0.1 | Pre-flight checks, CLI parameters, helper scripts, package manager state fix |
| XRay Inspector | v1.1.0 | TCP agent discovery (ports 9000–9009), Lua REPL with AvaloniaEdit, live log streaming, keyboard shortcuts, performance tuning, custom install fixes |
| Custom install UX | v1.1.1 | Custom install polish, single-instance mutex, shortcut/view tweaks |
| Auto-update | v1.2.0 | GitHub release auto-update checker, NEW/UPDATE catalog badges, outdated-cache detection, update-flow fixes, linux-arm64 RID |
| Hardening | v1.2.1 | XboxDeviceService split finalized, FileExplorerViewModel split, test infrastructure (172 green), About polish |
| Portal & wizards | v1.3.0 | User Files portal browser, X-Files enablement wizard, Loopback Exempt manager, UI scale to fit screen |
| SFTP performance | v1.3.1 | SSH.NET 2025.1 read-path rewrite, up-to-1 MB buffers, transfer diagnostics, window titles |

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
    section v1.0 Stabilization
    Installed card overhaul      :done, 2026-07, 2026-07
    Catalog overlay              :done, 2026-07, 2026-07
    Multi-strategy matching      :done, 2026-07, 2026-07
    Download flyout              :done, 2026-07, 2026-07
    Pre-flight checks            :done, 2026-07, 2026-07
    CLI parameters               :done, 2026-07, 2026-07
    section v1.1 Inspector
    XRay Inspector               :done, 2026-07, 2026-07
    Lua REPL                     :done, 2026-07, 2026-07
    Live log streaming           :done, 2026-07, 2026-07
    Keyboard shortcuts           :done, 2026-07, 2026-07
    Performance tuning           :done, 2026-07, 2026-07
    Custom install fixes         :done, 2026-07, 2026-07
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
| Tools | XRay Inspector (TCP agent discovery) | ✅ v1.1.0 |
| Tools | Lua REPL (AvaloniaEdit console) | ✅ v1.1.0 |
| Tools | Live log streaming | ✅ v1.1.0 |
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
| Stability | Test infrastructure (xUnit, 172 green) | ✅ v1.2.1 |
| Stability | .NET 10 migration | ✅ v1.3.2 |
| File Explorer | User Files portal browser (REST) | ✅ v1.3.0 |
| File Explorer | Portal rename / delete / new folder | ✅ v1.3.0 |
| Tools | X-Files enablement wizard | ✅ v1.3.0 |
| Tools | Loopback Exempt manager | ✅ v1.3.0 |
| UI | UI scale to fit screen (80–120%) | ✅ v1.3.0 |

---

## What's Next

### Planned Timeline

```mermaid
gantt
    title Road to v1.3 — Hardening
    dateFormat  YYYY-MM-DD
    section Test infrastructure
    xUnit test project + CI step     :done, 2026-08, 5d
    Service layer tests (cache, crypto, catalog, override, install classify) :done, 2026-08, 10d
    section Tech debt sweep
    Split FileExplorerViewModel     :done, 2026-08, 14d
    Split XboxDeviceService         :done, 2026-08, 14d
    .NET 10 migration               :done, 2026-08, 3d
    async void fix                  : 2026-08, 3d
    ConfigureAwait(false) sweep     : 2026-08, 2d
    section Beyond v1.3
    Community catalog               : 2026-09, 21d
    Enhanced version checker        : 2026-09, 5d
    Storage analyzer                : 2026-10, 10d
```

### v1.4.x — Android Enablement & Hardening

The road past **v1.4.0** is dedicated to **Android enablement, platform adapters, and safer refactors**. Desktop remains the reference implementation; Android should reuse the service and ViewModel contracts wherever possible.

| Item | Status | Description |
|------|--------|-------------|
| **Test infrastructure** | ✅ Shipped | 240 tests passing under `tests/XBVault.Tests` |
| **Static-analysis cleanup** | ✅ Shipped | Desktop app builds with 0 warnings / 0 errors; nullable context sweep completed |
| **Window icon consistency** | ✅ Shipped | All desktop `Window` roots use the shared app icon, including splash and setup wizard |
| **Developer architecture docs** | ✅ Shipped | Shared service contracts, ViewModel boundaries, threading rules, and Android reuse guidance documented |
| **Android project skeleton** | ✅ Buildable | `XBVault.Android` builds in Release for `net10.0-android36.0/android-arm64` when `JAVA_HOME` points to JDK 21 |
| **Remove `async void`** | 🟡 Planned | 10 remaining event handlers that should route through safe `FireAndForget` wrappers |
| **ConfigureAwait(false) sweep** | 🟡 Planned | 9 uses exist; service-layer I/O policy still incomplete |
| **DI / CompositionRoot** | 🟡 Planned | `App.axaml.cs` remains 847 lines with manual service/ViewModel construction |
| **Platform adapters** | 🟡 Planned | Dialogs, pickers, clipboard, navigation, and Android-specific lifecycle need explicit abstractions |
| **Remaining tech debt** | 🟡 Active | Full list in [Tech Debt](tech-debt) |

### v1.0.0 — First Stable Release ✅

Shipped. Feature-complete, refactored, and tech-debt-reduced. See [CHANGELOG](https://github.com/marcelofrau/xb-homebrew-vault/blob/main/CHANGELOG.md) for details.

### Beyond v1.2 (v1.x+) — Ecosystem & Features

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

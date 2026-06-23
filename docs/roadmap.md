---
layout: default
title: Roadmap
---

# Roadmap

## Current Status

**Latest release: v0.8.6** · [Download](https://github.com/marcelofrau/xb-homebrew-vault/releases/latest)

The app is feature-complete for daily Xbox Dev Mode homebrew management. Core flows — first-run setup, browse, install, uninstall, dev tools, USB permissions — are all shipping and stable. File Explorer tab exists as a placeholder pending the SSH/SFTP implementation.

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
| File Explorer | SSH/SFTP browser | ⏳ in progress |
| CI | Windows + Ubuntu build matrix | ✅ |
| CI | Linux release artifact | ✅ |

---

## What's Next

### Planned Timeline

```mermaid
gantt
    title Ideas for v0.9 / v1.0+
    dateFormat  YYYY-MM-DD
    section File Explorer
    SSH/SFTP file browser        : 2026-07, 21d
    Mount drives via mklink      : 2026-07, 7d
    Upload/download with progress: 2026-07, 7d
    section Tech debt
    Extract XboxDeviceService    : 2026-08, 14d
    Theme gradient as resource   : 2026-08, 3d
    ConfigureAwait pass          : 2026-08, 3d
    async void fix               : 2026-08, 2d
    section Ecosystem
    Community catalog            : 2026-09, 21d
    Enhanced version checker     : 2026-09, 5d
    Scheduled tasks              : 2026-09, 10d
    section Features
    Storage analyzer             : 2026-09, 10d
    System health checks         : 2026-09, 7d
    Enhanced log viewer          : 2026-09, 5d
    Game clip/screenshot manager : 2026-10, 14d
    section Cross-platform
    macOS build + CI             : 2026-10, 14d
```

### Next — File Explorer (SSH/SFTP)

The File Explorer tab is a placeholder today. The planned implementation uses SSH.NET to connect on port 22 (same credentials as WDP) — no companion app required.

| Feature | Description |
|---------|-------------|
| SSH/SFTP browser | TreeView + file list, lazy-load on expand |
| Mount drives | `mklink /J` via SSH shell to expose `C:\`, `D:\`, `E:\` through SFTP |
| Upload | Drag-and-drop + file picker, progress per file |
| Download | Right-click → download to PC |
| Operations | Delete, rename from context menu |

### v0.9 — Tech Debt & Polish

| Item | Description |
|------|-------------|
| **Split XboxDeviceService** | Break the 1038-line god class into `XboxPackageService`, `XboxProcessService`, `XboxSystemService`, `XboxNetworkService`, `XboxPerformanceService` |
| **Theme resources** | Extract duplicated title bar gradient + close button into shared `StaticResource` / UserControl (currently copy-pasted in 14+ windows) |
| **`ConfigureAwait(false)` audit** | Add to all service-layer `await` calls |
| **Remove `async void`** | Fix fire-and-forget event handlers that can crash the process on unhandled exceptions |

### v1.0 — Ecosystem

| Feature | Description |
|---------|-------------|
| Community catalog | Curated homebrew repo, click-to-install beyond Emulation Revival |
| Enhanced version checker | Compare installed vs catalog version, 1-click update all |
| Scheduled tasks | Recurring restart/shutdown/catalog refresh/backup |
| Storage analyzer | Pie chart per-app storage, temp/cache cleanup |
| System health checks | Ping latency, storage, memory overview dashboard |
| Enhanced log viewer | Real-time Xbox logs, filter, search, export to file |
| Game clip manager | Browse and download Xbox screenshots and game captures |

### Future (v1.x+)

| Feature | Notes |
|---------|-------|
| **macOS builds** | Avalonia supports it; `DllImport` in Logger + WMI in UsbDriveDetector need guards |
| **Media player streaming** | Play Xbox media on PC over network |
| **Xbox Remote Play** | Stream Xbox screen to PC |

---

## Contributing

Issues and PRs welcome on [GitHub](https://github.com/marcelofrau/xb-homebrew-vault). See [Tech Debt](tech-debt) for known issues prioritized by severity.

---

[← API Reference](api) · [Tech Debt →](tech-debt)

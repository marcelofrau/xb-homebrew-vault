# Changelog

All notable changes to XBVault are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.9.6] — 2026-06-30

### Added

- **Drag & drop package install** — drop `.appx` / `.msix` / `.appxbundle` / `.zip` onto Browse or Installed grids → CustomInstall window opens with file pre-loaded and auto-analyzes
- **Visual drop overlay** — blue highlight with icon + "Drop package to install" text on valid drag-over
- **Architecture filter** — `FilterByArchitecture()` discards packages not matching target console architecture from catalog results
- **Retry loop** — 5 attempts on HTTP 409/503 during package upload (handles wired/WiFi flakiness)
- **InstalledView auto-refresh** — switches to Installed tab triggers `RefreshPackagesCommand`, newly installed packages appear immediately
- **Unsupported file dialog** — `ErrorDialog` warns when dropped file is not a supported package format
- **Screenshot save status** — green background + checkmark icon on success, red on failure, blue on info
- **Tab-aware polling** — `GET /api/resourcemanager/processes` only runs while Installed tab is visible (not in background)

### Fixed

- **Drag-drop flickering** — overlay registered on stable `Panel` wrapper, not on ListBox; `IsHitTestVisible="False"` on overlay prevents DragLeave/DragOver loop
- **Package manager state polling** — handles HTTP 204 NoContent as idle state (Xbox never returns 404), parses 200+JSON `Success:true` as completion signal, logs unexpected status codes at Warn
- **ScreenshotWindow status colors** — was hardcoded to Danger (red) for all messages, now dynamically green/red/blue per severity
- **SHA256 checksums + VirusTotal links** — release body appends instead of overwriting previous results
- **JSON parse errors** — logged properly instead of silent catch

### Assets

- `Assets/Icons/droppackage-64.png` — new icon for drag-drop overlay (Lanczos resize)

---

## [0.9.5] — 2026-06-28

### Added

- **Animation System (Stages 1–6)** — page transitions, sidebar hover glow, brand pulse, connect pulse, dialog fade behavior, status bar animations
- **File Explorer refinements** — cursor states, path guard, go expand, layout adjustments, new file type icons, ViewModel null guards
- **CI/CD** — Cloudflare Pages docs deployment, SEO (og:image, sitemap, Search Console)
- **Developer tooling** — build scripts for artifact management, `.editorconfig`, `.vscode/` configs

### Removed

- `Models/MemeLines.cs` — dead code (156 lines)

---

## [0.9.4] — 2026-06-26

### Added

- File Explorer CRUD — upload, download, rename, delete, mkdir with SFTP
- Sidebar navigation — Browse, Installed tabs with active state indicators
- Package installation with progress reporting and cancellation
- Screenshot capture (single + live periodic)
- System info viewer
- Process list with running/suspended state
- Network info viewer
- Performance monitoring (CPU, memory, GPU, temp)
- Xbox restart/shutdown from Tools view
- First-run setup wizard
- USB media drive activation (Windows)
- Crash data viewer
- Custom install with file picker
- Settings persistence with auto-hide success notification
- Log viewer with level filtering
- i18n foundation — Portuguese + Spanish translations, language switcher
- GitHub Actions CI — multi-platform build (.NET 8, Avalonia 12)
- Dev Portal shortcut
- Splash screen with minimum 2s delay

---

## [0.9.0] — 2026-06-23

### Added

- Initial public release
- Xbox connection management (WMI discovery + manual config)
- Package browsing from remote catalog
- Package install/uninstall
- Basic file upload via Xbox REST API
- Debug logging infrastructure

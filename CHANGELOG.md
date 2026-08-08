# Changelog

All notable changes to XBVault are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.3.1] — 2026-08-08

### Fixed

- **File navigation races** — rapid navigation (quick folder clicks, fast tree expansion) could start overlapping SFTP listings that raced and returned wrong or empty trees/lists. In-flight navigations are now cancelled, re-navigation to the same path is skipped, and tree expansion honours cancellation.
- **Slow folder / ZIP uploads** — remote directories are now created in a single batched pass (deduplicated, parent-first) instead of one `mkdir` round trip per file, eliminating dozens/hundreds of shell commands on large trees.
- **Slow uploads / downloads (buffering)** — the SFTP transfer buffer now scales up to 1 MB for files > 1 GB (previous cap 512 KB); download size probing no longer performs a redundant double stat.
- **Slow downloads (SSH.NET read path)** — upgraded SSH.NET from 2024.2.0 to 2025.1.0, which overhauls the SFTP read path: read-ahead is now built into `SftpFileStream` (previously sequential reads with a single ~32 KB request in flight at a time, so round-trip latency dominated), plus array-backed SFTP packet buffers and `ArraySegment` channel data. Upstream measured 3–20× faster stream copies on high-latency links.
- **Unnecessary shell round trips on upload** — `CreateDirectoryAsync` now verifies the directory actually exists before falling back to the shell `mkdir`, avoiding a shell command when the folder is already present.
- **Shell command timeouts now cancellable** — a user cancel is distinguishable from a timeout, and cancelled commands stop promptly instead of waiting out the full timeout.

### Changed

- **Transfer diagnostics** — a transfer sampler logs instantaneous speed every 2 s, warns when a transfer stalls (no data for 5 s), and logs a per-file summary (average/peak speed). Batch transfers log an overall summary and, on failure, the failing file, progress, and elapsed time — making slow transfers much easier to diagnose.
- **Window titles** — all windows and dialogs now use `XBVault - ...` titles (dynamic for error/input/confirm dialogs), which also lets OBS Window Capture target dialogs individually.

---

## [1.3.0] — 2026-08-07

### Added

- **User Files portal browser** — File Explorer browses app `LocalAppData` / `DevelopmentFiles` via the Dev Portal REST API (read-only `User Files:\` tree root), with recursive listing and per-file / multi-file download
- **Portal folder creation** — "New Folder" works inside an app's `LocalAppData` through the portal API
- **Portal rename / delete** — rename and delete entries inside an app's `LocalAppData` / `DevelopmentFiles` through the portal API (folders created through the portal are now fully manageable)
- **X-Files Enablement wizard** — one-click tool that auto-detects the X-Files UWP app and applies its loopback exemption (via `checknetisolation` over SSH), so the app can reach the console's own Dev Portal REST API
- **Loopback Exempt Manager wizard** — apply or remove the loopback exemption for any installed app and check its current status, with post-command verification
- **Portal loading overlay** — dim + spinner overlay over the file tree and list while portal REST listings are in progress (replaces the stale "Loading..." tree placeholder)
- **UI scale to fit screen** — main window content auto-shrinks to fit the display work area (fixes clipping on HiDPI / high display-scaling setups), plus a UI scale setting (80–120%) to tune interface size

### Changed

- **Tools page layout** — X-Files Enablement and Loopback Exempt Manager live under XBOX ACTIONS (moved below EXTERNAL MEDIA TOOLS); the four XBOX ACTIONS buttons now fit on a single row
- **X-Files detection** — matches `X-Files` / `XFiles.Xbox` package family name (normalizes hyphens/spaces/dots) instead of exact-name substring
- **Quick wizard opens on step 2** — X-Files Enablement skips the Overview step; detection result shows only after the package list loads (no red "not found" flash)
- **Wizard icon** — the X-Files step header now uses a folder icon instead of a game-pad icon
- **Wizard run verification** — a successful `checknetisolation` exit code is treated as success; the `-s` post-check is best-effort because the console SSH shell may not echo its output
- **Logs moved into Settings** — removed the Logs sidebar tab; the live log console is now opened from a "Logs screen" button in Settings (Ctrl+Tab cycling still reaches it)
- **Compact sidebar** — reduced nav item padding/font size so all tabs fit on shorter displays
- **Larger default window** — default main window height raised from 860 to 1000 for more content room on large screens

### Fixed

- **Wizard step overlap** — quick (X-Files) and full (app selection) panels shared the same visibility binding and rendered stacked; each mode now shows only its own panel
- **Wizard nav buttons delayed** — Back / Next / Cancel are now always visible and only disabled while busy, instead of appearing after the package load finishes
- **Tab switching from Settings** — GoToLogs no longer bounces back to Browse; sidebar selection is now one-way with explicit per-item switching

---

## [1.2.0] — 2026-07-28

### Added

- **Auto-update checker** — compares installed version against latest GitHub release, surfaces update availability
- **NEW / UPDATE catalog badges** — catalog items marked as new or as updates to installed packages
- **Outdated-cache detection** — flags and refreshes stale catalog caches
- **linux-arm64 build-matrix entry** — new release RID (`linux-arm64`)

### Fixed

- **Update flow fixes** — corrected version comparison and update-request handling
- **Cancel connection dialog no longer shows error popup** (#4) — cancelling the connection flow now exits cleanly

---

## [1.1.1] — 2026-07-19

### Added

- **Custom install UX polish** — improved custom install wizard interaction and shortcuts
- **Single-instance mutex** — launching a second XBVault instance activates the existing window instead of starting a duplicate

### Fixed

- **x64 target-arch package filter** — package filter uses x64 as the target architecture (fixes #2)
- **"Higher version" install errors** — skip install attempts when the installed version is already newer; added issue templates

### Changed

- **CI release verification** — robust filename extraction (`unzip -Z1`), trailing-space trim in verify step

---

## [1.1.0] — 2026-07-12

### Added

- **XRay / Inspector integration** — TCP agent discovery on ports 9000–9009, real-time Xbox log streaming, Lua REPL with command history and output formatting
- **Keyboard shortcuts** — Escape to close dialogs/windows, Ctrl+Enter for quick actions
- **AvaloniaEdit console** — syntax-highlighted Lua REPL in Inspector view with `FiraCode Nerd Font`
- **Inspector guide docs** — comprehensive developer pitch page: what XRay is, advantages over ad-hoc tools, connection guide
- **Window maximize/restore button** — toggle between maximized and restored window state from title bar
- **Filter overlay** — catalog filter panel with improved layout and UX
- **Custom install wizard logging** — Trace/Debug/Info/Warn/Error logging across `CustomInstallViewModel`, `PackageInstallService`, and `XboxDeviceService` for diagnosability

### Fixed

- **File lock on custom install** — ZIP extraction kept file handle open during `ZipFile.ExtractToDirectory`; switched to explicit `using` block to close before analysis
- **WaitForPackageManagerReady infinite loop** — error `-2146762496` (0x800B0100, TRUST_E_NOSIGNATURE) not recognized as "idle" state; added `IsSignatureError()` helper to break the loop after 120s timeout
- **Input validation in custom install** — `AnalyzeAsync` now validates `SourcePath`/`SourceUrl` before use; empty or whitespace-only input shows error message instead of proceeding
- **BladesTheme CheckBox/RadioButton resource keys** — corrected Avalonia Fluent resource keys (`RadioButtonOuterEllipseStroke*`, `CheckBoxCheckBackgroundStroke*`, `*Foreground*`); stroke colors corrected to `#8B8D91` normal, `#B5E665` on hover
- **Item detail error display** — replaced red error card with plain text below "Install failed" message

### Changed

- **Performance tuning** — Skia GPU cache increased, dirty-rect clipping enabled for reduced CPU usage
- **Tab transitions** — smoother cross-fade between Browse/Installed/Tools/Logs tabs
- **Disabled icon set** — visual feedback for inactive/disabled UI elements
- **Inspector console polish** — improved log streaming display and REPL output formatting

---

## [1.0.1] — 2026-07-05

### Added

- **Pre-flight checks** — validates Xbox connection before install/uninstall operations
- **CLI parameters** — command-line flags for headless or scripted usage
- **Helper scripts** — convenience scripts for common development tasks

### Fixed

- **Package manager state polling** — improved detection of install/uninstall completion states

---

## [1.0.0] — 2026-07-01

### Added

- **Installed card overhaul** — redesigned package cards with status indicators and action buttons
- **Catalog overlay** — detailed overlay view for catalog items with install/dependency info
- **Multi-strategy package matching** — improved matching of installed packages to catalog entries using name, version, and family
- **Download flyout** — progress flyout during package download with speed and ETA
- **Disabled icon set** — visual treatment for inactive/disabled toolbar buttons
- **File drop dialog** — enhanced drag-and-drop with file type validation and unsupported format warnings

### Changed

- **Post-1.0 stabilization complete** — feature-complete, refactored, tech-debt-reduced for stable release

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

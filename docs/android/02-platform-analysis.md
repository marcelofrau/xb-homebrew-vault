---
layout: default
title: Platform Analysis
---

# Platform Analysis — Codebase Audit

Full audit of every file in the XBVault project, classified by Android readiness.

## Summary

| Category | Count | Percentage |
|----------|-------|------------|
| Fully compatible | 18 services, 9 UserControls, 24 ViewModels | ~70% |
| Needs minor changes | 12 services, 21 Window dialogs | ~25% |
| Needs major changes | 1 service (PlatformDialog) | ~3% |
| Not applicable on mobile | 2 (UsbDriveDetector, AutostartService) | ~2% |

**No blocking issues found.** Every ViewModel and most services work on Android without modification.

---

## Views — Classification by Type

### UserControls (9) — Main Tab Views + Panels

These are embedded in the main content area and are the primary views.

| View | File | Desktop | Mobile Adaptation |
|------|------|---------|-------------------|
| BrowseView | `Views/BrowseView.axaml` | UserControl | Responsive grid → stacked cards on mobile |
| InstalledView | `Views/InstalledView.axaml` | UserControl | List already works; adjust card widths |
| FileExplorerView | `Views/FileExplorerView.axaml` | UserControl | TreeView → simplified list; touch targets |
| ToolsView | `Views/ToolsView.axaml` | UserControl | Button grid → vertical list on mobile |
| InspectorView | `Views/InspectorView.axaml` | UserControl | Already scrollable; minor padding adjustments |
| SettingsView | `Views/SettingsView.axaml` | UserControl | Already scrollable; works as-is |
| LogsView | `Views/LogsView.axaml` | UserControl | Uses AvaloniaEdit — may need fallback for mobile |
| TasksPanel | `Views/TasksPanel.axaml` | UserControl | Popup-embedded; needs mobile alternative |
| NotificationsPanel | `Views/NotificationsPanel.axaml` | UserControl | Popup-embedded; needs mobile alternative |

### Windows (21) — Modal Dialogs

All dialogs inherit from `Window` and use `ShowDialog()`. On Android, each must be converted to a fullscreen page or bottom sheet.

| Dialog | Purpose | Mobile Complexity |
|--------|---------|-------------------|
| ConnectionWindow | Xbox connection setup wizard | Complex — multi-step form |
| SetupWizardWindow | First-run setup | Complex — multi-step wizard |
| ItemDetailWindow | Homebrew item details | Medium — scrollable content |
| CustomInstallWindow | Custom package install | Medium — form inputs |
| AboutWindow | App info and credits | Simple — static content |
| ConfirmWindow | Generic confirmation | Simple — two buttons |
| DeleteConfirmWindow | Delete confirmation | Simple — two buttons |
| InputDialog | Text input prompt | Simple — text field + buttons |
| ErrorDialog | Error display | Simple — text + close |
| ScreenshotWindow | Xbox screenshot viewer | Medium — image display |
| SystemInfoWindow | Xbox system info | Medium — data grid |
| ProcessesWindow | Running processes | Medium — list with actions |
| NetworkInfoWindow | Network configuration | Medium — data display |
| PerformanceWindow | CPU/memory charts | Complex — real-time charts |
| CrashDataWindow | Crash dump viewer | Medium — file list |
| RefreshWindow | System refresh status | Simple — progress indicator |
| UsbPermissionWindow | USB drive permissions | Not applicable — Windows-only |
| LoopbackExemptWindow | Loopback exemption | Not applicable — Windows-only |
| SftpInfoWindow | SFTP connection info | Simple — text display |
| DiscordPopup | Discord link | Simple — single action |

---

## ViewModels — All Cross-Platform

All 24 ViewModels use `CommunityToolkit.Mvvm` with `[ObservableProperty]` and `[RelayCommand]`. None reference platform-specific APIs.

| ViewModel | File | Lines | Android Notes |
|-----------|------|-------|---------------|
| MainViewModel | `ViewModels/MainViewModel.cs` | — | Navigation index mapping |
| BrowseViewModel | `ViewModels/BrowseViewModel.cs` | — | HTTP catalog fetch — works |
| InstalledViewModel | `ViewModels/InstalledViewModel.cs` | — | HTTP package list — works |
| FileExplorerViewModel | `ViewModels/FileExplorerViewModel.cs` | — | SSH/SFTP — works; Windows-only tool launchers guarded |
| ToolsViewModel | `ViewModels/ToolsViewModel.cs` | — | Windows-only features already gated |
| SettingsViewModel | `ViewModels/SettingsViewModel.cs` | — | SettingsService — works |
| InspectorViewModel | `ViewModels/InspectorViewModel.cs` | — | HTTP status — works |
| LogsViewModel | `ViewModels/LogsViewModel.cs` | — | Log display — works |
| ConnectionViewModel | `ViewModels/ConnectionViewModel.cs` | — | HTTP auth — works |
| TaskCenterViewModel | `ViewModels/TaskCenterViewModel.cs` | — | Background tasks — works |
| ConfirmViewModel | `ViewModels/ConfirmViewModel.cs` | — | Generic — works |
| DeleteConfirmViewModel | `ViewModels/DeleteConfirmViewModel.cs` | — | Generic — works |
| DiscordPopupViewModel | `ViewModels/DiscordPopupViewModel.cs` | — | Generic — works |
| CustomInstallViewModel | `ViewModels/CustomInstallViewModel.cs` | — | HTTP install — works |
| CrashDataViewModel | `ViewModels/CrashDataViewModel.cs` | — | HTTP fetch — works |
| LoopbackExemptViewModel | `ViewModels/LoopbackExemptViewModel.cs` | — | Windows-only — skip on mobile |
| NetworkInfoViewModel | `ViewModels/NetworkInfoViewModel.cs` | — | HTTP fetch — works |
| PerformanceViewModel | `ViewModels/PerformanceViewModel.cs` | — | WebSocket — works |
| ProcessesViewModel | `ViewModels/ProcessesViewModel.cs` | — | HTTP fetch — works |
| RefreshViewModel | `ViewModels/RefreshViewModel.cs` | — | HTTP actions — works |
| ScreenshotViewModel | `ViewModels/ScreenshotViewModel.cs` | — | HTTP fetch — works |
| SetupWizardViewModel | `ViewModels/SetupWizardViewModel.cs` | — | HTTP auth — works |
| SystemInfoViewModel | `ViewModels/SystemInfoViewModel.cs` | — | HTTP fetch — works |
| UsbPermissionViewModel | `ViewModels/UsbPermissionViewModel.cs` | — | Windows-only — skip on mobile |

---

## Services — Detailed Classification

### Fully Compatible (18)

No changes needed. Pure HTTP, JSON, or BCL operations.

| Service | Lines | What It Does |
|---------|-------|--------------|
| InspectorConsoleColorizer | 37 | AvaloniaEdit colorizer |
| GitHubReleaseCheckerService | 72 | GitHub API version check |
| CryptoService | 47 | XOR obfuscation |
| BackgroundTaskService | 377 | Job orchestration via Avalonia Dispatcher |
| InstalledAppUpdateService | 135 | Update detection orchestration |
| NotificationCenterService | 242 | In-app toast notifications |
| PackageLauncher | 70 | Xbox app launch orchestration |
| PortalAppFilesService | 449 | Xbox Dev Portal filesystem |
| PackageOverrideService | 183 | Embedded catalog overrides |
| SerilogAdapter | 16 | Logger passthrough |
| XrayAgentService | 267 | Xbox Xray TCP agent |
| XboxSystemService | 237 | Xbox screenshots/crash dumps |
| XboxResponseParser | 169 | JSON response parsing |
| XboxProcessService | 90 | Xbox process list |
| XboxPerformanceService | 87 | Xbox perf WebSocket |
| XboxPackageService | 504 | Xbox package lifecycle |
| XboxNetworkService | 99 | Xbox network config |
| SftpTransferService | 791 | File transfer orchestration |
| VersionCheckerService | 204 | Version matching |
| ServiceLocator | 36 | DI container |

### Needs Minor Changes (12)

Work on Android but need path adjustments, semantic adaptation, or security review.

| Service | Lines | Issue | Fix |
|---------|-------|-------|-----|
| CatalogApiService | 452 | `SpecialFolder.LocalApplicationData` path | Works on Android — verify cache location |
| CacheService | 124 | `SpecialFolder.LocalApplicationData` path | Works on Android — verify cache location |
| AutostartService | 36 | Concept has no mobile equivalent | Skip or adapt to Android autostart |
| Logger | 345 | kernel32 P/Invoke guarded by `IsWindows()` | Safe — no changes needed |
| PackageInstallService | 568 | `SpecialFolder.LocalApplicationData` for temp | Works on Android |
| PreFlightChecker | 270 | Console.WriteLine in health check | Guard with platform check |
| WindowSettingsService | 43 | Window size concept is desktop-only | Skip on Android |
| XboxAuthService | 384 | Self-signed cert bypass | Review security for mobile |
| UpdateVersionCache | 90 | `SpecialFolder.LocalApplicationData` | Works on Android |
| SftpService | 706 | SSH.NET crypto libs on Android | Test — should work with .NET Android |
| SettingsService | 117 | `SpecialFolder.ApplicationData` path | Works on Android |
| VersionCheckerService | 204 | Pure logic — actually fully compatible | — |

### Needs Major Changes (1)

| Service | Lines | Issue | Fix |
|---------|-------|-------|-----|
| **PlatformDialog** | 135 | user32.dll P/Invoke, zenity/osascript all unavailable on Android | Add Android branch using Avalonia `Window.ShowDialog()` or native AlertDialog |

### Not Applicable on Mobile (2)

| Service | Lines | Why |
|---------|-------|-----|
| UsbDriveDetector | 218 | WMI is Windows-only; already returns empty on non-Windows |
| LoopbackExemptViewModel | — | Windows loopback exemption has no Android equivalent |

---

## Platform-Specific Code Inventory

### P/Invoke (3 files)

| File | DLL | Function | Guarded? | Android Impact |
|------|-----|----------|----------|----------------|
| Program.cs | user32.dll | SetForegroundWindow, ShowWindow | `RuntimeInformation.IsOSPlatform(Windows)` | None — Program.cs not used on Android |
| Logger.cs | kernel32.dll | AttachConsole, AllocConsole | `OperatingSystem.IsWindows()` | None — guarded |
| PlatformDialog.cs | user32.dll | MessageBox | `OperatingSystem.IsWindows()` | **Needs Android branch** |

### Preprocessor Directives

| File | Directive | Purpose |
|------|-----------|---------|
| UsbDriveDetector.cs | `#if WINDOWS_BUILD` | Wraps entire WMI implementation |
| XBVault.csproj | `DefineConstants` | Defines `WINDOWS_BUILD` on Windows only |

### Runtime Platform Checks

| File | Line | Check | Purpose |
|------|------|-------|---------|
| Program.cs | 102 | `RuntimeInformation.IsOSPlatform(Windows)` | Activate existing window |
| FileExplorerViewModel.cs | 355 | `RuntimeInformation.IsOSPlatform(Windows)` | `IsWindows` property |
| ToolsViewModel.cs | 45 | `RuntimeInformation.IsOSPlatform(Windows)` | `IsWindows` property |
| UsbPermissionViewModel.cs | 88 | `RuntimeInformation.IsOSPlatform(Windows)` | `IsWindows` property |
| Logger.cs | 161 | `OperatingSystem.IsWindows()` | Console attach |
| PlatformDialog.cs | 9, 20 | `OperatingSystem.IsWindows()` / `IsMacOS()` | Dialog method selection |

### Hardcoded Paths

**None found.** All file paths are constructed from `Environment.SpecialFolder` or relative to cache roots. This is excellent for portability.

---

## Environment.SpecialFolder Usage

| Service | SpecialFolder | Android Resolution |
|---------|--------------|-------------------|
| SettingsService | `ApplicationData` | `/data/data/<pkg>/files/` |
| Logger | `ApplicationData` | `/data/data/<pkg>/files/` |
| PreFlightChecker | `ApplicationData` | `/data/data/<pkg>/files/` |
| PreFlightChecker | `LocalApplicationData` | `/data/data/<pkg>/cache/` |
| CatalogApiService | `LocalApplicationData` | `/data/data/<pkg>/cache/` |
| CacheService | `LocalApplicationData` | `/data/data/<pkg>/cache/` |
| PackageInstallService | `LocalApplicationData` | `/data/data/<pkg>/cache/` |
| UpdateVersionCache | `LocalApplicationData` | `/data/data/<pkg>/cache/` |

All resolve to app-private storage on Android. **No changes needed** — the BCL handles platform differences.

---

## Threading Analysis

All threading primitives used are BCL or Avalonia-level, which work on Android:

| Pattern | Used By | Android Safe? |
|---------|---------|---------------|
| `lock` (Monitor) | BackgroundTaskService, NotificationCenterService, SettingsService | Yes |
| `SemaphoreSlim` | XrayAgentService, SftpService, XboxAuthService | Yes |
| `System.Threading.Timer` | NotificationCenterService | Yes |
| `DispatcherTimer` | BackgroundTaskService | Yes — Avalonia Dispatcher works on Android |
| `Task.Run` / async | All async services | Yes |
| `PeriodicTimer` | BackgroundTaskService | Yes — .NET 10 BCL |

**No Android-specific threading concerns.** Avalonia's `Dispatcher` runs on Android's main thread correctly.

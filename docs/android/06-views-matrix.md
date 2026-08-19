---
layout: default
title: Views Matrix
---

# Views Matrix — View-by-View Adaptation Status

Every AXAML view in the project, classified by type, mobile adaptation needs, and priority.

> **Updated 2026-08-19.** 7 mobile views created and deployed. Phase 1C complete.

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Done — mobile view created and deployed |
| 🔵 | Created — view exists but content still placeholder or needs wiring |
| ⬜ | Not applicable on mobile |
| 🔴 | Not started — needs new mobile-specific view |

---

## Android-Specific Views (Created Files)

These are **independent mobile views** in `XBVault/Views/`. They reuse ViewModels from the shared project but have their own AXAML layouts designed for mobile portrait fullscreen.

| View | File | Status | Description |
|------|------|--------|-------------|
| MobileSplashView | `Views/MobileSplashView.axaml` | ✅ Done | Portrait splash with background image, version, Oxanium fonts |
| MobileMainWindow | `Views/MobileMainWindow.axaml` | ✅ Done | Shell: gradient top bar + Carousel content + bottom tab bar |
| MobileBrowseView | `Views/MobileBrowseView.axaml` | ✅ Done | Catalog cards with images, category filter, search, CdSpinner |
| MobileDetailView | `Views/MobileDetailView.axaml` | ✅ Done | Fullscreen item detail with thumbnail, metadata, install button |
| MobileAboutView | `Views/MobileAboutView.axaml` | ✅ Done | Splash bg image, Discord flyout, gradient top bar, back header |
| MobileSettingsView | `Views/MobileSettingsView.axaml` | ✅ Done | Single-column, Save button, gradient top bar, back header |
| MobileToolsView | `Views/MobileToolsView.axaml` | 🔵 Created | 4 section cards (placeholder actions, needs real tool wiring) |

---

## Main Navigation Views (Desktop — Not reused on Android)

These are the desktop tab content views. On Android, they're replaced by independent Mobile* views.

| View | File | Desktop Features | Mobile Adaptation | Status |
|------|------|------------------|-------------------|--------|
| BrowseView | `Views/BrowseView.axaml` | Responsive grid, hover cards, search | Replaced by `MobileBrowseView` | ✅ |
| InstalledView | `Views/InstalledView.axaml` | Card list, package actions | **Not yet created** — needs MobileInstalledView | 🔴 |
| FileExplorerView | `Views/FileExplorerView.axaml` | TreeView sidebar, file list | **Not yet created** — needs MobileFilesView | 🔴 |
| ToolsView | `Views/ToolsView.axaml` | Button grid, Windows-only features | Replaced by `MobileToolsView` (placeholder) | 🔵 |
| InspectorView | `Views/InspectorView.axaml` | AvaloniaEdit console | **Excluded from Android** | ⬜ |
| SettingsView | `Views/SettingsView.axaml` | Settings form, scale controls | Replaced by `MobileSettingsView` | 🔵 |
| LogsView | `Views/LogsView.axaml` | AvaloniaEdit log viewer | **Not yet created** — needs MobileLogsView | 🔴 |

---

## Modal Dialogs (Desktop — Need conversion)

All dialogs inherit from `Window` and use `ShowDialog()`. On Android, each must be converted.

### Complex Dialogs (fullscreen page pattern)

| Dialog | File | Status | Mobile Adaptation |
|--------|------|--------|-------------------|
| ConnectionWindow | `Views/ConnectionWindow.axaml` | 🔴 Not started | Multi-step wizard → fullscreen with progress |
| SetupWizardWindow | `Views/SetupWizardWindow.axaml` | 🔴 Not started | Multi-step wizard → fullscreen with progress |
| ItemDetailWindow | `Views/ItemDetailWindow.axaml` | ✅ Done | Replaced by `MobileDetailView` |
| CustomInstallWindow | `Views/CustomInstallWindow.axaml` | 🔴 Not started | Form inputs, file picker → fullscreen page |
| PerformanceWindow | `Views/PerformanceWindow.axaml` | ⬜ Skip | Charts need touch-friendly redesign — Phase 4 |
| ScreenshotWindow | `Views/ScreenshotWindow.axaml` | ⬜ Skip | Phase 4 |
| SystemInfoWindow | `Views/SystemInfoWindow.axaml` | ⬜ Skip | Phase 4 |
| ProcessesWindow | `Views/ProcessesWindow.axaml` | ⬜ Skip | Phase 4 |
| NetworkInfoWindow | `Views/NetworkInfoWindow.axaml` | ⬜ Skip | Phase 4 |
| CrashDataWindow | `Views/CrashDataWindow.axaml` | ⬜ Skip | Phase 4 |
| RefreshWindow | `Views/RefreshWindow.axaml` | ⬜ Skip | Phase 4 |

### Simple Dialogs (bottom sheet or safe wrapper)

| Dialog | File | Status | Mobile Adaptation |
|--------|------|--------|-------------------|
| ConfirmWindow | `Views/ConfirmWindow.axaml` | 🔴 Not started | Bottom sheet with two buttons |
| DeleteConfirmWindow | `Views/DeleteConfirmWindow.axaml` | 🔴 Not started | Bottom sheet with two buttons |
| InputDialog | `Views/InputDialog.axaml` | 🔴 Not started | Bottom sheet with text field |
| ErrorDialog | `Views/ErrorDialog.axaml` | ✅ Done | Log-only on Android (no Window) |

### Inline / Hamburger Menu

| Dialog | File | Status | Mobile Adaptation |
|--------|------|--------|-------------------|
| AboutWindow | `Views/AboutWindow.axaml` | ✅ Done | Replaced by `MobileAboutView` (fullscreen overlay) |
| SftpInfoWindow | `Views/SftpInfoWindow.axaml` | ⬜ Skip | Inline card — Phase 4 |
| DiscordPopup | `Views/DiscordPopup.axaml` | ✅ Done | Inline Flyout on Discord button in `MobileAboutView` |
| UsbPermissionWindow | `Views/UsbPermissionWindow.axaml` | ⬜ Skip | Windows-only |
| LoopbackExemptWindow | `Views/LoopbackExemptWindow.axaml` | ⬜ Skip | Windows-only |

---

## Panels (Desktop — Need mobile alternatives)

| View | File | Status | Mobile Adaptation |
|------|------|--------|-------------------|
| TasksPanel | `Views/TasksPanel.axaml` | 🔴 Not started | Bottom sheet or inline in hamburger menu |
| NotificationsPanel | `Views/NotificationsPanel.axaml` | 🔴 Not started | Bottom sheet or inline in hamburger menu |

---

## Root Views

| View | File | Status | Mobile Adaptation |
|------|------|--------|-------------------|
| MainWindow | `MainWindow.axaml` | ✅ Done | Replaced by `MobileMainWindow` on Android |
| SplashWindow | `SplashWindow.axaml` | ✅ Done | Replaced by `MobileSplashView` + native pre-splash |
| App.axaml | `App.axaml` | ✅ Done | No changes needed |

---

## Summary by Phase

### Phase 1 (Mobile Shell) ✅
- MobileSplashView, MobileMainWindow created
- Pre-splash native + Avalonia splash
- Tab bar, top bar, hamburger menu

### Phase 1C (Mobile Views) ✅
- MobileBrowseView, MobileDetailView, MobileAboutView, MobileSettingsView, MobileToolsView

### Phase 2 (Core) 🔄
- ✅ BrowseView — working with cards, search, detail
- ✅ ItemDetailWindow — replaced by MobileDetailView
- ✅ ErrorDialog — log-only on Android
- 🔴 InstalledView — needs MobileInstalledView
- 🔴 ConnectionWindow — needs fullscreen page
- 🔴 Confirm/DeleteConfirm — need bottom sheets

### Phase 3 (Extended)
- 🔴 FileExplorerView — needs MobileFilesView with breadcrumbs
- 🔴 ToolsView — needs real tool actions wired
- 🔴 SettingsView — needs real settings save/load
- 🔴 LogsView — needs plain TextBlock (no AvaloniaEdit)
- 🔴 SetupWizardWindow — needs fullscreen page
- 🔴 TasksPanel, NotificationsPanel — need mobile alternatives

### Phase 4 (Polish)
- ⬜ PerformanceWindow, ScreenshotWindow, SystemInfoWindow, etc.
- ⬜ Adaptive icon fix
- ⬜ Native splash logo restoration
- ⬜ Network state handling, battery optimization

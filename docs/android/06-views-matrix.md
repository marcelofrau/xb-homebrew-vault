---
layout: default
title: Views Matrix
---

# Views Matrix — View-by-View Adaptation Status

Every AXAML view in the project, classified by type, mobile adaptation needs, and priority.

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Ready — works on mobile as-is |
| 🟡 | Minor — needs touch target / responsive tweaks |
| 🟠 | Medium — needs layout restructure or dialog conversion |
| 🔴 | Major — needs new mobile-specific view or significant rewrite |
| ⬜ | Skip — not applicable on mobile |

---

## Main Navigation Views (UserControls)

These are the primary tab content views embedded in the Carousel.

| View | File | Lines | Type | Desktop Features | Mobile Adaptation | Priority | Phase |
|------|------|-------|------|------------------|-------------------|----------|-------|
| BrowseView | `Views/BrowseView.axaml` | — | UserControl | Responsive grid, hover cards, search | 🟠 Responsive → 1-2 col cards, remove hover | P0 | Phase 2 |
| InstalledView | `Views/InstalledView.axaml` | — | UserControl | Card list, package actions | 🟡 Adjust card widths, touch targets | P0 | Phase 2 |
| FileExplorerView | `Views/FileExplorerView.axaml` | — | UserControl | TreeView sidebar, file list, context menus | 🟠 TreeView → breadcrumbs, long-press menu | P1 | Phase 3 |
| ToolsView | `Views/ToolsView.axaml` | — | UserControl | Button grid, Windows-only features | 🟡 Grid → vertical list, gate Windows features | P1 | Phase 3 |
| InspectorView | `Views/InspectorView.axaml` | — | UserControl | AvaloniaEdit console, connection details | 🟡 Test AvaloniaEdit, adjust padding | P1 | Phase 3 |
| SettingsView | `Views/SettingsView.axaml` | — | UserControl | Settings form, scale controls | 🟡 Minor padding adjustments | P1 | Phase 3 |
| LogsView | `Views/LogsView.axaml` | — | UserControl | AvaloniaEdit log viewer | 🟡 Test AvaloniaEdit, consider fallback | P2 | Phase 3 |

---

## Panels (UserControls — embedded in popups)

| View | File | Lines | Type | Desktop Features | Mobile Adaptation | Priority | Phase |
|------|------|-------|------|------------------|-------------------|----------|-------|
| TasksPanel | `Views/TasksPanel.axaml` | — | UserControl | Popup-anchored to gear icon | 🟠 Needs mobile alternative (bottom sheet or inline) | P1 | Phase 3 |
| NotificationsPanel | `Views/NotificationsPanel.axaml` | — | UserControl | Popup-anchored to bell icon | 🟠 Needs mobile alternative (bottom sheet or inline) | P1 | Phase 3 |

---

## Modal Dialogs (Windows → fullscreen pages / bottom sheets)

All dialogs inherit from `Window` and use `ShowDialog()`. On Android, each must be converted.

### Complex Dialogs (fullscreen page)

| Dialog | File | Lines | Purpose | Mobile Adaptation | Priority | Phase |
|--------|------|-------|---------|-------------------|----------|-------|
| ConnectionWindow | `Views/ConnectionWindow.axaml` | — | Xbox connection wizard (multi-step) | 🔴 Multi-step wizard → fullscreen with progress | P0 | Phase 2 |
| SetupWizardWindow | `Views/SetupWizardWindow.axaml` | — | First-run setup wizard | 🔴 Multi-step wizard → fullscreen with progress | P1 | Phase 3 |
| ItemDetailWindow | `Views/ItemDetailWindow.axaml` | — | Homebrew item details + install | 🟠 Scrollable content, action buttons | P0 | Phase 2 |
| CustomInstallWindow | `Views/CustomInstallWindow.axaml` | — | Custom package install form | 🟠 Form inputs, file picker | P2 | Phase 3 |
| PerformanceWindow | `Views/PerformanceWindow.axaml` | — | Real-time CPU/memory charts | 🔴 Charts need touch-friendly redesign | P2 | Phase 3 |
| ScreenshotWindow | `Views/ScreenshotWindow.axaml` | — | Xbox screenshot viewer | 🟠 Image display, save/share | P2 | Phase 3 |
| SystemInfoWindow | `Views/SystemInfoWindow.axaml` | — | Xbox system information | 🟡 Data grid → scrollable list | P2 | Phase 3 |
| ProcessesWindow | `Views/ProcessesWindow.axaml` | — | Running process list + kill | 🟡 List with action buttons | P2 | Phase 3 |
| NetworkInfoWindow | `Views/NetworkInfoWindow.axaml` | — | Xbox network configuration | 🟡 Data display | P2 | Phase 3 |
| CrashDataWindow | `Views/CrashDataWindow.axaml` | — | Crash dump viewer | 🟡 File list | P2 | Phase 3 |
| RefreshWindow | `Views/RefreshWindow.axaml` | — | System refresh progress | 🟢 Progress indicator — simple | P2 | Phase 3 |

### Simple Dialogs (bottom sheet)

| Dialog | File | Lines | Purpose | Mobile Adaptation | Priority | Phase |
|--------|------|-------|---------|-------------------|----------|-------|
| ConfirmWindow | `Views/ConfirmWindow.axaml` | — | Generic yes/no confirmation | 🟢 Bottom sheet with two buttons | P0 | Phase 2 |
| DeleteConfirmWindow | `Views/DeleteConfirmWindow.axaml` | — | Delete confirmation | 🟢 Bottom sheet with two buttons | P0 | Phase 2 |
| InputDialog | `Views/InputDialog.axaml` | — | Text input prompt | 🟢 Bottom sheet with text field | P1 | Phase 3 |
| ErrorDialog | `Views/ErrorDialog.axaml` | — | Error message display | 🟢 Bottom sheet with dismiss | P0 | Phase 2 |

### Inline / Skip

| Dialog | File | Lines | Purpose | Mobile Adaptation | Priority | Phase |
|--------|------|-------|---------|-------------------|----------|-------|
| AboutWindow | `Views/AboutWindow.axaml` | — | App info and credits | 🟢 Inline card or fullscreen | P2 | Phase 3 |
| SftpInfoWindow | `Views/SftpInfoWindow.axaml` | — | SFTP connection info | 🟢 Inline card | P2 | Phase 3 |
| DiscordPopup | `Views/DiscordPopup.axaml` | — | Discord invite link | 🟢 Inline card | P2 | Phase 3 |
| UsbPermissionWindow | `Views/UsbPermissionWindow.axaml` | — | USB drive permissions (Windows) | ⬜ Skip — Windows-only | — | — |
| LoopbackExemptWindow | `Views/LoopbackExemptWindow.axaml` | — | Loopback exemption (Windows) | ⬜ Skip — Windows-only | — | — |

---

## Root Views

| View | File | Lines | Purpose | Mobile Adaptation | Priority | Phase |
|------|------|-------|---------|-------------------|----------|-------|
| MainWindow | `MainWindow.axaml` | 770 | Desktop shell (sidebar + carousel) | 🟠 Replaced by MobileMainWindow on Android | P0 | Phase 1 |
| SplashWindow | `SplashWindow.axaml` | — | Startup splash screen | 🟡 May use Android native splash instead | P0 | Phase 0 |
| App.axaml | `App.axaml` | 31 | Application root, styles, resources | ✅ No changes needed | — | — |

---

## Summary by Phase

### Phase 1 (Mobile Shell)
- 1 view: MobileMainWindow (new)
- 0 view changes needed in existing UserControls

### Phase 2 (Core)
- BrowseView — responsive adaptation
- InstalledView — minor tweaks
- ConnectionWindow — fullscreen page conversion
- ConfirmWindow, DeleteConfirmWindow, ErrorDialog — bottom sheets
- ItemDetailWindow — fullscreen page

### Phase 3 (Extended)
- FileExplorerView — TreeView → breadcrumbs
- ToolsView — grid → list
- InspectorView — test AvaloniaEdit
- SettingsView — minor
- LogsView — test AvaloniaEdit
- SetupWizardWindow — fullscreen page
- InputDialog — bottom sheet
- TasksPanel, NotificationsPanel — mobile alternatives
- Remaining dialogs — fullscreen pages or inline

### Phase 4 (Polish)
- PerformanceWindow — chart redesign
- ScreenshotWindow, SystemInfoWindow, ProcessesWindow, NetworkInfoWindow, CrashDataWindow — fullscreen pages
- AboutWindow, SftpInfoWindow, DiscordPopup — inline cards
- Landscape, tablet, edge cases

---

## Views Not Requiring Changes

These views work on Android without modification:

| View | Why |
|------|-----|
| App.axaml | Styles and resources — platform-agnostic |
| SplashWindow | Minimal code — just sets version text |
| All 24 ViewModels | Pure MVVM — no platform dependencies |

---

## Android-Specific Views (New Files)

These are **independent Android views** in `XBVault.Android/Views/`. They reuse ViewModels from the shared project but have their own AXAML layouts designed for mobile.

| View | File | Phase | Description |
|------|------|-------|-------------|
| MobileSplashView | `Views/MobileSplashView.axaml` | 1B | Portrait splash with all text elements |
| MobileMainWindow | `Views/MobileMainWindow.axaml` | 1B | Shell: top bar + content + tab bar |
| BrowsePage | `Views/Pages/BrowsePage.axaml` | 2 | Browse content (responsive cards) |
| InstalledPage | `Views/Pages/InstalledPage.axaml` | 2 | Installed packages list |
| ConnectionPage | `Views/Pages/ConnectionPage.axaml` | 2 | Connection wizard (fullscreen) |
| FilesPage | `Views/Pages/FilesPage.axaml` | 3 | File explorer (breadcrumbs) |
| ToolsPage | `Views/Pages/ToolsPage.axaml` | 3 | Tools (vertical card list) |
| SettingsPage | `Views/Pages/SettingsPage.axaml` | 3 | Settings + Logs link |
| ConfirmPage | `Views/Pages/ConfirmPage.axaml` | 2 | Simple confirm (bottom sheet) |
| ErrorPage | `Views/Pages/ErrorPage.axaml` | 2 | Error display (bottom sheet) |
| AboutPage | `Views/Pages/AboutPage.axaml` | 3 | About dialog (fullscreen) |

**Rule**: All Android views are independent — copy visual patterns from desktop when needed, but have full freedom to change later.

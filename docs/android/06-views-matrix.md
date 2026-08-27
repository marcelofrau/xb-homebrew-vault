---
layout: default
title: Views Matrix
---

# Views Matrix — View-by-View Adaptation Status

Every AXAML view in the project, classified by type, mobile adaptation, and shipped status.

> **Updated 2026-08-27.** The Android port ships with **27 `Mobile*` files** — all views below are shipped and working on physical devices (v2.0.0+).

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Shipped — mobile view created, deployed and verified |
| ⬜ | Not applicable / intentionally excluded on mobile |

---

## Mobile Views (XBVault/Views/)

These are **standalone mobile views** in the shared `XBVault/Views/` directory (pure-Avalonia, no Android types — keeps `App.axaml.cs` in the shared project free of circular references). They reuse ViewModels from the shared project with their own portrait-first AXAML layouts.

### Shell & Chrome

| View | File | Status | Purpose |
|------|------|--------|---------|
| MobileSplashView | `Views/MobileSplashView.axaml` | ✅ | Portrait splash with background, version, Oxanium fonts; the only fullscreen content hosted outside the main shell |
| MobileMainWindow | `Views/MobileMainWindow.axaml` | ✅ | The single hybrid window (a `UserControl`): gradient top bar, tab Carousel, bottom tab bar, full-screen `NavigationPanel` overlay layer |
| MobileTitleBar | `Views/MobileTitleBar.axaml` | ✅ | Reusable top bar component — logo, back button, right/far-right content slots |

### Tabs

| View | File | Status | Purpose |
|------|------|--------|---------|
| MobileBrowseView | `Views/MobileBrowseView.axaml` | ✅ | Catalog cards with images, category filter, search, refresh spinner |
| MobileInstalledView | `Views/MobileInstalledView.axaml` | ✅ | Installed packages — launch/suspend/uninstall, update status |
| MobileFileExplorerView | `Views/MobileFileExplorerView.axaml` | ✅ | SFTP explorer — breadcrumbs, upload/download, new folder/delete, SAF content-URI uploads |
| MobileToolsView | `Views/MobileToolsView.axaml` | ✅ | Tools menu — card grid wired to the shared tool commands |

### Overlays (NavigationPanel)

| View | File | Status | Purpose |
|------|------|--------|---------|
| MobileConnectionView | `Views/MobileConnectionView.axaml` | ✅ | Connect screen — manual credentials or QR (scan/share) from the top-bar connect icon |
| MobileSetupWizardView | `Views/MobileSetupWizardView.axaml` | ✅ | First-run setup wizard via `MobileWizardShell` |
| MobileCustomInstallView | `Views/MobileCustomInstallView.axaml` | ✅ | Custom install + sideload wizard — local file (SAF) or URL (incl. GoFile/Drive/OneDrive) via `MobileWizardShell` |
| MobileDetailView | `Views/MobileDetailView.axaml` | ✅ | Item detail — thumbnail, metadata, downloads, install/update |
| MobileSettingsView | `Views/MobileSettingsView.axaml` | ✅ | Settings — single-column, save/back header |
| MobileAboutView | `Views/MobileAboutView.axaml` | ✅ | About — splash background, community dialogs, version |
| MobileLogsView | `Views/MobileLogsView.axaml` | ✅ | Logs — plain text log viewer with copy + **Save Log** (file / share) |
| MobileJobsView | `Views/MobileJobsView.axaml` | ✅ | Jobs — background activity with progress, retry, abort |
| MobileNotificationsView | `Views/MobileNotificationsView.axaml` | ✅ | Notifications — in-app history from the notification center |
| MobileSftpInfoView | `Views/MobileSftpInfoView.axaml` | ✅ | SFTP transfer info overlay |
| MobileLoopbackView | `Views/MobileLoopbackView.axaml` | ✅ | Loopback exempt manager |
| MobileScreenshotView | `Views/MobileScreenshotView.axaml` | ✅ | Screenshot viewer |
| MobileToolResultView | `Views/MobileToolResultView.axaml` | ✅ | Generic tool result (output text) overlay |
| MobileToolOverlayView | `Views/MobileToolOverlayView.axaml` | ✅ | In-session tool execution overlay |

### Dialogs

| View | File | Status | Purpose |
|------|------|--------|---------|
| MobileConfirmDialogView | `Views/MobileConfirmDialogView.axaml` | ✅ | Confirm dialog (replaces ConfirmWindow / DeleteConfirmWindow) |
| MobileInputDialogView | `Views/MobileInputDialogView.axaml` | ✅ | Text input dialog |
| MobileInfoDialogView | `Views/MobileInfoDialogView.axaml` | ✅ | Info dialog |
| MobileErrorDialogView | `Views/MobileErrorDialogView.axaml` | ✅ | Global error dialog (shown for unhandled exceptions) |
| MobileQrDialogView | `Views/MobileQrDialogView.axaml` | ✅ | QR code display (connection share, log share) |

### Wizard Shell

| View | File | Status | Purpose |
|------|------|--------|---------|
| MobileWizardShell | `Views/MobileWizardShell.axaml` | ✅ | Shared multi-step wizard chrome (Next/Back/step dots) used by setup, custom install and sideload flows |

---

## Desktop View Coverage (what replaced what)

| Desktop view | Mobile replacement | Status |
|--------------|--------------------|--------|
| MainWindow | `MobileMainWindow` | ✅ |
| SplashWindow | `MobileSplashView` + native pre-splash | ✅ |
| BrowseView | `MobileBrowseView` | ✅ |
| ItemDetailWindow | `MobileDetailView` | ✅ |
| InstalledView | `MobileInstalledView` | ✅ |
| FileExplorerView | `MobileFileExplorerView` | ✅ |
| ToolsView | `MobileToolsView` | ✅ |
| SettingsView | `MobileSettingsView` | ✅ |
| LogsView | `MobileLogsView` (plain TextBlock, no AvaloniaEdit) | ✅ |
| ConnectionWindow | `MobileConnectionView` | ✅ |
| SetupWizardWindow | `MobileSetupWizardView` (`MobileWizardShell`) | ✅ |
| CustomInstallWindow | `MobileCustomInstallView` (`MobileWizardShell`) | ✅ |
| **SideloadWizard** (new on mobile) | sideload flow inside `MobileCustomInstallView` | ✅ |
| ConfirmWindow / DeleteConfirmWindow | `MobileConfirmDialogView` | ✅ |
| InputDialog | `MobileInputDialogView` | ✅ |
| InfoDialog | `MobileInfoDialogView` | ✅ |
| ErrorDialog | `MobileErrorDialogView` | ✅ |
| QrDialog | `MobileQrDialogView` | ✅ |
| AboutWindow | `MobileAboutView` | ✅ |
| SftpInfoWindow | `MobileSftpInfoView` | ✅ |
| LoopbackExemptWindow | `MobileLoopbackView` | ✅ |
| ScreenshotWindow | `MobileScreenshotView` | ✅ |
| Performance / SystemInfo / Processes / NetworkInfo / CrashData | `MobileToolResultView` / `MobileToolOverlayView` (in-tab tool execution) | ✅ |
| TasksPanel / NotificationsPanel | `MobileJobsView` / `MobileNotificationsView` | ✅ |
| UsbPermissionWindow | ⬜ Windows-only (USB detection) | ⬜ |
| InspectorView | ⬜ Excluded from Android (desktop XRay console) | ⬜ |

---

## Summary

- **27 `Mobile*` files** — 4 tab views, 1 shell, 1 title bar, 1 splash, 5 dialog views, 1 wizard shell, and 14 overlay/screen views — all shipped in **v2.0.0+**.
- Mobile views reuse the **shared ViewModels**; `App.axaml.cs` wires their `Action`/`Func` delegates (dialogs, share, logs) to the mobile overlay views.
- Only **USB permission** (desktop hardware) and **Inspector** (AvaloniaEdit console) are intentionally absent on Android.
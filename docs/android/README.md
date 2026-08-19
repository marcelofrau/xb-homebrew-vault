---
layout: default
title: Android Port
---

# XBVault Android Port

> **Status: Phase 1B complete, Phase 2 in progress.** Splash, main shell, and 6 mobile views deployed and working on physical device. Catalog browse with item detail functional. Next: Installed, Settings, Tools content, connection, dialogs.

## Objective

Port the existing Avalonia desktop application to Android, starting with core functionality (Browse, Installed, Connection) and incrementally adding features. The goal is a fully functional Android app that connects to Xbox Dev Mode via the same HTTP/SSH APIs used by the desktop version.

## Why Avalonia?

XBVault already uses Avalonia 12 for its desktop UI. Avalonia natively supports Android (`net10.0-android`), meaning:

- **No UI framework rewrite** — same AXAML views, same MVVM pattern, same CommunityToolkit.Mvvm ViewModels
- **Shared service layer** — all Xbox HTTP services, SSH/SFTP, and business logic are cross-platform already
- **Single codebase** — one project serves desktop and mobile with platform-specific adaptations where needed

## Documentation Index

| Document | Description |
|----------|-------------|
| [01-architecture](01-architecture.md) | Project structure, dependency flow, design decisions |
| [02-platform-analysis](02-platform-analysis.md) | Full codebase audit — views, services, ViewModels classified by Android readiness |
| [03-ui-adaptation](03-ui-adaptation.md) | Mobile layout changes — navigation, responsive design, dialogs |
| [04-services](04-services.md) | Service layer adaptation — HTTP, SSH, file paths, platform-specific code |
| [05-implementation-plan](05-implementation-plan.md) | Phased implementation with tasks, estimates, and acceptance criteria |
| [06-views-matrix](06-views-matrix.md) | View-by-view adaptation matrix with priority and complexity |
| [07-testing-strategy](07-testing-strategy.md) | Emulator setup, device testing, connection validation |
| [08-build-and-release](08-build-and-release.md) | Build configuration, CI pipeline, APK generation |
| [09-mobile-ux-design](09-mobile-ux-design.md) | Complete Android UX spec: screens, tabs, dialogs, icons, colors |
| [Developer Architecture Guide](../developer-architecture.md) | Shared contracts, layer boundaries, threading rules, and cross-frontend guidance |

## Quick Summary

| Metric | Value |
|--------|-------|
| Mobile views created | 7 — Splash, MainWindow, BrowseView, DetailView, AboutView, SettingsView, ToolsView |
| ViewModels | 24 — all cross-platform, no changes needed |
| Services | 33 implementations — 18 fully compatible, 12 need minor changes, 1 needs major changes (PlatformDialog), 2 not applicable on mobile |
| Platform-specific code | P/Invoke (3 files), WMI (1 file) — all guarded or not-applicable |
| Blocking issues | Launcher icon showing generic (needs adaptive icon fix), native splash logo missing |

## Estimated Timeline

| Phase | Duration | Status |
|-------|----------|--------|
| Phase 0: Project setup | ✅ Done | Buildable Android project skeleton |
| Phase 1A: Placeholder | ✅ Done | Pre-splash placeholder on emulator |
| Phase 1B: Shell + Splash | ✅ Done | Pre-splash, Avalonia splash, MobileMainWindow shell |
| Phase 1C: Mobile views | ✅ Done | Browse, Detail, About, Settings, Tools views created |
| Phase 2: Core views | In progress | Browse functional, need Installed + Connection |
| Phase 3: Extended features | 5–8 days | File Explorer, full Tools, full Settings, Logs |
| Phase 4: Polish | 3–5 days | Dialogs, notifications, back button, edge cases |

---
layout: default
title: Android Port
---

# XBVault Android Port

> **Status: Phase 0 complete.** Buildable Android project skeleton with 3-project structure (shared library + desktop host + Android host). All projects compile, 240 tests pass. Ready for Phase 1 (mobile shell/navigation).

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
| [Developer Architecture Guide](../developer-architecture.md) | Shared contracts, layer boundaries, threading rules, and cross-frontend guidance |

## Quick Summary

| Metric | Value |
|--------|-------|
| Views (AXAML) | 33 total — 9 UserControls (main tabs + panels), 21 Windows (dialogs), 3 root (App, MainWindow, SplashWindow) |
| ViewModels | 24 — all cross-platform, no changes needed |
| Services | 33 implementations — 18 fully compatible, 12 need minor changes, 1 needs major changes (PlatformDialog), 2 not applicable on mobile |
| Platform-specific code | P/Invoke (3 files), WMI (1 file) — all guarded or not-applicable |
| Blocking issues | None in project build; local environment must select JDK 21 for Android (`JAVA_HOME=%LOCALAPPDATA%/Android/Sdk/jdk-21`) |

## Estimated Timeline

| Phase | Duration | Deliverable |
|-------|----------|-------------|
| Phase 0: Project setup | 1–2 days | Buildable Android project skeleton |
| Phase 1: Mobile shell | 3–5 days | Bottom tab navigation, status bar, responsive MainWindow |
| Phase 2: Core views | 5–8 days | Browse, Installed, Connection working on Android |
| Phase 3: Extended features | 5–8 days | File Explorer, Tools, Settings, Inspector |
| Phase 4: Polish | 3–5 days | Dialogs, notifications, landscape, edge cases |
| **Total** | **17–28 days** | Feature-complete Android release |

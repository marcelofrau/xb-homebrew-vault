---
layout: default
title: Android Port
---

# XBVault Android Port

> **Status: Shipped in v2.0.0.** The Android app is released and working on physical devices — splash, shell, 4 tabs, all overlays, sideload wizard, QR connect, file explorer, logs, jobs and notifications. See the [Mobile Guide](../mobile) for end-user instructions and the [views matrix](06-views-matrix) for the full view inventory.

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
| Mobile files (`XBVault/Views/Mobile*`) | **27** — splash, shell, title bar, 4 tabs, wizard shell, 5 dialogs + 14 overlays/screens |
| ViewModels | Shared with desktop — no mobile-specific VM layer |
| Services | 33 implementations — shared with desktop, property-passed from `App.axaml.cs` |
| Desktop-only | USB permission wizard (hardware) and Inspector (XRay console) |
| Blocking issues | None — safe areas, status-bar icons, URL resolver and SAF content-URI fixes all landed |

## Delivery Status

| Phase | Status |
|-------|--------|
| Phase 0: Project setup | ✅ Done — buildable Android project skeleton |
| Phase 1A: Placeholder | ✅ Done — pre-splash placeholder on emulator |
| Phase 1B: Shell + Splash | ✅ Done — pre-splash, Avalonia splash, `MobileMainWindow` shell |
| Phase 1C: Mobile views | ✅ Done — browse, detail, about, settings, tools, dialogs |
| Phase 2: Core views | ✅ Done — installed, connection, file explorer, logs |
| Phase 3: Extended features | ✅ Done — sideload wizard, jobs, notifications, loopback, screenshot |
| Phase 4: Polish | ✅ Done — safe areas, status-bar icons, back navigation, QR connect |

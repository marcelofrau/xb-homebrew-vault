---
layout: default
title: Documentation
---

# Documentation

Everything you need to understand, build, and contribute to XB Homebrew Vault.

---

## Overview

| Document | Description |
|----------|-------------|
| [User Manual](user-manual) | Complete guide for using the app — install, manage, configure |
| [Troubleshooting](troubleshooting) | Fix common problems — connection, installs, crashes |
| [Requirements](requirements) | Functional and non-functional requirements — what the app must do and how |
| [Roadmap](roadmap) | Version history, shipped features, and what's planned next |
| [Inspector](inspector) | XRay TCP agent discovery, Lua REPL, live log streaming — Xbox diagnostics built-in |
| [Mobile Guide](mobile) | End-user guide for the Android app — APK install, QR connect, sideload, file explorer, logs |

---

## Architecture

| Document | Description |
|----------|-------------|
| [Architecture](architecture) | Layered architecture, MVVM structure, service map, startup flow, CI — with Mermaid diagrams |
| [API Reference](api) | Full Xbox Device Portal REST + WebSocket endpoint reference with request/response examples |
| [Data Sources](data-sources) | Emulation Revival `catalog.json` API, cache structure, package manifest format |
| [Blades Theme](theme) | Color palette, typography, title bar gradient, component styles |
| [Window Template](window-template) | AXAML template for new windows — drag, close button, green border pattern |

---

## How It Works

Deep-dives into the trickier integration challenges — how the app actually talks to the Xbox.

| Document | Description |
|----------|-------------|
| [Package Installation Flow](integration-package-installation-flow) | Dependency detection, main package + dependency install, registration, wait/retry logic, and how failures are handled |
| [SSH/SFTP & Path Handling](integration-ssh-sftp-challenges) | Path handling over SFTP, the `cmd.exe` shell layer, `dir`-style command quirks, and USB drive discovery |
| [USB Device Discovery](integration-usb-device-discovery) | WMI-based drive detection, permission setup with `icacls`, and the Windows-side discovery flow |

---

## Mobile (Android)

The Android app (v2.0.0+) is a portrait-first port that reuses the desktop services and ViewModels behind a new phone-form-factor view layer.

| Document | Description |
|----------|-------------|
| [Mobile Guide](mobile) | End-user guide — install the APK, QR connect, browse, sideload, file explorer, logs, jobs |
| [Android Architecture](android/01-architecture) | How the port reuses desktop services/VMs — shell, safe areas, back navigation, lifecycle |
| [Services Adaptation](android/04-services) | Service consumption matrix on Android — SAF paths, storage, platform limits |
| [Views Matrix](android/06-views-matrix) | Every mobile view vs its desktop counterpart — shipped status |
| [Mobile UX Design](android/09-mobile-ux-design) | Portrait-first design principles, tab structure, wizard/dialog system |
| [Android Build & Release](android/08-build-and-release) | Signing, versionCode, APK naming, CI pipeline, sideloading |
| [Android Testing Strategy](android/07-testing-strategy) | Test approach, build constraints (AOT/trimming), device testing |

---

## Development

| Document | Description |
|----------|-------------|
| [Tech Debt](tech-debt) | Known issues ordered by severity — open items with file:line references and fix recommendations |
| [Branching & Versioning](branching-and-versioning) | Git branch strategy, SemVer rules, commit message conventions, release workflow |
| [Assets Guide](assets-guide) | Icon naming conventions, size selection, directory structure, format rules |
| [Cross-Platform Porting](cross-platform-porting) | Windows/macOS/Linux/Android support — porting history, blockers, CI matrix |
| [Developer Architecture](developer-architecture) | Shared service contracts, ViewModel boundaries, threading rules, Android reuse guidance |
| [Portal Filesystem API](portal-filesystem-api) | The REST API behind the User Files portal browser — endpoints, auth, operations |

---

## Feature Specs

| Document | Description |
|----------|-------------|
| [File Explorer](feature-file-explorer) | Browse the Xbox filesystem, transfer files via SFTP, USB drive access |
| [Loopback Exempt](feature-loopback-exempt) | Allow development-mode packages to reach the network via loopback exemption |

---

## Other

| Document | Description |
|----------|-------------|
| [Attributions](attributions) | Credits for icons, data sources, frameworks, and background images |

---

## Quick links

- [Download latest release](https://github.com/marcelofrau/xb-homebrew-vault/releases/latest)
- [Open an issue](https://github.com/marcelofrau/xb-homebrew-vault/issues)
- [View source on GitHub](https://github.com/marcelofrau/xb-homebrew-vault)

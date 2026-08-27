---
layout: default
title: Mobile Guide (Android)
description: End-user guide for the XB Homebrew Vault Android app — install the APK, connect by QR or credentials, browse, sideload, and manage your Xbox from your phone.
---

# Mobile Guide (Android)

Since **v2.0.0**, XB Homebrew Vault ships as a **portrait-first Android app** alongside the desktop app. It reuses the same service layer and ViewModels as the desktop build behind a new phone-form-factor view layer — so Browse, Installed, Tools, Settings, logs, jobs and notifications all work from your phone.

- **Architecture**: Android 6.0+ (API 23)
- **CPU**: ARM64 (`android-arm64` build)
- **Orientation**: portrait, fullscreen, edge-to-edge on supported devices
- **Shortcut**: [Latest APK](https://github.com/marcelofrau/xb-homebrew-vault/releases/latest)

## Installing the APK

1. Download `XBVault-{version}-android-arm64.apk` from the [releases page](https://github.com/marcelofrau/xb-homebrew-vault/releases/latest).
2. Open the download and allow **"Install unknown apps"** for your browser (or file manager) when prompted.
3. Complete device verification / **Play Protect** warning — the APK is code-signed with the project's release keystore, not the Play Store.
4. Launch **XB Homebrew Vault**.

> Sideloading via ADB also works: `adb install XBVault-{version}-android-arm64.apk`. On emulators always `adb uninstall io.github.marcelofrau.xbvault` first — incremental (FastDev) installs corrupt bundled assemblies.

## First run

1. A **splash screen** shows for a few seconds while the app composes its services.
2. If no connection is configured, the **setup wizard** opens — enter your console's Dev Mode **address** (IP or hostname) and **credentials**.
3. The main shell opens with the bottom tab bar: **Browse · Installed · Tools · Settings**.

## Connecting

- Open **Settings → Connection** (or the connect screen from the shell).
- Enter the address + credentials manually, or **scan the QR code** that the desktop app can share from its Connection screen (QR Connect).
- The same credentials are used automatically by browse, install, file explorer and tools.

> The Xbox Dev Mode portal runs on port **11443** over HTTPS. Your phone and console must be on the same network.

## Tabs

| Tab | What you can do |
|-----|-----------------|
| **Browse** | Emulation Revival catalog with category filters — tap an item for the detail view (about, downloads, compatibility), then **Install / Update** |
| **Installed** | Every package on the console — launch, suspend, uninstall, and see update status (adaptive badges) |
| **Tools** | Mobile-form tools — process list, network/system info, screenshot, crash dumps, performance chart, update check, autostart toggle |
| **Settings** | Connection, credentials, app-update scanning, log level, theme, About |

## Sideloading

Beyond catalog installs, the **sideload wizard** installs arbitrary `.appx` / `.msix` / `.zip` packages:

1. Open the sideload wizard (Tools or the install flow).
2. Pick a source:
   - **Local file** — Android file picker (SAF). Selecting any file works even if your file manager hides the app's files.
   - **URL** — direct link, or indirect share links from **GoFile**, **Google Drive**, or **OneDrive** (resolved automatically).
3. Review dependencies, then install — the job appears in **Jobs** with live progress, and you can **Abort** it.

## Logs, Jobs & Notifications

- **Logs** — the app's activity log with copy and **Save Log** (exports the log to storage / share, including QR and GoFile upload).
- **Jobs** — background activity: catalog refresh, downloads, installs, updates, screenshots. Shows progress and lets you retry or abort.
- **Notifications** — surface updates from jobs and background tasks (e.g. an app update is available), with in-app history.

## Using the app

- **Back button** — closes overlay views (dialogs, wizards) first, then steps back through tab history, and exits when you're on Browse (the base tab).
- **Safe areas** — on Android 15+ the app is edge-to-edge and draws its own margins from the system insets; on older versions the system bars stay outside the app surface and margins are zeroed automatically.
- Desktop users: the mobile app shares its data model with the desktop build — what you install from either is the same Xbox-facing state.

## See also

- [Android Architecture](android/01-architecture) — how the port reuses desktop services/ViewModels
- [Mobile UX Design](android/09-mobile-ux-design) — portrait-first design principles
- [Android Build & Release](android/08-build-and-release) — APK signing, versionCode, CI
- [Troubleshooting](troubleshooting) — shared desktop/mobile connection and install issues

---

[← User Manual](user-manual) · [Android Architecture →](android/01-architecture)
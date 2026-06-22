# 🎮 XB Homebrew Vault

[![GitHub release](https://img.shields.io/github/v/release/marcelofrau/xb-homebrew-vault?style=flat-square)](https://github.com/marcelofrau/xb-homebrew-vault/releases)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square)](https://dotnet.microsoft.com/download)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?style=flat-square)](https://github.com/marcelofrau/xb-homebrew-vault/releases)

> Desktop manager for **Xbox Dev Mode** homebrew — browse, install, and manage emulators and apps from [Emulation Revival](https://emulationrevival.github.io), plus remote console tools via the Xbox Device Portal API.

<p align="center">
  <img src="docs/social-preview.png" alt="XB Homebrew Vault" width="800"/>
</p>

---

## ✨ Features

| | Feature | Description |
|---|---------|-------------|
| 🔍 | **Catalog Browser** | Browse and search the full Emulation Revival catalog (emulators, apps, ports, utilities) with category and compatibility filters |
| 🔗 | **Xbox Connect** | Connect to Xbox Dev Mode Device Portal — saved credentials (obfuscated), connection test, status indicator |
| 📦 | **Package Management** | View installed packages with sizes, version info; install (with auto-dependency resolution) and uninstall wirelessly |
| ⬇️ | **Custom Install** | Install `.appx`/`.msix`/`.zip` packages from local files or download URLs — InstallShield-style wizard with analysis, dependency check, and dual progress bars |
| 🛠️ | **Dev Tools Panel** | Remote console tools — screenshot capture, system info, process manager, network info, real-time performance chart (CPU/GPU/RAM), console restart/shutdown |
| 📁 | **File Explorer** | Browse files and directories on the Xbox file system |
| 🌙 | **Blades Theme** | Xbox 360 Blades-inspired dark theme with green accents |
| 🔐 | **Secure Storage** | Obfuscated credential storage (XOR + Base64) |
| 📋 | **Activity Log** | Full application log with multi-select, copy, auto-scroll, and configurable log level |

## 📸 Screenshots

| | |
|---|---|
| **Main Window** — catalog browser with Blades theme | **Browse & Detail View** |
| ![](docs/screenshots/main.png) | ![](docs/screenshots/detailview-browse.png) |

<details>
<summary>More screenshots (click to expand)</summary>

| | |
|---|---|
| **Installed Packages** | **Installing from Browse** |
| ![](docs/screenshots/installed.png) | ![](docs/screenshots/installing-from-browse.png) |
| **Install Complete** | **Custom Install Wizard** |
| ![](docs/screenshots/install-complete-from-browse.png) | ![](docs/screenshots/wizard-installcustom.png) |
| **Wizard — Analysis** | **Wizard — Confirm** |
| ![](docs/screenshots/wizard-installcustom1.png) | ![](docs/screenshots/wizard-installcustom2.png) |
| **Confirm Uninstall** | **Not Connected Warning** |
| ![](docs/screenshots/confirm-uninstall.png) | ![](docs/screenshots/install-not-connected.png) |
| **Connection Dialog** | **About Window** |
| ![](docs/screenshots/connecting.png) | ![](docs/screenshots/about-window.png) |
| **Dev Tools Panel** | **Performance Monitor** |
| ![](docs/screenshots/tools.png) | ![](docs/screenshots/performancemonitor.png) |
| **Process List** | **Screen Capture** |
| ![](docs/screenshots/processlist.png) | ![](docs/screenshots/screen%20capture.png) |

</details>

> Tip: click any screenshot to view full size.

---

## 📥 Installation

Download the latest release from the [Releases page](https://github.com/marcelofrau/xb-homebrew-vault/releases).

```powershell
# Extract XBVault-v0.8.5-win-x64.zip and run XBVault.exe
```

## 📋 Prerequisites

- **Windows 10/11** (x64)
- **Xbox One** or **Xbox Series S|X** in [Developer Mode](https://wiki.sternserv.xyz/docs/xbox-setup/xbox-developer-mode-setup)

## 🎯 Usage

### Quick start

1. Open **Settings** → enter your Xbox IP address and Dev Mode credentials (username + password)
2. Click **Connect** — a green connection indicator appears when successful
3. Browse the catalog or use the **Tools** panel for remote management

### Catalog operations

| Action | Description |
|--------|-------------|
| **Browse** | Browse Emulation Revival catalog with dynamic category filter |
| **Search** | Search by name, description, or developer across cached catalog |
| **Install** | Select an app → auto-download, dependency analysis, upload to Xbox |
| **Uninstall** | Remove installed packages via remote API |

### Custom Install Wizard

Opens from **Browse** or **Tools** panels. Supports local files (`.appx`/`.msix`/`.appxbundle`/`.msixbundle`/`.zip`) and download URLs.

1. **Source** — pick a local file or enter a download URL
2. **Analysis** — auto-analyzes archives and directories, classifies main package vs dependencies
3. **Confirm** — review package list and dependency count
4. **Install** — dual progress bars (overall + current package), spinning indicator, success/failure result

### Dev Tools

| Tool | Description |
|------|-------------|
| **Screenshot** | Capture Xbox screen; save as PNG |
| **System Info** | Console info — OS version, CPU, memory, temperatures |
| **Processes** | List running processes; filter by name; kill selected |
| **Network Info** | Wi-Fi networks, connection profiles, IP config |
| **Performance** | Real-time CPU/GPU/RAM chart with WebSocket connection |
| **Restart** | Restart Xbox remotely (with confirmation) |
| **Shutdown** | Shut down Xbox remotely (with confirmation) |
| **Open Dev Portal** | Open Xbox Device Portal in browser (authenticated URL) |

### Connections

| URL Type | Format |
|----------|--------|
| Xbox Dev Portal | `https://{ip}:11443` |
| WebSocket (perf) | `wss://{ip}:11443/api/resourcemanager/systemperf` |

---

## 🧰 Tech Stack

| Layer | Technology |
|-------|-----------|
| ⚙️ Runtime | .NET 8 |
| 🖥️ UI Framework | Avalonia UI 12 (Fluent theme) |
| 🏗️ Architecture | MVVM (CommunityToolkit.Mvvm, source generators) |
| 📡 API | Xbox Device Portal API (REST + WebSocket), Emulation Revival JSON API |

## 🏛️ Project Structure

```
XBVault/
├── Models/               # Data models (CatalogItem, CatalogApi, etc.)
├── ViewModels/           # MVVM view models
├── Views/                # Avalonia UI (AXAML) windows & controls
├── Services/             # Business logic & API clients
│   ├── XboxDeviceService.cs       — All Xbox API calls
│   ├── CatalogApiService.cs       — Emulation Revival JSON catalog API
│   ├── PackageInstallService.cs   — Package analysis & install pipeline
│   ├── SettingsService.cs         — Settings persistence
│   ├── CryptoService.cs           — Credential obfuscation
│   └── Logger.cs                  — Application logging
├── Converters/           # Value converters
├── Assets/               # Icons, fonts, themes
└── Controls/             # Custom UI controls
build/                    # Build & packaging scripts
docs/                     # Documentation
```

## 🗺️ Roadmap

| Phase | Status | Description |
|-------|--------|-------------|
| 0 — Scaffold | ✅ | Project structure, Blades theme, splash, navigation, build scripts |
| 1 — Connection | ✅ | Xbox connection, settings, credential encryption |
| 2 — Catalog | ✅ | Emulation Revival browser with search, filters, item details |
| 3 — Package Management | ✅ | Install/uninstall, dependency resolution, cache, progress bars |
| 4 — Tools | ✅ | Screenshot, system info, processes, network, performance chart |
| 5 — Refinement | ✅ | Error dialogs, exit confirmation, custom install wizard, log viewer |
| 6 — Cross-platform | ⏳ | Linux/macOS builds, CI matrix |
| 7 — Polish | ⏳ | Accessibility, edge cases, localization |

See [docs/PLAN.md](docs/PLAN.md) for detailed versioning and release strategy.

## 🏗️ Building from source

Requires **.NET 8 SDK**.

```powershell
# Clone
git clone https://github.com/marcelofrau/xb-homebrew-vault.git
cd xb-homebrew-vault

# Run (development)
.\build\run.ps1

# Build release
.\build\build-release.ps1 -Version 0.8.5 -Arch x64
```

The release script produces a self-contained ZIP at `build/dist/XBVault-v<Version>-win-<Arch>.zip`.

## 📦 Release artifacts

Releases are built on tag push (`v*`). Each release includes a Windows x64 self-contained ZIP.

## 🙏 Thanks

Splash and About window backgrounds by **Johnson Martin** on [Unsplash](https://unsplash.com/@johnsonmartin).

Special thanks to **MewLew** and the [Emulation Revival](https://emulationrevival.github.io) team for their curated catalog JSON API — the same source that powers the Emulation Revival website powers this app's Browse experience. Without their work organizing and maintaining the homebrew catalog, XB Homebrew Vault wouldn't be possible.

## 🎨 Icons

Icons by [Icons8](https://icons8.com) (3d-fluency & fluency styles), [Microsoft FluentUI Emoji](https://github.com/microsoft/fluentui-emoji), and [KyleBing retro console icons](https://github.com/KyleBing/retro-game-console-icons).

See [docs/ATTRIBUTIONS.md](docs/ATTRIBUTIONS.md) for full attribution.

## 📄 License

GNU General Public License v3.0 — see [LICENSE](LICENSE) for details.

---

<p align="center">
  <sub>⚠️ Not affiliated with Microsoft, Xbox, or Emulation Revival.</sub>
</p>

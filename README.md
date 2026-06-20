# 🎮 XB Homebrew Vault

[![GitHub release](https://img.shields.io/github/v/release/marcelofrau/xb-homebrew-vault?style=flat-square)](https://github.com/marcelofrau/xb-homebrew-vault/releases)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square)](https://dotnet.microsoft.com/download)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?style=flat-square)](https://github.com/marcelofrau/xb-homebrew-vault/releases)

> Desktop manager for **Xbox Dev Mode** homebrew — browse, install, and manage emulators and apps from [Emulation Revival](https://emulationrevival.github.io), plus remote console tools via the Xbox Device Portal API.

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

## 📥 Installation

Download the latest release from the [Releases page](https://github.com/marcelofrau/xb-homebrew-vault/releases).

```powershell
# Extract XBVault-v0.8.0-win-x64.zip and run XBVault.exe
```

## 📋 Prerequisites

- **Windows 10/11** (x64)
- **Xbox One** or **Xbox Series S|X** in [Developer Mode](https://wiki.sternserv.xyz/docs/xbox-setup/xbox-developer-mode-setup)
- **.NET 8 SDK** (only for building from source)

## 🏗️ Building from source

```powershell
# Clone
git clone https://github.com/marcelofrau/xb-homebrew-vault.git
cd xb-homebrew-vault

# Run (development)
.\build\run.ps1

# Build release
.\build\build-release.ps1 -Version 0.8.0 -Arch x64
```

The release script produces a self-contained ZIP at `build/dist/XBVault-v<Version>-win-<Arch>.zip`.

## 🎯 Usage

### Quick start

1. Open **Settings** → enter your Xbox IP address and Dev Mode credentials (username + password)
2. Click **Connect** — a green connection indicator appears when successful
3. Browse the catalog or use the **Tools** panel for remote management

### Catalog operations

| Action | Description |
|--------|-------------|
| **Browse** | Browse Emulation Revival catalog with category filter (Emulators, Apps, Ports, Utilities) |
| **Search** | Search by name across cached catalog |
| **Filter** | Filter by compatibility tier |
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

## 🧰 Tech Stack

| Layer | Technology |
|-------|-----------|
| ⚙️ Runtime | .NET 8 |
| 🖥️ UI Framework | Avalonia UI 12 (Fluent theme) |
| 🏗️ Architecture | MVVM (CommunityToolkit.Mvvm, source generators) |
| 🌐 HTML Parsing | HtmlAgilityPack |
| 📡 API | Xbox Device Portal API (REST + WebSocket) |

## 🏛️ Project Structure

```
XBVault/
├── Models/               # Data models
├── ViewModels/           # MVVM view models
│   ├── MainViewModel.cs
│   ├── BrowseViewModel.cs
│   ├── InstalledViewModel.cs
│   ├── ConnectionViewModel.cs
│   ├── CustomInstallViewModel.cs
│   ├── SettingsViewModel.cs
│   ├── ConfirmViewModel.cs
│   ├── ToolsViewModel.cs
│   ├── ScreenshotViewModel.cs
│   ├── SystemInfoViewModel.cs
│   ├── ProcessesViewModel.cs
│   ├── NetworkInfoViewModel.cs
│   ├── PerformanceViewModel.cs
│   ├── FileExplorerViewModel.cs
│   ├── LogsViewModel.cs
│   └── RefreshViewModel.cs
├── Views/                # Avalonia UI (AXAML) windows & controls
│   ├── BrowseView.axaml
│   ├── InstalledView.axaml
│   ├── SettingsView.axaml
│   ├── ToolsView.axaml
│   ├── FileExplorerView.axaml
│   ├── LogsView.axaml
│   ├── ConnectionWindow.axaml
│   ├── CustomInstallWindow.axaml
│   ├── ItemDetailWindow.axaml
│   ├── ConfirmWindow.axaml
│   ├── ErrorDialog.axaml
│   ├── ScreenshotWindow.axaml
│   ├── SystemInfoWindow.axaml
│   ├── ProcessesWindow.axaml
│   ├── NetworkInfoWindow.axaml
│   ├── PerformanceWindow.axaml / PerformanceChart.cs
│   └── ...
├── Services/             # Business logic & API clients
│   ├── XboxDeviceService.cs     — All Xbox API calls
│   ├── EmulationRevivalService.cs — Catalog scraper
│   ├── PackageInstallService.cs  — Package analysis
│   ├── CacheService.cs          — Catalog cache
│   ├── SettingsService.cs       — Settings persistence
│   ├── CryptoService.cs         — Credential obfuscation
│   └── Logger.cs               — Application logging
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
| 5 — Refinement | 🔄 | Error dialogs, exit confirmation, custom install wizard, log viewer |
| 6 — Cross-platform | ⏳ | Linux/macOS builds, CI matrix |
| 7 — Polish | ⏳ | Accessibility, edge cases, localization |

See [docs/PLAN.md](docs/PLAN.md) for detailed versioning and release strategy.

## 📦 Release artifacts

Releases are auto-built by GitHub Actions on tag push (`v*`). Each release includes a Windows x64 self-contained ZIP attached to the release page.

## 🎨 Icons

Icons by [Icons8](https://icons8.com) (3d-fluency & fluency styles), [Microsoft FluentUI Emoji](https://github.com/microsoft/fluentui-emoji), and [KyleBing retro console icons](https://github.com/KyleBing/retro-game-console-icons).

See [docs/ATTRIBUTIONS.md](docs/ATTRIBUTIONS.md) for full attribution.

## 📄 License

GNU General Public License v3.0 — see [LICENSE](LICENSE) for details.

---

<p align="center">
  <sub>⚠️ Not affiliated with Microsoft, Xbox, or Emulation Revival.</sub>
</p>

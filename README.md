# 🎮 XB Homebrew Vault

[![GitHub release](https://img.shields.io/github/v/release/marcelofrau/xb-homebrew-vault?style=flat-square)](https://github.com/marcelofrau/xb-homebrew-vault/releases)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square)](https://dotnet.microsoft.com/download)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?style=flat-square)](https://github.com/marcelofrau/xb-homebrew-vault)
[![Build](https://img.shields.io/github/actions/workflow/status/marcelofrau/xb-homebrew-vault/build.yml?branch=main&style=flat-square)](https://github.com/marcelofrau/xb-homebrew-vault/actions)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](CONTRIBUTING.md)

> 📦 Desktop manager for Xbox Dev Mode homebrew — browse, install & manage emulators and apps from [Emulation Revival](https://emulationrevival.github.io).

<p align="center">
  <img src="XBVault/Assets/Icons/app.ico" alt="XB Homebrew Vault" width="128"/>
</p>

---

## ✨ Features

| | Feature | Description |
|---|---------|-------------|
| 🔍 | **Browse Catalog** | Full Emulation Revival catalog (emulators, apps, ports, utilities) |
| 🔗 | **Xbox Connect** | Connect to Xbox Dev Mode via Device Portal API |
| 📋 | **Package List** | View installed packages on your console |
| ⬇️ | **One-Click Install** | Download → cache → deploy pipeline |
| ❌ | **Remote Uninstall** | Remove packages wirelessly |
| 🌙 | **Blades Theme** | Xbox 360 Blades-inspired dark theme |
| 🔐 | **Secure Storage** | Obfuscated credential storage |

## 📸 Screenshots

> _Coming soon — contributions welcome!_ 🙌

## 📋 Prerequisites

- 🪟 **Windows 10/11**
- 🎮 **Xbox One** or **Xbox Series S|X** in [Developer Mode](https://wiki.sternserv.xyz/docs/xbox-setup/xbox-developer-mode-setup)
- 🛠️ [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building)

## ⚡ Installation

Download latest release from the [Releases page](https://github.com/marcelofrau/xb-homebrew-vault/releases).

```powershell
# Extract XBVault-v0.4.0-win-x64.zip and run XBVault.exe
```

## 🏗️ Building from source

```powershell
# Clone
git clone https://github.com/marcelofrau/xb-homebrew-vault.git
cd xb-homebrew-vault

# Run (development)
.\build\run.ps1

# Build release
.\build\build-release.ps1 -Version 0.4.0 -Arch x64
```

## 🎯 Usage

| Step | Action | Details |
|------|--------|---------|
| 1️⃣ | **⚙️ Settings** | Enter Xbox IP + Dev Mode credentials |
| 2️⃣ | **🌐 Browse** | Explore homebrew catalog |
| 3️⃣ | **📥 Install** | Select app → auto-download, dependency analysis, deploy |
| 4️⃣ | **📦 Installed** | Manage console packages |

## 🏛️ Project Structure

```
XBVault/               # 🧱 Main application
├── Models/            # 📊 Data models
├── ViewModels/        # 🧠 MVVM view models
├── Views/             # 🖥️ Avalonia UI views
├── Services/          # 🔧 Business logic & API clients
├── Controls/          # 🎨 Custom UI controls
├── Assets/            # 🖼️ Icons, fonts, themes
├── Converters/        # 🔄 Value converters
build/                 # 📜 Build scripts
docs/                  # 📚 Documentation
```

## 🗺️ Roadmap

- **Phase 0** ✅ Project scaffold, Blades theme, navigation
- **Phase 1** 🔄 Xbox connection & settings
- **Phase 2** 🔄 Emulation Revival catalog browsing
- **Phase 3** 🔄 Install/uninstall workflow
- **Phase 4** 🔄 Polish & release

See [docs/PLAN.md](docs/PLAN.md) for details.

## 🧰 Tech Stack

| Layer | Technology |
|-------|-----------|
| ⚙️ Runtime | .NET 8 |
| 🖥️ UI Framework | Avalonia UI 11 |
| 🏗️ Architecture | MVVM (CommunityToolkit.Mvvm) |
| 🌐 HTML Parsing | HtmlAgilityPack |
| 📡 API | Xbox Device Portal API |

## 🎨 Icons

Icons by [Icons8](https://icons8.com) (3d-fluency & fluency styles), [Microsoft FluentUI Emoji](https://github.com/microsoft/fluentui-emoji), and [KyleBing retro console icons](https://github.com/KyleBing/retro-game-console-icons).

See [docs/ATTRIBUTIONS.md](docs/ATTRIBUTIONS.md) for full attribution.

## 📄 License

GNU General Public License v3.0 — see [LICENSE](LICENSE) for details.

---

<p align="center">
  <sub>⚠️ Not affiliated with Microsoft, Xbox, or Emulation Revival.</sub>
</p>

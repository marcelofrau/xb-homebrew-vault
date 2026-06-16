# Architecture

## Overview

XB Homebrew Vault uses the **MVVM** pattern with **CommunityToolkit.Mvvm** and **Avalonia UI**. The application is a Windows desktop client that communicates with an Xbox console in Developer Mode via the Windows Device Portal (WDP) REST API.

## Layered Architecture

```
┌─────────────────────────────────────────────────┐
│  Views (Avalonia XAML)                          │
│  BrowseView  InstalledView  SettingsView         │
│  MainWindow  SplashWindow                       │
├─────────────────────────────────────────────────┤
│  ViewModels (CommunityToolkit.Mvvm)             │
│  MainViewModel  BrowseViewModel                 │
│  InstalledViewModel  SettingsViewModel          │
├─────────────────────────────────────────────────┤
│  Services                                       │
│  EmulationRevivalService  XboxDeviceService     │
│  SettingsService  CryptoService                 │
├─────────────────────────────────────────────────┤
│  Models                                         │
│  CatalogItem  InstalledPackage                  │
│  XboxConnection  AppSettings                    │
└─────────────────────────────────────────────────┘
```

## Data Flow

```
User Action (View)
    → ViewModel (command)
        → Service (HTTP/scrape)
            → Model (data)
        ← Model
    ← ObservableProperty update
← UI renders
```

## Xbox WDP API Integration

The `XboxDeviceService` communicates with the Xbox Developer Mode Device Portal:

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/os/info` | GET | Device info, connection test |
| `/api/app/packagemanager/packages` | GET | List installed packages |
| `/api/app/packagemanager/package` | POST | Install package |
| `/api/app/packagemanager/package` | DELETE | Uninstall package |

Authentication: HTTP Basic Auth (username/password set in Xbox Dev Mode).

## Emulation Revival Scraping

The `EmulationRevivalService` fetches the catalog from 7 static HTML pages:

- `/xbox-dev-mode/emulators.html`
- `/xbox-dev-mode/frontends.html`
- `/xbox-dev-mode/ports.html`
- `/xbox-dev-mode/apps.html`
- `/xbox-dev-mode/experimental-apps.html`
- `/xbox-dev-mode/media-apps.html`
- `/xbox-dev-mode/utilities.html`

Each page contains card elements parsed via HtmlAgilityPack into `CatalogItem` models.

## Settings Persistence

`SettingsService` stores configuration JSON in `%APPDATA%/XBVault/settings.json`. Passwords are obfuscated using salt+XOR+Base64 via `CryptoService`.

## Navigation

`MainWindow` uses a sidebar with `ListBox` selection bound to `MainViewModel.SelectedTab`. Three views are stacked in the content area with visibility toggled by boolean properties (`IsBrowseActive`, `IsInstalledActive`, `IsSettingsActive`).

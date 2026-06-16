# Development Plan

## Versioning

Semantic Versioning (SEMVER): `MAJOR.MINOR.PATCH`

Release assets: `XBVault-v{VER}-win-{ARCH}.zip`

## Phases

### Phase 0 — Scaffold Initial (current)
- GitHub repo + CI
- Avalonia project structure
- Xbox 360 Blades theme
- Splash screen with placeholder
- Sidebar navigation (Browse, Installed, Settings)
- Empty views with placeholders
- Models, services stubs
- Build scripts + docs

### Phase 1 — Connection & Settings
- Xbox connection configuration UI (Address, Username, Password)
- Obfuscation service (salt+XOR+Base64)
- Settings persistence (JSON in %APPDATA%)
- XboxDeviceService: test connection, list packages
- Connection status indicator

### Phase 2 — Emulation Revival Catalog
- HtmlAgilityPack scraper for all 7 pages
- Cache parsed results locally
- BrowseView with search, category filter, compatibility filter
- Card grid displaying catalog items
- AppDetailView with full info + install button

### Phase 3 — Package Management
- Install flow: download → cache → analyze → upload to Xbox
- Support for .msix/.appx standalone and .zip with dependencies
- Progress bars (download + install)
- InstalledView with package list, sizes, uninstall
- Cache management

### Phase 4 — Polish & Release
- Error handling (offline, timeout, parse failures)
- Cache invalidation
- Edge cases (duplicate installs, version comparison)
- Accessibility pass
- First public release (v0.1.0)

## Build & Release

```
# Development
.\build\run.ps1

# Release
.\build\build-release.ps1 -Version 0.1.0 -Arch x64
# Outputs: dist\XBVault-v0.1.0-win-x64.zip
```

CI/CD via GitHub Actions: build on every push, release on `v*` tag.

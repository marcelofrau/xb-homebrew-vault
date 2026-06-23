# Roadmap

## Version history

```mermaid
timeline
    title XB Homebrew Vault releases
    v0.1 : Scaffold, theme, splash, sidebar, build scripts
    v0.2 : Connection wizard, settings, obfuscation
    v0.5 : Avalonia 12 migration, button theming
    v0.7 : Accurate dial-up detection, FileExplorer, Tools skeleton
    v0.8 : Launch/suspend packages, crash dumps, network info
    v0.8.1 : Connection link speed detection
    v0.8.2 : Pipe fix, running state indicator
    v0.8.3 : Window border, dead code cleanup, docs overhaul, OpenAPI spec
```

## Released features

```mermaid
gantt
    title Feature delivery per version
    dateFormat  YYYY-MM-DD
    section Connection
    Connection wizard            :done, 2026-01, 2026-02
    Connection guard             :done, 2026-04, 2026-04
    Link speed detection         :done, 2026-06, 2026-06
    section Catalog
    Emulation Revival scraper    :done, 2026-02, 2026-03
    Category/compat filters      :done, 2026-03, 2026-03
    Item details                 :done, 2026-03, 2026-03
    section Package Management
    Install/uninstall            :done, 2026-03, 2026-04
    Custom install wizard        :done, 2026-04, 2026-05
    Launch/suspend/terminate     :done, 2026-06, 2026-06
    Running state indicator      :done, 2026-06, 2026-06
    section Tools
    Processes (list/kill)        :done, 2026-05, 2026-06
    Network info                 :done, 2026-05, 2026-06
    System info                  :done, 2026-05, 2026-06
    Crash dumps                  :done, 2026-06, 2026-06
    Screenshot                   :done, 2026-05, 2026-06
    Performance monitor          :done, 2026-05, 2026-06
    File explorer                :done, 2026-06, 2026-06
    section UI
    Xbox 360 Blades theme        :done, 2026-01, 2026-01
    Black window border          :done, 2026-06, 2026-06
```

## Current state

**Version:** v0.8.3 (bugfix)

The app is feature-complete for daily homebrew management on Xbox Dev Mode.

## Future ideas

```mermaid
gantt
    title Ideas for v0.9 / v1.0+
    dateFormat  YYYY-MM-DD
    section Tech debt
    Extract XboxDeviceService    : 2026-07, 14d
    Theme gradient as resource   : 2026-07, 3d
    ConfigureAwait pass          : 2026-07, 3d
    async void fix               : 2026-07, 2d
    section Companion
    Companion UWP app            : 2026-08, 21d
    USB media explorer           : 2026-08, 14d
    DVD/CD drive access          : 2026-08, 14d
    Extend file system access    : 2026-08, 10d
    section Ecosystem
    Community catalog            : 2026-09, 21d
    Dependency resolver          : 2026-09, 7d
    Version checker enhanced     : 2026-09, 5d
    section Features
    Storage analyzer             : 2026-09, 10d
    System health checks         : 2026-09, 7d
    Scheduled tasks              : 2026-09, 10d
    Enhanced log viewer          : 2026-09, 5d
    Game clip/screenshot manager : 2026-10, 14d
    section Cross-platform
    Linux build + CI             : 2026-10, 14d
    macOS build + CI             : 2026-10, 14d
```

### v0.9 — Tech debt & polish

- Break `XboxDeviceService` into smaller domain services
- Theme gradient + close button extracted as `StaticResource` / UserControl
- `ConfigureAwait(false)` audit
- Remove `async void` from code-behinds

### v1.0 — Companion app & ecosystem

**Companion UWP (runs on Xbox in Dev Mode):**
- USB media explorer — navigate pendrives/HDs, copy ROMs/saves
- DVD/CD drive access — read disc content, extract files
- Extended file system access — beyond WDP (`X:\`, `D:\`, restricted paths)

**Community & automation:**
- Community catalog — curated homebrew repo, click-to-install
- Dependency resolver — auto-install VC++ runtimes, .NET, etc.
- Enhanced version checker — compare installed vs catalog, 1-click update
- Scheduled tasks — recurring restart/shutdown/scrape/backup

**Tools:**
- Storage analyzer — pie chart per-app, temp/cache cleanup
- System health checks — ping, latency, storage, memory overview
- Enhanced log viewer — Xbox real-time logs, filter, search, export
- Game clip/screenshot manager — download captures from Xbox

### v?.?.? — Mid/long term

- **Cross-platform**: Linux + macOS (see `CROSS-PLATFORM-PORTING.md`)
- **Media player streaming**: play Xbox media on PC over network
- **Xbox Remote Play**: stream Xbox screen to PC (ultimate goal)

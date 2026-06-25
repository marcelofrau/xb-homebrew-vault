---
layout: default
title: Code Structure Analysis (Internal)
---

# Code Structure Analysis
## Internal Working Document

> **Note:** This is an internal analysis document for comprehensive documentation. Not published on the site.

---

## Executive Summary

XB Homebrew Vault is a .NET 8 + Avalonia 12 MVVM desktop application with ~8-10k LOC across Services, ViewModels, and Views. The architecture is layered (Views → ViewModels → Services → Models) with a predominant "god class" pattern in `XboxDeviceService`.

**Key findings:**
- 1 critical god class (XboxDeviceService)
- 14 documented tech debts, potentially 16+ when verified against actual code
- **11 `async void` event handlers** (higher than documented 4)
- ~82-100 `await` calls with no `.ConfigureAwait(false)` in Services
- Manual service instantiation in App.axaml.cs (no DI container)
- Generally good error logging in Services, with 14 bare `catch { }` blocks

---

## Service Layer Analysis

### XboxDeviceService.cs
**Metrics:**
- **Lines of code:** 1,207 (actual, vs ~1,038 estimated)
- **Public methods/properties:** 35

**Domain Breakdown (35 methods):**
- Connection management: 6
- Package management: 9
- Process management: 2
- Crash dumps: 4
- Network configuration: 4
- System operations (restart, shutdown): 3
- Screenshot: 1
- WebSocket (performance): 1

**Responsibilities (Mixed domains):**
1. **Package Management** — install, uninstall, launch, list
2. **Process Management** — list, kill, get running title
3. **Crash Dumps** — list, delete, enable/disable crash control
4. **Network Configuration** — get network config, WiFi interfaces/networks
5. **System Information** — get system info, restart, shutdown, screenshot
6. **Performance Streaming** — WebSocket connection for real-time metrics
7. **Authentication** — CSRF token handling, HTTP Basic auth
8. **SFTP Credentials** — SMB password fetch, SSH connection info

**Architecture Issues:**
- Mixes HTTP REST calls, WebSocket handling, JSON parsing, and logging
- No domain separation → makes testing, navigation, and refactoring difficult
- Tight coupling: ViewModels call all 35 methods directly

**Error Handling:**
- 2 bare `catch { }` blocks (lines 422, 621-623)
- ~50+ missing `.ConfigureAwait(false)` on await calls
- Generally good logging in catch blocks when exceptions are caught

**Key Patterns:**
```csharp
// Certificate validation bypass (line 32-33)
ServerCertificateCustomValidationCallback = (_, _, _, _) => true,

// HTTP client recreation on reconfigure (lines 51-68)
// Rationale: BaseAddress freezes after first request in Avalonia/HttpClient

// CSRF token handling via cookie container
// Tokens are managed internally, attached to requests automatically
```

---

### PackageInstallService.cs
**Metrics:**
- Well-structured, single responsibility
- Depends on: CacheService, XboxDeviceService

**Key Patterns:**
- Download + dependency analysis + install orchestration
- Regex-based dependency classification:
  - `DepPattern`: Identifies Microsoft, VCLibs, runtime packages
  - `JunkPattern`: Filters out certs, PS scripts, diagnostics
  - `InstallerExts`: `.appx`, `.msix`, `.appxbundle`, `.msixbundle`
- Cache-aware: Reuses downloads, validates before install
- **Interesting detail:** Dependency folder detection (`Dependencies/`, `deps/`, `dep/`) with case-insensitive matching

---

### SftpService.cs
**Metrics:**
- Well-implemented, **implements `IDisposable`** ✓
- Depends on: SSH.NET (Renci.SshNet)

**Key Patterns:**
```csharp
// Connection wrapping in Task.Run for non-blocking UI
public async Task ConnectAsync(string host, int port, string user, string pass)
{
    await Task.Run(() => {
        // SSH.NET is synchronous, so wrap in Task.Run
        _ssh = new SshClient(connInfo);
        _ssh.Connect();
        _sftp = new SftpClient(connInfo);
        _sftp.Connect();
    });
}

// Proper resource cleanup
public void Dispose() { /* handles both SSH and SFTP cleanup */ }
```

**Timeouts & Keep-Alive:**
- SFTP OperationTimeout: 15 seconds
- KeepAliveInterval: 30 seconds
- Prevents dropped connections during long operations

---

### CatalogApiService.cs
**Metrics:**
- Single responsibility: fetch + parse catalog
- Cache: 6-hour TTL, persisted to disk

**Key Patterns:**
```csharp
// JSON API endpoint (line 20)
const string JsonApiUrl = "https://emulationrevival.github.io/api/catalog.json";

// Cache location
%APPDATA%\XBVault\cache\catalog-api.json

// Fallback strategy: API fails → try stale cache
// Ensures offline functionality
```

**Generated Regex:**
```csharp
[GeneratedRegex(@"microsoft\.|vclibs|\.net\.core|ui\.xaml|windowsappsdk|directx|webview2",
    RegexOptions.IgnoreCase | RegexOptions.Compiled)]
```

---

## Error Handling Issues

### Bare `catch { }` Blocks Found
**Total in Services:** 14 instances

| File | Line | Context | Severity |
|------|------|---------|----------|
| XboxDeviceService.cs | 422 | `TryParseError()` JSON parse | Medium |
| XboxDeviceService.cs | 1140 | WebSocket close teardown | Low (intentional cleanup) |
| CryptoService.cs | 41 | Decryption failure | Medium |
| AdminHelper.cs | 19 | Elevation check | Low |
| Logger.cs | 125-127 | Logger internal failures | High (error in error logger!) |
| PackageInstallService.cs | 1 | Likely in cleanup paths | TBD |
| SftpService.cs | 2 | SFTP disconnect cleanup | Low (intentional) |

---

## `async void` Event Handlers - EXPANDED LIST

**Total found:** 11 instances (vs 4 documented)

| File | Line | Method | Impact |
|------|------|--------|--------|
| ConnectionWindow.axaml.cs | 37 | `OnConnectionCompleted()` | High — connection result handling |
| ErrorDialog.axaml.cs | 60 | `OnCopyClick()` | High — error clipboard copy |
| FileExplorerView.axaml.cs | 272 | `OnTreeItemExpanded()` | High — file tree expansion |
| FileExplorerView.axaml.cs | 284 | `OnBrowseFilesClick()` | High — file browsing |
| FileExplorerView.axaml.cs | 562 | `OnDropZoneDrop()` | High — drag-drop file handling |
| LogsView.axaml.cs | 41 | `OnCopyClick()` | Medium — logs clipboard copy |
| NetworkInfoWindow.axaml.cs | 15 | `OnLoaded()` | High — window initialization |
| SftpInfoWindow.axaml.cs | 23 | `OnCopyHostClick()` | Low — simple clipboard ops |
| SftpInfoWindow.axaml.cs | 35 | `OnCopyPortClick()` | Low |
| SftpInfoWindow.axaml.cs | 47 | `OnCopyUserClick()` | Low |
| SftpInfoWindow.axaml.cs | 59 | `OnCopyPasswordClick()` | Low |

**Risk:** Unhandled exception in any of these crashes the entire process with no recovery.

---

## ConfigureAwait Analysis

**Services measured:** ~82-100 `await` calls across Services layer
**ConfigureAwait(false) usage:** 0 instances found ❌

**Impact:**
- Each `await` captures the UI synchronization context
- In server/service layers, this is unnecessary and reduces throughput
- Potential for deadlocks if UI thread is blocked

**Recommendation:** Add `.ConfigureAwait(false)` to all `await` in:
- XboxDeviceService (~75-80 awaits)
- PackageInstallService (~5 awaits)
- CatalogApiService (~3 awaits)
- SftpService (~4 awaits)
- Other Services as needed

---

## IDisposable Analysis

### Missing IDisposable
**XboxDeviceService.cs (lines 18-19):**
```csharp
private HttpClient _http;
private HttpClientHandler? _handler;
```

- Holds disposable resources but **does not implement IDisposable**
- Manual disposal on reconfigure (lines 61-67), but no cleanup on app exit
- **Fragile:** Relies on GC finalizer for cleanup

### Properly Implemented
✅ **SftpService.cs** — implements `IDisposable`, properly disposes SSH and SFTP clients

❌ **PerformanceViewModel.cs** — holds `CancellationTokenSource` but does not implement `IDisposable`
- `_cts?.Cancel()` called, but never `_cts?.Dispose()`
- Resource leak: WaitHandle not released until GC finalizer

---

## Hardcoded Magic Delays

**Total instances found:** 23 hardcoded `Task.Delay()` calls

| File | Lines | Values | Purpose |
|------|-------|--------|---------|
| App.axaml.cs | 125 | 2000 | Splash screen duration |
| ConnectionWindow.axaml.cs | 41, 43 | 2000, 1500 | Connection feedback delay |
| RefreshWindow.axaml.cs | 51 | 1500 | Refresh completion delay |
| LogsView.axaml.cs | 58 | 2000 | Log display delay |
| BrowseViewModel.cs | 564 | 3000 | Browse result display |
| UsbPermissionViewModel.cs | 227 | 1000 | USB permission dialog delay |
| RefreshViewModel.cs | 69 | 200 | Refresh status update |
| SettingsViewModel.cs | 77 | 3000 | Settings save feedback |
| XboxDeviceService.cs | 572, 587 | 2000, 3000 | Package manager polling |
| ConnectionViewModel.cs | 105-172 | 300-600 | Animation timing (11 values) |

**Recommendation:** Extract as named constants in appropriate classes or a `TimingConstants` class.

---

## Service Composition in App.axaml.cs

**Metrics (from agent analysis):**
- **Total lines:** 497
- **InitAfterSplashAsync method:** 384 lines (single massive method!)
- **Services instantiated:** 4 (XboxDeviceService, CacheService, PackageInstallService, SftpService)
- **Windows/Dialogs wired:** 22 dialogs registered dynamically
- **Bare catch blocks:** 2 (lines 107, 110)

**Pattern:**
```csharp
// Manual composition (no DI container)
var xboxService = new XboxDeviceService();
var cacheService = new CacheService();
var installService = new PackageInstallService(cacheService, xboxService);
var sftpService = new SftpService();

// 22 dialog registrations:
dialogCoordinator.Register<ConnectionWindow>(
    () => new ConnectionWindow(),
    connectionVm => connectionWindow.DataContext = connectionVm);
// ... repeated 22 times
```

**Complexity:**
- 384-line `InitAfterSplashAsync` does: dialog registration, service initialization, event wiring, splash close
- Becomes harder to test and maintain as new features added

---

## ViewModel Patterns

**Quick observations:**
- Using CommunityToolkit.Mvvm source generators (ObservableProperty, RelayCommand)
- Generally good separation of concerns

**Size Analysis (from agent):**
- **BrowseViewModel:** 580 lines (TBD if same as "499 lines" — needs verification)
  - 7 main responsibilities: catalog loading, filtering, search, item selection, install orchestration, image thumbnails, selection management
  - ❌ Creates CatalogApiService inline (line 40) — not injected
  - **Issue:** Approaching god-class threshold

**CatalogApiService Injection Issue:**
```csharp
// In BrowseViewModel constructor
public BrowseViewModel(...)
{
    _catalogService = new CatalogApiService();  // PROBLEM: inline creation
}
```

**Consequences:**
- Multiple CatalogApiService instances if multiple BrowseVMs created
- Cache not shared properly
- Violates dependency injection pattern
- **Tech debt #12**

---

## Async/Await Patterns

**Current approach in ViewModels:**
- Fire-and-forget patterns via `async void` event handlers
- No exception catching → exceptions crash app

**ConfigureAwait approach:**
- Not used anywhere in codebase
- ViewModels should probably NOT use `.ConfigureAwait(false)` (UI updates needed)
- Services SHOULD use `.ConfigureAwait(false)` (no UI dependency)

---

## Next Steps

- [ ] Verify all findings against actual code
- [ ] Get method-by-method breakdown from code-analysis agent
- [ ] Create decision records for each integration pattern
- [ ] Document MVVM patterns in detail
- [ ] Create enhancement recommendations for each doc

---

**Document version:** 1.0  
**Last updated:** 2026-06-25  
**Status:** In progress (awaiting agent analysis)

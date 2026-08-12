# Split XboxDeviceService

**Impact:** High | **Effort:** Medium | **Suggested priority:** Phase 1

## Status: **DONE (Aug 2026)** — facade deleted, interfaces + VM migration complete, .NET 10 migration landed. Tests: 172 green. Architecture now documents the split services (`docs/architecture.md`).

## Problem

`XboxDeviceService` had **1,433 lines** (verified v1.2.0, Aug 2026), ~41 public members, cyclomatic complexity 205, and handled distinct responsibilities:

- Authentication (CSRF, Basic Auth, cookies)
- Package management (install, uninstall, launch, suspend, terminate)
- Process management (list, kill)
- Screenshot capture
- System info
- Network info
- Performance monitoring (WebSocket)
- Crash dumps
- Console power (restart, shutdown)

One class, many reasons to change.

## Proposal

### Proposed split

| New Service | Responsibilities | # Methods (current) |
|---|---|---|
| `XboxAuthService` | Login, CSRF, cookies, `Configure()` | 4 |
| `XboxPackageService` | List, install, uninstall, launch, suspend, terminate | 8 |
| `XboxProcessService` | List processes, kill, running title | 3 |
| `XboxSystemService` | System info, restart, shutdown, crash dumps, crash control | 7 |
| `XboxNetworkService` | Network config, WiFi interfaces, networks | 3 |
| `XboxPerformanceService` | WebSocket performance, screenshot | 2 |
| `XboxResponseParser` | (added) pure static JSON/error helpers extracted from the god class | 10 |

### Completed (split #1, Aug 2026)

1. **`XboxAuthService`** created — owns `_handler`, `_http`, `_csrfToken`, `_baseUrl`, `_username`, `_password`, `_configured`, `_connected`, `ConnectionChanged`. Exposes `Http`, `CsrfToken`, `BaseUrl`, `GetWsBaseUrl()`, `ReadResponseBody`, `PostWithCsrfAsync`, `DeleteWithCsrfAsync` (internal) so domain services use the authenticated client.
2. **Domain services created** — `XboxPackageService`, `XboxProcessService`, `XboxSystemService`, `XboxNetworkService`, `XboxPerformanceService`. Each receives `XboxAuthService` via constructor.
3. **`XboxResponseParser`** — static helpers (`TryParseError`, `ParseMsixPackageName`, `IsIdleCode`, `IsSignatureError`, `IsResourceInUseError`, `IsHigherVersionError`, `IsFatalDeploymentError`, `IsJsonSuccess`, `Truncate`, `SizeFormat`) moved here, culture-safe.
4. **Shared types centralized** — `PackagesResponse`, `SshConnectionInfo`, `ConnectionTestResult` in `Models/XboxSharedTypes.cs`.
5. **`XboxDeviceService` → facade** (170 lines) — delegates to the domain services; keeps `ConnectionChanged` + static helper delegates so ViewModels and the 28 helper-test call sites compile unchanged.
6. **Verified:** `dotnet build` 0 warnings/0 errors; **147 tests green**.

### Completed (split #2, Aug 2026)

1. **Interfaces created** — `IXboxAuthService` (extends `IDisposable`; `ConnectionChanged`, `IsConfigured`, `IsConnected`, `SmbPassword`, `Host`, `Configure`, `GetSshCredentials`, `FetchSmbPasswordAsync`, `GetDevPortalUrl`, `MarkConnected`, `Disconnect`, `TestConnectionAsync`), `IXboxPackageService` (8), `IXboxProcessService` (3), `IXboxSystemService` (8), `IXboxNetworkService` (3), `IXboxPerformanceService` (1). All 6 concrete services implement their interface.
2. **16 ViewModels migrated** — each injects the specific `IXbox*` services it needs (auth + domain; `CustomInstall`/`NetworkInfo`/`Processes`/`Screenshot` are single-service).
3. **`PackageInstallService`** — takes `IXboxPackageService` instead of the facade.
4. **`App.axaml.cs` composition root** — `new XboxAuthService()` + 5 domain services (`new Xbox...Service(authService)`); `InitAfterSplashAsync` takes concrete services; all 20 VM creation points rewired; direct `xboxService.` uses → `authService.`.
5. **Facade deleted** — `Services/XboxDeviceService.cs` removed.
6. **Tests retargeted** — `XboxDeviceServiceHelperTests` → `XboxResponseParserTests` (`XboxDeviceService.` → `XboxResponseParser.`).
7. **Verified:** `dotnet build` 0 warnings/0 errors; **147 tests green**.

### Strategy

1. **Extract `XboxAuthService`** — manages `_handler`, `_http`, `_csrfToken`, `_baseUrl`, `_username`, `_password`, `_configured`, `_connected`. Other services receive `XboxAuthService` via DI and use authenticated `HttpClient`. ✅ done
2. **Create interfaces** — `IXboxPackageService`, `IXboxProcessService`, etc. Allows mocking in tests. ✅ done (split #2)
3. **Keep `XboxDeviceService` as facade** (optional) — backward compatibility, delegates calls, then remove. ✅ done
4. **Centralize shared types** — `PackagesResponse`, `SshConnectionInfo`, `ConnectionTestResult` go to `Models/`. ✅ done

### Dependencies
- Before or together with [DI Container](di-container.md)
- **Preferably after** [Testing](testing-infrastructure.md) — tests first so the split is verifiable

### Files to create
- `Services/XboxAuthService.cs` ✅
- `Services/XboxPackageService.cs` ✅
- `Services/XboxProcessService.cs` ✅
- `Services/XboxSystemService.cs` ✅
- `Services/XboxNetworkService.cs` ✅
- `Services/XboxPerformanceService.cs` ✅
- `Services/XboxResponseParser.cs` ✅ (static helpers)
- `Models/XboxSharedTypes.cs` ✅
- `Services/IXboxAuthService.cs` ✅
- `Services/IXboxPackageService.cs` ✅
- `Services/IXboxProcessService.cs` ✅
- `Services/IXboxSystemService.cs` ✅
- `Services/IXboxNetworkService.cs` ✅
- `Services/IXboxPerformanceService.cs` ✅

### Files to modify
- `Services/XboxDeviceService.cs` → reduce to facade, then remove. ✅ facade (170 lines); ✅ removed in split #2
- All ViewModels using `XboxDeviceService` directly → inject specific services. ✅ split #2
- `App.axaml.cs` (composition root) → register new services. ✅ split #2

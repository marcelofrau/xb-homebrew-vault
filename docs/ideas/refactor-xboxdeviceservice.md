# Split XboxDeviceService

**Impact:** High | **Effort:** Medium | **Suggested priority:** Phase 1

## Problem

`XboxDeviceService` has 1289 lines, 23 public methods, 12 private methods, 2 silenced warning pragmas, and handles distinct responsibilities:

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
| `XboxPackageService` | List, install, uninstall, launch, suspend, terminate | 6 |
| `XboxProcessService` | List processes, kill | 2 |
| `XboxSystemService` | System info, restart, shutdown, crash dumps, crash control | 6 |
| `XboxNetworkService` | Network config, WiFi interfaces, networks | 3 |
| `XboxPerformanceService` | WebSocket performance, screenshot | 2 |

### Strategy

1. **Extract `XboxAuthService`** — manages `_handler`, `_http`, `_csrfToken`, `_baseUrl`, `_username`, `_password`, `_configured`, `_connected`. Other services receive `XboxAuthService` via DI and use authenticated `HttpClient`.
2. **Create interfaces** — `IXboxPackageService`, `IXboxProcessService`, etc. Allows mocking in tests.
3. **Keep `XboxDeviceService` as facade** (optional) — backward compatibility, delegates calls, then remove.
4. **Centralize shared types** — `PackagesResponse`, `SshConnectionInfo`, `ConnectionTestResult` go to `Models/`.

### Dependencies
- Before or together with [DI Container](di-container.md)
- Ideally before [Testing](testing-infrastructure.md) to avoid double work

### Files to create
- `Services/XboxAuthService.cs`
- `Services/XboxPackageService.cs`
- `Services/XboxProcessService.cs`
- `Services/XboxSystemService.cs`
- `Services/XboxNetworkService.cs`
- `Services/XboxPerformanceService.cs`
- `Services/IXboxAuthService.cs`
- `Services/IXboxPackageService.cs`
- `Services/IXboxProcessService.cs`
- `Services/IXboxSystemService.cs`
- `Services/IXboxNetworkService.cs`
- `Services/IXboxPerformanceService.cs`

### Files to modify
- `Services/XboxDeviceService.cs` → reduce to facade, then remove
- All ViewModels using `XboxDeviceService` directly → inject specific services
- `Program.cs` → register new services

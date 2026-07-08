# Testing Infrastructure

**Impact:** High | **Effort:** Medium | **Suggested priority:** Phase 1

## Problem

Zero tests in the project. Services with critical logic (PackageInstallService 500 lines, XboxDeviceService 1289 lines) operate without coverage. Refactoring and new features depend on manual testing.

## Proposal

### 1. Test project setup
- New xUnit project: `tests/XBVault.Tests/XBVault.Tests.csproj`
- Target `net8.0`, references `XBVault.csproj`
- Packages:
  - `xunit`, `xunit.runner.visualstudio`
  - `NSubstitute` (simple mocking)
  - `FluentAssertions` (readable assertions — optional)
  - `Microsoft.Extensions.DependencyInjection` (to test composition root)

### 2. What to test first

**High priority (business rules + parsing):**
- `PackageInstallService.ClassifyPackages()` — filter junk, identify dependencies, choose main vs bundle
- `PackageInstallService.ExtractPackage()` — ZIP extraction, nested bundles
- `PackageInstallService.GetInstallableFiles()` — directory scan
- `PackageOverrideService.TryGetCatalogId()` — lookup by PFN and name, merge embedded + remote
- `CacheService` — get/set/clear, `GetCacheSizeBytes()`

**Medium priority (services with mocked I/O):**
- `XboxDeviceService.TestConnectionAsync()` — success, timeout, HTTP error, cancellation
- `XboxDeviceService.GetInstalledPackagesAsync()` — JSON parsing, empty packages, error
- `XboxDeviceService.InstallPackageAsync()` — retry flow, rate limiting, progress
- `XboxDeviceService.ConnectPerformanceWsAsync()` — WebSocket, parse snapshot

**Low priority (ViewModels):**
- `MainViewModel` — state transitions (disconnected → connected → disconnected)
- `BrowseViewModel` — filters, search, loading state
- `CustomInstallViewModel` — URL validation, file analysis

### 3. Approach
- Unit tests: mock dependencies (`HttpClient`, `IHttpClientFactory`, `Stream`)
- Integration tests (future): connect to real Xbox or local HTTP stub
- CI: add `dotnet test` to `build.yml` workflow

### 4. Risks
- `XboxDeviceService` uses `HttpClient` created internally (not injected) — needs refactoring to `IHttpClientFactory` or making it `virtual` for mocking
- `PackageInstallService` has `static` methods preventing mocking — need to become instance methods or accept `Func`/interfaces

### 5. Dependencies
- None until test project is added
- Minor refactoring in `XboxDeviceService` (inject `HttpClient`)
- `PackageInstallService` needs to become a normal class (remove static)

### 6. Files to create
- `tests/XBVault.Tests/XBVault.Tests.csproj`
- `tests/XBVault.Tests/Services/PackageInstallServiceTests.cs`
- `tests/XBVault.Tests/Services/CacheServiceTests.cs`
- `tests/XBVault.Tests/Services/PackageOverrideServiceTests.cs`
- `tests/XBVault.Tests/Usings.cs`

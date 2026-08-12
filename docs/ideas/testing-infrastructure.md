# Testing Infrastructure

**Impact:** High | **Effort:** Medium | **Suggested priority:** Phase 1 (before god-class refactors)

## Problem

Zero tests in the project. All 110+ code files ship without automated coverage. Critical logic — `PackageInstallService` (514 lines), `XboxDeviceService` (1,433 lines, complexity 205), `FileExplorerViewModel` (1,880 lines, complexity 254) — operates unverified. The planned god-class splits ([Tech Debt #1/#2](tech-debt)) are unsafe without a test net to catch regressions. (Status Aug 2026: Phase 1a + 1b + 1c shipped — 147 tests green on pure services + god-class helpers. God-class instance logic still uncovered.)

> **Update (Aug 2026):** God-class split landed — `XboxDeviceService` is deleted, replaced by 6 domain services (`XboxAuthService`, `XboxPackageService`, `XboxProcessService`, `XboxSystemService`, `XboxNetworkService`, `XboxPerformanceService`) with interfaces, and the project migrated to .NET 10. Suite now **172 tests green**. Phase 2/3 below is unblocked.

## Testability Assessment (verified v1.2.0, Aug 2026)

| Component | Testable now? | Blockers |
|-----------|---------------|----------|
| `CryptoService` | ✅ Yes | none — pure static, no I/O |
| `PackageInstallService` parsing (`ExtractPackage`, `ExtractBundles`, `ClassifyPackages`, `FilterByArchitecture`) | ✅ Yes | none — static pure methods; file I/O via temp dirs |
| `CacheService` | ✅ Yes | file I/O only — use temp dir |
| `PackageOverrideService.TryGetCatalogId` | ✅ Yes | no network for embedded overrides; remote needs HTTP mock — done via `HttpClient` inject + internal seams |
| `UpdateVersionCache` | ✅ Yes | file I/O only |
| `GitHubReleaseCheckerService` | ✅ Yes | `HttpClient` injectable via ctor (done) |
| `SettingsService` | ✅ Yes | internal `SaveTo`/`LoadFrom` path seams (done); static `Current` singleton untouched |
| `CatalogApiService` (parse + cache TTL) | 🟡 Needs seam | uses `HttpClient` internally; parsing is testable if exposed |
| `XboxDeviceService` | 🟡 Needs HTTP seam | `HttpClient`/`HttpClientHandler` created in ctor (line 36–44) and `Configure` — no `IHttpClientFactory`, no `virtual` seam. Pure helpers now covered (Phase 1c). |
| `FileExplorerViewModel` | 🔴 Needs refactor | 1,880 lines; SFTP + drive + path logic entangled |
| ViewModels (general) | 🟡 Partial | CommunityToolkit `ObservableObject` works off-UI-thread; but constructors reach into static `SettingsService` |

## Proposal

### 1. Test project setup
- New xUnit project: `tests/XBVault.Tests/XBVault.Tests.csproj`
- Target `net10.0`, references `XBVault.csproj`
- Packages (shipped):
  - `xunit` (`2.9.2`), `xunit.runner.visualstudio` (`2.8.2`), `Microsoft.NET.Test.Sdk` (`17.11.1`)
  - No mock library needed so far — HTTP stubbed via custom `StubHttpMessageHandler`. Add `NSubstitute` if ViewModel tests demand it.
- CI: `dotnet test` job added to `.github/workflows/build.yml` (windows-latest + ubuntu-latest, Release config) ✅

### 2. What to test first (by testability, not by size)

**Phase 1a — pure logic (DONE, Aug 2026, 49 tests green):**
- `CryptoService.Obfuscate/Deobfuscate` — round-trip, empty string, salt stability ✅
- `PackageInstallService.ClassifyPackages` — junk filter, dependency detection, main vs bundle ✅
- `PackageInstallService.ExtractPackage` / `ExtractBundles` — ZIP + nested bundle extraction ✅
- `PackageInstallService.GetInstallableFiles` — x64/arm64 architecture filtering ✅
- `CacheService` — get/set/clear, `GetCacheSizeBytes()` ✅
- `UpdateVersionCache` — read/write/persistence/corrupt-file ✅
- Seams added: `CacheService(string? cacheRoot)` + `UpdateVersionCache(string? cacheFilePath)` — default preserves existing behavior, tests inject temp dirs.
- Test file locations: `tests/XBVault.Tests/*Tests.cs` (root, not `Services/`). `TestInitializer.cs` sets `Logger.MinLevel = Fatal` to keep logger quiet in tests.

**Phase 1b — seam-light (DONE, Aug 2026):**
- `GitHubReleaseCheckerService` — inject `HttpClient` via ctor, mock responses ✅
- `PackageOverrideService` — `HttpClient` ctor inject + internal `ParseAndMerge`/`FetchRemoteAsync` seams, temp override JSON ✅
- `SettingsService` — internal `SaveTo(path, settings)`/`LoadFrom(path)` seams (static `Current` untouched), save/load/corrupt-file tests ✅
- Seams added: `GitHubReleaseCheckerService(HttpClient?)`, `PackageOverrideService(HttpClient?)`, `SettingsService.SaveTo/LoadFrom`, `InternalsVisibleTo("XBVault.Tests")`. Bug fixed: `ParseAndMerge` now swallows malformed JSON instead of throwing.
- CI: `dotnet test` job wired into `.github/workflows/build.yml`.

**Phase 2 — after god-class splits (blocked by #1/#2):**
- `XboxPackageService` etc. — mock authenticated `HttpClient`
- `FileExplorerViewModel` — once `SftpUploadService` + path parser extracted

**Phase 1c — god-class helper characterization (DONE, Aug 2026):**
- `XboxDeviceService` 10 pure helpers made `internal static`: `TryParseError`, `ParseMsixPackageName`, `IsIdleCode`, `IsSignatureError`, `IsResourceInUseError`, `IsHigherVersionError`, `IsFatalDeploymentError`, `IsJsonSuccess`, `Truncate`, `SizeFormat` ✅
- `FileExplorerViewModel` 9 pure helpers made `internal static`: `FormatBps`, `InsertSorted`, `UpdateLastChildFlag`, `UpdateChildrenPathsRecursive`, `FindEntry`, `FindParent`, `GetParentPath`, `CollectExpandedPaths`, `ClearTreeCache` ✅
- New test files: `tests/XBVault.Tests/XboxDeviceServiceHelperTests.cs` + `tests/XBVault.Tests/FileExplorerHelperTests.cs` (65 new cases → 147 tests green total).
- **Bugs found + fixed by the new tests:**
  - `UpdateChildrenPathsRecursive` corrupted nested paths (neto ganhava segmento duplicado, ex `\sub\sub\`) — recursão reusava `oldPath` mas substituía com `entry.FullPath` do filho já renomeado. Now takes `oldPath` + `newPath` and rewrites consistently.
  - `SizeFormat`/`FormatBps` used current-culture `:F1` → output differed pt-BR (`1,5KB`) vs CI en-US (`1.5KB`). Now `CultureInfo.InvariantCulture` — deterministic everywhere.
- Same culture bug still latent in other formatters (out of scope): `SftpEntry.FormatSize`, `SystemInfo`, `PackageInstallService`, `ProcessInfo`, `CrashDumpInfo`, `PreFlightChecker`, `UsbDriveDetector`, `SettingsViewModel`. Tracked in [Tech Debt](tech-debt).

**Phase 3 — ViewModels:**
- `MainViewModel` — connection state transitions (disconnected → connected → disconnected)
- `BrowseViewModel` — filters, search, loading state (once `SettingsService` seam exists)
- `CustomInstallViewModel` — URL/file validation

### 3. Approach
- Unit tests: temp dirs for file I/O, NSubstitute for services, `HttpMessageHandler` stub for HTTP
- Integration tests (future): local HTTP stub emulating WDP, or real Xbox on demand
- CI: `dotnet test` on both build jobs

### 4. Risks / refactor prerequisites
- `XboxDeviceService` creates its own `HttpClient` (ctor + `Configure`) — needs `IHttpClientFactory` or a ctor overload accepting `HttpClient`. Do this during the [#1 split](refactor-xboxdeviceservice).
- `SettingsService.Current` static singleton — ViewModels/`App.axaml.cs` reach it directly; needs an injectable instance path.
- `Logger` is static — keep it static (logging is infrastructure); tests assert on returned values, not logs.
- Avalonia `ObservableObject` properties are testable off-UI-thread — no `Dispatcher` needed for simple state assertions.

### 5. Dependencies
- None blocking until Phase 1a (pure logic first).
- Phase 1b needs the small seam refactors above (~2–3h).
- Phase 2 depends on god-class splits (tracked in [Tech Debt](tech-debt)).

### 6. Files
- `tests/XBVault.Tests/XBVault.Tests.csproj` ✅
- `tests/XBVault.Tests/Usings.cs` ✅
- `tests/XBVault.Tests/TestInitializer.cs` ✅
- `tests/XBVault.Tests/StubHttpMessageHandler.cs` ✅ (HTTP stub helper)
- `tests/XBVault.Tests/CryptoServiceTests.cs` ✅
- `tests/XBVault.Tests/PackageInstallServiceTests.cs` ✅
- `tests/XBVault.Tests/CacheServiceTests.cs` ✅
- `tests/XBVault.Tests/UpdateVersionCacheTests.cs` ✅
- `tests/XBVault.Tests/GitHubReleaseCheckerServiceTests.cs` ✅
- `tests/XBVault.Tests/PackageOverrideServiceTests.cs` ✅
- `tests/XBVault.Tests/SettingsServiceTests.cs` ✅
- Phase 2/3 tests: after god-class splits + ViewModel seams

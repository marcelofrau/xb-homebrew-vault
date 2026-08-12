## 1. Baseline

- [x] 1.1 Run `dotnet build XBVault/XBVault.csproj -c Release` and `dotnet test tests/XBVault.Tests/XBVault.Tests.csproj -c Release` on current net8 baseline to confirm green before touching anything
- [x] 1.2 Record any pre-existing warnings to distinguish them from new-SDK warnings

## 2. Retarget

- [x] 2.1 Change `<TargetFramework>` to `net10.0` in `XBVault/XBVault.csproj`
- [x] 2.2 Change `<TargetFramework>` to `net10.0` in `tests/XBVault.Tests/XBVault.Tests.csproj`
- [x] 2.3 Run `dotnet build XBVault/XBVault.csproj -c Release` with the .NET 10 SDK; resolve any new errors/warnings with minimal code changes
- [x] 2.4 Run `dotnet test tests/XBVault.Tests/XBVault.Tests.csproj -c Release`; confirm all tests pass on net10

## 3. CI

- [x] 3.1 Update `.github/workflows/build.yml` `dotnet-version` from `8.0.x` to `10.0.x` in the `build` job
- [x] 3.2 Update `.github/workflows/build.yml` `dotnet-version` to `10.0.x` in the `test` job
- [x] 3.3 Update `.github/workflows/build.yml` `dotnet-version` to `10.0.x` in the `release` job
- [x] 3.4 Update build artifact upload path from `net8.0` to `net10.0`

## 4. Tooling

- [x] 4.1 Update `.vscode/launch.json` debug `program` paths from `net8.0` to `net10.0` (both launch configs)
- [x] 4.2 Check `installer/XBVault.iss` for hard-coded runtime/output assumptions; adjust only if needed

## 5. Docs

- [x] 5.1 Update `AGENTS.md`: target version notes, CI `dotnet-version: 10.0.x`, and fix the stale "No test project exists" note (tests live in `tests/XBVault.Tests`)
- [x] 5.2 Update any docs under `docs/` referencing `net8.0` or .NET 8 as current target (e.g. `docs/ideas/auto-update.md`, `docs/ideas/testing-infrastructure.md`)
- [x] 5.3 Verify no tracked file under `XBVault/`, `tests/`, `.github/`, `.vscode/` references `net8.0`

## 6. Release verification

- [x] 6.1 Run a debug `powershell -File build/run.ps1` smoke test to confirm the app launches on net10
- [x] 6.2 Run `powershell -File build/build-release.ps1 -Version <next> -Arch x64` locally to confirm a self-contained ZIP still produces and runs
- [x] 6.3 Push branch/PR; confirm CI build + test jobs pass on `windows-latest` and `ubuntu-latest`
- [x] 6.4 Confirm next `v*` tag triggers the release job across all 6 RIDs (win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64)

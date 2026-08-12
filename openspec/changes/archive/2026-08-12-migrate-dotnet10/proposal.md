## Why

.NET 8 (LTS) enters end-of-support on November 10, 2026 — ~3 months away. After that it stops receiving security and reliability patches, which is a risk for a desktop app distributed to end users. .NET 9 (STS) is already EOL (May 2026), so the next LTS is .NET 10 (released Nov 2025, supported until Nov 2028). Migrating now gives ~2 more years of support, plus incremental JIT/GC/startup improvements for free.

## What Changes

- Target framework `net8.0` → `net10.0` in `XBVault/XBVault.csproj` and `tests/XBVault.Tests/XBVault.Tests.csproj`.
- Bump CI `.github/workflows/build.yml` `dotnet-version` from `8.0.x` to `10.0.x` in the build, test, and release jobs.
- Update artifacts/launch paths that hard-code `net8.0` output dir (`.vscode/launch.json`, workflow artifact `path`).
- Update `AGENTS.md` build/env notes (dotnet resolution, versions).
- Update docs that reference .NET 8 / support status if they mention it.
- Validate: full build + test suite green on Windows and Linux (CI matrix), release ZIP publish on all 6 RIDs.
- **BREAKING**: none expected. All dependencies (Avalonia 12, SSH.NET, CommunityToolkit.Mvvm 8.4, Serilog 4.x, System.Management 8.0) are netstandard2.0+/net8.0 compatible and run on net10. Any build-time analyzer warnings introduced by newer SDK are resolved.
- Runtime strategy unchanged: releases remain `--self-contained true`, so end users do NOT need a runtime installed.

## Capabilities

### New Capabilities
- `dotnet10-target`: Build, test, and release the application on .NET 10 LTS with no regressions, while keeping self-contained distribution.

### Modified Capabilities
<!-- No existing specs in openspec/specs/ cover target framework or build infrastructure. No requirement changes. -->

## Impact

- **Code**: `XBVault/XBVault.csproj`, `tests/XBVault.Tests/XBVault.Tests.csproj` (TFM bump; code changes expected to be zero or cosmetic).
- **CI**: `.github/workflows/build.yml` (3 jobs reference `8.0.x`).
- **Tooling**: `.vscode/launch.json` (2 paths with `net8.0`), `AGENTS.md`, `docs/`.
- **Packaging**: unchanged — `build/build-release.ps1` / `build/build-release.sh` already `--self-contained true` with `RuntimeIdentifiers` in csproj; no client runtime install needed.
- **Dependencies**: no version bumps required. Optional follow-ups (out of scope): trimming, NativeAOT evaluation, `System.Management` 10.x.
- **Risks**: low. Main unknowns are analyzer/new-SDK warnings and Avalonia 12 runtime behavior on net10 — covered by existing test suite + CI matrix.

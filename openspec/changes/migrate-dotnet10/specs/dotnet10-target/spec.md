## ADDED Requirements

### Requirement: Application targets .NET 10 LTS
The `XBVault` project SHALL target `net10.0` and SHALL build cleanly with the .NET 10 SDK. The project SHALL NOT rely on any .NET 8 EOL behavior. All source code SHALL compile without functional changes to existing features.

#### Scenario: Project targets net10.0
- **WHEN** the `XBVault/XBVault.csproj` is inspected
- **THEN** `<TargetFramework>` SHALL be `net10.0`

#### Scenario: Local build succeeds on .NET 10 SDK
- **WHEN** `dotnet build XBVault/XBVault.csproj -c Release` runs with the .NET 10 SDK
- **THEN** the build SHALL succeed with zero errors

### Requirement: Test suite targets .NET 10
The `XBVault.Tests` project SHALL target `net10.0` and SHALL reference the `XBVault` project at the same TFM. The full test suite SHALL pass on .NET 10.

#### Scenario: Tests retargeted
- **WHEN** the `tests/XBVault.Tests/XBVault.Tests.csproj` is inspected
- **THEN** `<TargetFramework>` SHALL be `net10.0`

#### Scenario: Test suite passes locally
- **WHEN** `dotnet test tests/XBVault.Tests/XBVault.Tests.csproj -c Release` runs with the .NET 10 SDK
- **THEN** all tests SHALL pass with zero failures

### Requirement: CI builds and tests on .NET 10
The CI workflow SHALL install the .NET 10 SDK (`10.0.x`) and SHALL build and test on both `windows-latest` and `ubuntu-latest`. Artifact paths SHALL reference the `net10.0` output directory.

#### Scenario: CI SDK version
- **WHEN** `.github/workflows/build.yml` is inspected
- **THEN** every `dotnet-version` value SHALL be `10.0.x`

#### Scenario: CI artifact path
- **WHEN** `.github/workflows/build.yml` is inspected
- **THEN** the build artifact upload path SHALL reference `net10.0`

### Requirement: Self-contained release distribution preserved
Release publishing SHALL remain `--self-contained true` so end users do not need to install a .NET runtime. The RID matrix (win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64) SHALL be unchanged.

#### Scenario: Release publish stays self-contained
- **WHEN** a tagged release is built via `build/build-release.ps1` or `build/build-release.sh`
- **THEN** the publish command SHALL include `--self-contained true`
- **AND** the resulting ZIP SHALL contain the runtime and run without a .NET installation

### Requirement: Tooling references net10.0
Developer tooling SHALL reference the `net10.0` output directory and SHALL not hard-code `net8.0`.

#### Scenario: VSCode launch paths
- **WHEN** `.vscode/launch.json` is inspected
- **THEN** every debug launch `program` path SHALL reference `net10.0`

### Requirement: Docs reflect .NET 10
Project documentation SHALL state .NET 10 as the target framework and current support status, and SHALL not reference .NET 8 as the active target.

#### Scenario: AGENTS.md updated
- **WHEN** `AGENTS.md` is inspected
- **THEN** it SHALL describe the .NET 10 target and CI `dotnet-version: 10.0.x`

#### Scenario: No stale net8.0 references
- **WHEN** the repository is searched for `net8.0`
- **THEN** no tracked file under `XBVault/`, `tests/`, `.github/`, or `.vscode/` SHALL reference it, except historical changelogs or archived OpenSpec changes

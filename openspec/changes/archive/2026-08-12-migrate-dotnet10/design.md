## Context

XBVault is a .NET 8 (LTS) Avalonia 12 desktop app, self-contained distributed via ZIP per RID (win-x64/arm64, linux-x64/arm64, osx-x64/arm64). Single csproj, no solution. CI in `.github/workflows/build.yml` builds/tests/releases with SDK `8.0.x`. A test suite exists at `tests/XBVault.Tests` (xunit), referenced by CI but not mentioned in AGENTS.md.

.NET 8 EOL is 2026-11-10. .NET 9 (STS) is already EOL. .NET 10 is the current LTS (supported to Nov 2028). The dev machine has SDKs 8.0.4xx, 9.0.3xx, and 10.0.x installed — no global.json pins the SDK.

## Goals / Non-Goals

**Goals:**
- Target .NET 10 LTS for app + tests.
- Keep zero (or near-zero) code changes — this is a retarget, not a rewrite.
- Keep self-contained distribution: no runtime install for end users.
- CI (build/test/release) fully green on net10, Windows + Linux.
- Update tooling/docs that hard-code `net8.0` or `8.0.x`.

**Non-Goals:**
- Dependency version bumps (Avalonia, SSH.NET, Serilog, etc.) unless a build failure forces one.
- Trimming or NativeAOT enablement.
- `global.json` SDK pinning (no requirement; SDKs already present).
- Runtime-install distribution model — remains self-contained.
- Performance benchmarking/regression harness.

## Decisions

**D1: Target `net10.0`, not `net9.0`.**
.NET 9 is STS and already EOL. Skipping it avoids a second migration in under a year. net10 LTS until Nov 2028.
*Alternative considered:* stay on net8 until forced — rejected because EOL is ~3 months away and migrating at the last minute is riskier than doing it while net10 is young.

**D2: Retarget tests to net10.0 alongside the app.**
The test project references the app project directly; mixed TFMs (tests on net8 referencing net10) would trigger NU1201. Keeping them in lockstep is the only clean option.

**D3: Bump CI to `10.0.x`, fix hard-coded `net8.0` paths.**
Three jobs (`build`, `test`, `release`) pin `8.0.x`. Artifact path `XBVault/bin/Release/net8.0/` and `.vscode/launch.json` hard-code the TFM output folder. All must move to `net10.0`.
*Note:* `actions/setup-dotnet` installs SDK 10.0.x which can build net8 targets, but since we retarget, everything becomes net10.

**D4: No `RuntimeIdentifiers`/packaging changes.**
csproj already lists all 6 RIDs and publish scripts use `--self-contained true`. The publish output layout does not change between net8/net10.

**D5: Keep `System.Management` 8.0 (Windows USB detection).**
Package is netstandard2.0; runs unmodified on net10. No bump needed. Optional follow-up: align to 10.x later.

**D6: Validate via existing CI matrix, not ad-hoc machines.**
Build+test on windows-latest and ubuntu-latest covers the port. Release RID matrix exercised on tag push. Local `dotnet build` + `dotnet test` on the dev machine as a first smoke check.

## Risks / Trade-offs

- [New-SDK analyzers emit warnings on net10 that were silent on net8] → Fix or suppress explicitly; CI treats warnings as visible but not errors (no `TreatWarningsAsErrors` currently). Decide case-by-case.
- [Avalonia 12 runtime behavior difference on net10] → Mitigated by existing unit tests (services, parsers, crypto, cache) + manual smoke run via `build/run.ps1`. Avalonia 12 supports net8+; net10 is forward-compatible.
- [Package version caps that exclude net10] → Unlikely (all deps are netstandard2.0/8.0 compatible); if NU1201/NU1701 appears, bump the offending package — decision documented in tasks.
- [Docs/AGENTS.md drift (tests exist but AGENTS says none)] → Fix AGENTS.md note about test project while updating version references.
- [Rollback] → Single-file TFM revert (`net10.0` → `net8.0`), CI revert to `8.0.x`. Low risk since no code changes expected.

## Migration Plan

1. Local smoke: `dotnet build` + `dotnet test` against current net8 baseline (confirm green before touching anything).
2. Bump TFM to `net10.0` in both csproj files; build locally with SDK 10.
3. Resolve any warnings/errors; keep code changes minimal.
4. Run test suite locally on net10.
5. Update `.github/workflows/build.yml` (3 jobs, artifact path) and `.vscode/launch.json`.
6. Update `AGENTS.md` + any docs referencing `net8.0`/support windows.
7. Push; confirm CI build+test green on Windows + Linux.
8. On next `v*` tag, verify release job produces all 6 RID ZIPs.
9. Rollback path: revert TFM + CI changes (single commit history).

## Open Questions

- Does the user want a version bump (1.4.0 → 1.5.0) alongside the migration? Default: no, bump happens on next release tag.
- Installer (`XBVault.iss`): any hard-coded runtime requirement? Checked during implementation; Inno Setup bundles the self-contained publish output, so expected no change.

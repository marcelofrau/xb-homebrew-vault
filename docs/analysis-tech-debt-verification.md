---
layout: default
title: Tech Debt Verification (Internal)
---

# Tech Debt Verification & Analysis
## Internal Document

> **Re-verified against v1.2.0 source — August 2026.** This supersedes the June 2026 verification. All line counts, method counts, and locations were re-measured against the current tree; the summary table reflects the live status of every item.
>
> **⚠️ Post-verification update (Aug 2026):** The `XboxDeviceService` god class item in this report is **RESOLVED** — it was split into 6 domain services and deleted after this verification ran. `FileExplorerViewModel` remains large (~1,736 lines). See current [Tech Debt](tech-debt).

---

## Verification Results

### HIGH SEVERITY

#### 🔴 VERIFIED #1: XboxDeviceService — God Class (WORSE)

| Aspect | June 2026 | **Aug 2026 (v1.2.0)** |
|--------|-----------|------------------------|
| File | XBVault/Services/XboxDeviceService.cs | unchanged |
| **Lines** | 1,207 | **1,433** (+226) |
| **Public members** | 35 | **~41** |
| **Cyclomatic complexity** | not measured | **205** |
| IDisposable | ❌ | ❌ still absent |
| Domains mixed | 8 | 8 |

**Status:** **OPEN** — grew 19% in two months. Split recommendation still valid (XboxPackageService / XboxProcessService / XboxCrashService / XboxNetworkService / XboxSystemService / XboxPerformanceService).

**Effort Estimate:** 6–8 hours

---

#### 🔴 VERIFIED #2 (NEW): FileExplorerViewModel — Undocumented God Class

| Aspect | Finding |
|--------|---------|
| File | XBVault/ViewModels/FileExplorerViewModel.cs |
| **Lines** | **1,880** — largest file in the repository |
| **Cyclomatic complexity** | **254** — highest in the project |
| Responsibilities | SFTP browse/upload (folder + mixed), drive mounting, path parsing, drag-drop, UI state |

**Status:** **OPEN** — untracked until this verification. Surpassed XboxDeviceService as the biggest single file.

**Recommendation:** Split upload pipelines into `SftpUploadService`; extract path normalization; keep VM for state/wiring only.

**Effort Estimate:** 6–8 hours

---

#### ✅ VERIFIED #3: `_Backup/` directory tracked in git — RESOLVED

Removed from tracking, gitignored, deleted. Confirmed no `Assets/_Backup/` in tree.

---

### MEDIUM SEVERITY

#### 🟡 VERIFIED #4: App.axaml.cs — 724 lines, manual composition root (PARTIAL)

| Aspect | June 2026 | **Aug 2026 (v1.2.0)** |
|--------|-----------|------------------------|
| Total lines | 497 | **724** |
| Bare `catch { }` in ShowErrorDialogSafe | 2 (lines 107/110) | **0 — FIXED** (all catch blocks now `Logger.Error`) |
| Manual `new` service wiring | yes | yes (6 services) |
| DI container | none | none (0 `ServiceProvider`/`AddSingleton` in tree) |

**Status:** **PARTIAL** — the silent-catch defect is resolved, but the composition-root bloat grew and remains unaddressed.

**Effort Estimate:** 3–5 hours

---

#### 🟡 VERIFIED #5: No ConfigureAwait(false) Anywhere (WORSE)

| Aspect | June 2026 | **Aug 2026 (v1.2.0)** |
|--------|-----------|------------------------|
| Total await calls | ~82–100 (Services) | **~404** (Services + ViewModels) |
| ConfigureAwait(false) | 0 | **0** |

**Status:** **OPEN** — the surface area quadrupled while the gap stayed.

**Effort Estimate:** 1–2 hours (mechanical)

---

#### 🟡 VERIFIED #6: Silent Exception Swallowing — 26 bare catches

| Category | Count | Verdict |
|----------|-------|---------|
| Intentional (Logger self-protection, Sftp disconnect + trace, Program.cs last-resort, JSON parse guards) | ~15 | ✅ OK |
| **Needs audit** (FileExplorerViewModel:1835, InstalledViewModel:521/543, ErrorDialog:145/157, FileExplorerView:601, BoolToValueConverter:18) | ~7 | ⚠️ flagged in #6 |
| JSON parse guards in XboxDeviceService:770–819 (return false) | 4 | ⚠️ suggest `Logger.Trace` |

**Status:** **PARTIAL** — the 10 originally-documented sites were fixed (v0.9.2), but regressions appeared in code added since.

**Effort Estimate:** 1 hour

---

#### 🟡 VERIFIED #7: async void — 22 instances (11 → 22)

| Aspect | June 2026 | **Aug 2026 (v1.2.0)** |
|--------|-----------|------------------------|
| Instances | 11 | **22** |
| Files | 5 | **11** |
| High-risk (real async work) | 5 | **15** |

New high-risk handlers since June: MainWindow `OnDiscordClick`/`OnDisconnectClick`, BrowseView/InspectorView/InstalledView `OnDrop`, ErrorDialog `OnConnectClick`/`OnDownloadClick`, FileExplorerView uploads (5).

**Status:** **OPEN** — doubled. Highest crash-risk item after the god classes.

**Effort Estimate:** 3–5 hours

---

#### 🟡 VERIFIED #8: IDisposable Not Implemented

| Class | June 2026 | **Aug 2026 (v1.2.0)** |
|-------|-----------|------------------------|
| XboxDeviceService | ❌ | ❌ **still open** |
| PerformanceViewModel | ❌ | ✅ `: IDisposable`, disposes CTS |
| SftpService | — | ✅ `: IDisposable` |
| XrayAgentService | — | ✅ `: IDisposable` |
| PackageOverrideService | — | ✅ `: IDisposable` |
| GitHubReleaseCheckerService | — | ✅ `: IDisposable` |

**Status:** **PARTIAL** — only `XboxDeviceService` remains. See #1.

**Effort Estimate:** 1 hour

---

#### 🟡 VERIFIED #9: Border CornerRadius Does Not Clip Image

**Status:** **OPEN** — re-verified: no `Clip=`/`ImageBrush`/`RectangleGeometry` workaround in BrowseView/ItemDetailWindow. Avalonia 12.0.0 limitation stands.

**Effort Estimate:** 2–3 hours

---

#### 🟡 VERIFIED #10 (NEW): ViewModels Past the God-Class Threshold

| File | Lines | Complexity |
|------|-------|-----------|
| FileExplorerViewModel | **1,880** | **254** |
| BrowseViewModel | **899** | 167 |
| CustomInstallViewModel | 726 | 98 |
| InstalledViewModel | 632 | 112 |
| InspectorViewModel | 545 | 72 |

**Status:** **OPEN** — BrowseViewModel alone grew 580 → 899 since June.

**Effort Estimate:** 4–8 hours across the set

---

#### ✅ VERIFIED #11–12: TitleGradient + WindowCloseButton — RESOLVED (v0.9.1)

Confirmed shared resources in `BladesTheme.axaml`.

---

### LOW SEVERITY

#### ✅ VERIFIED #13: Magic Delays — MOSTLY RESOLVED

Named constants cover the bulk. **2 stragglers:** `DialogFadeBehavior.cs:53` (200), `CustomInstallViewModel.cs:550/566` (1500).

**Effort Estimate:** 15 min

---

#### ✅ VERIFIED #14: CatalogApiService injection — RESOLVED

`BrowseViewModel.cs:50` constructor receives `CatalogApiService`. Self-instantiation gone.

---

#### ✅ VERIFIED #15: PerformanceViewModel CTS — RESOLVED

`PerformanceViewModel.cs:18–21` implements `IDisposable`, calls `_cts?.Dispose()`.

---

#### ✅ VERIFIED #16: DllImport / System.Management — RESOLVED

csproj defines `WINDOWS_BUILD` on Windows only; `UsbDriveDetector` WMI wrapped in `#if WINDOWS_BUILD`.

---

#### ✅ VERIFIED #17: PerformanceSnapshot silent catch — RESOLVED

Logs `Logger.Error`.

---

#### 🟢 VERIFIED #18: BrowseViewModel size — WORSE (see #10)

---

#### ✅ VERIFIED #19: Orphaned _Backup icons — RESOLVED (v0.9.1)

---

#### 🟢 VERIFIED #20: FileExplorer drive list — hardcoded, accepted fallback

Static drive list still in `DetectDrivesAsync`. Status: sufficiently addressed.

---

#### 🟢 VERIFIED #21 (NEW): Zero test coverage

No test project in tree. `docs/ideas/testing-infrastructure.md` proposal exists but unimplemented. Highest-impact gap for enabling the #1/#2/#10 refactors safely.

---

#### 🟢 VERIFIED #22 (NEW): Comment ratio 0–2%

Services 2%, ViewModels 0%.

---

## Summary Table

| # | Issue | Severity | Status (Aug 2026) | Effort |
|---|-------|----------|-------------------|--------|
| 1 | God class (XboxDeviceService) | 🔴 High | ⚠️ WORSE — 1,433 lines, cplx 205 | 6–8h |
| 2 | God class (FileExplorerViewModel) | 🔴 High | ❌ NEW — 1,880 lines, cplx 254 | 6–8h |
| 3 | _Backup directory | 🔴 High | ✅ Resolved | — |
| 4 | App.axaml.cs bloat | 🟡 Med | ⚠️ Partial (catches fixed, root open) | 3–5h |
| 5 | No ConfigureAwait | 🟡 Med | ⚠️ WORSE — 404 awaits, 0 configured | 1–2h |
| 6 | Silent exceptions | 🟡 Med | ⚠️ Partial — 26 sites, ~7 to audit | 1h |
| 7 | async void (11→22) | 🟡 Med | ⚠️ WORSE — 22 instances | 3–5h |
| 8 | No IDisposable | 🟡 Med | ⚠️ Partial — only XboxDeviceService left | 1h |
| 9 | Border clipping | 🟡 Med | ❌ Open | 2–3h |
| 10 | Large ViewModels | 🟡 Med | ❌ NEW — 5 files > 500 lines | 4–8h |
| 11 | Gradient duplication | 🟡 Med | ✅ Resolved | — |
| 12 | Button duplication | 🟡 Med | ✅ Resolved | — |
| 13 | Magic delays | 🟢 Low | ✅ Mostly resolved (2 stragglers) | 15m |
| 14 | CatalogApiService injection | 🟢 Low | ✅ Resolved | — |
| 15 | CTS disposal | 🟢 Low | ✅ Resolved | — |
| 16 | DllImport/Windows deps | 🟢 Low | ✅ Resolved | — |
| 17 | PerformanceSnapshot | 🟢 Low | ✅ Resolved | — |
| 18 | BrowseViewModel size | 🟢 Low | ⚠️ WORSE — 899 lines | (see #10) |
| 19 | Orphaned icons | 🟢 Low | ✅ Resolved | — |
| 20 | Drive list hardcoded | 🟢 Low | ➖ Accepted (fallback OK) | — |
| 21 | Zero tests | 🟢 Low | ❌ Open — highest impact gap | 8–16h |
| 22 | Comment ratio 0–2% | 🟢 Low | ❌ Open | 0h (during refactors) |

---

## Recommended Priority for Fixes (updated Aug 2026)

1. **IMMEDIATE:** Add test infrastructure (`dotnet test` + CI step) before any large refactor — makes #2 and #3 safe.
2. **HIGH:** Split `FileExplorerViewModel` (largest file, 254 complexity) — biggest risk concentration in the app.
3. **HIGH:** Split `XboxDeviceService` (+ implement `IDisposable` while touching it).
4. **HIGH:** Eliminate the 15 high-risk `async void` handlers (FireAndForget wrapper).
5. **MEDIUM:** Add `.ConfigureAwait(false)` to service layer.
6. **MEDIUM:** Audit the ~7 flagged bare-catch sites.
7. **LOW:** Straggler magic delays, comment-why on refactored paths.

---

**Document version:** 3.0  
**Last updated:** 2026-08-01  
**Status:** Complete re-verification against v1.2.0

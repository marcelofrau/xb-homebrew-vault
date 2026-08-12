# Auto-Update

**Impact:** High | **Effort:** Low | **Suggested priority:** Phase 1

## Problem

Users stay on old versions. Without an update mechanism, fixed bugs and new features don't reach users until they manually visit GitHub.

## Current State

- `AppSettings.CheckForUpdatesOnStartup` exists as a placeholder — never read by code
- `BuildInfo.Version` / `BuildInfo.DisplayVersion` extract version from assembly (`v1.0.1`)
- CI already publishes releases on GitHub with `v*` tags
- Downloads available via `https://github.com/marcelofrau/xb-homebrew-vault/releases/latest`

## Proposal

### 1. GitHubReleaseCheckerService
- GET `https://api.github.com/repos/marcelofrau/xb-homebrew-vault/releases/latest`
- Parse `tag_name`, `html_url`, `assets[].browser_download_url`
- Compare with `BuildInfo.Version` using `SemVer.Parse()` (or manual parsing)
- Cache response for 1h (avoid rate limit)

### 2. Notification UI
- Sidebar badge "Update available" with tooltip + link
- Modal "New version X.Y.Z available" with release notes (loaded from release description)
- Buttons: "Download" (opens browser), "Remind later" (dismiss for N days), "Skip this version"

### 3. Applying updates (advanced, optional)
- Download correct ZIP based on `RuntimeInformation.OSDescription` + `ProcessArchitecture`
- Extract + replace executable + show instructions
- Or simply open the download URL — user does the rest

### 4. Settings
- Checkbox "Check for updates on startup" (SettingsView — already exists in model)
- Frequency: startup only (no background polling)

## Dependencies
- None new (HttpClient already used everywhere)
- For semantic parsing: `System.Version` works for semver-like versions

## Auto-Apply (download + replace)

### Problem
Notification alone doesn't solve it — user still has to download manually, extract ZIP, replace files. Low update adoption.

### Architecture

**.NET locks the EXE while running** — can't replace the binary while it's open. Solution: separate updater.

### Option A — Separate updater (recommended)

```
XBVault.exe (running)
  → detects update via GitHubReleaseCheckerService
  → prompts "Update vX.Y.Z available. Download and install?"
  → if yes:
      1. Download correct release ZIP to %TEMP%/XBVault-update/
      2. Extract ZIP
      3. Spawn XBVault.Updater.exe with args:
         --source "%TEMP%/XBVault-update/"
         --target "<install-dir>"
         --pid <current-process-id>
      4. Exit
  → Updater.exe:
      1. Wait for XBVault.exe process to exit (WaitForExit)
      2. Copy new files over old ones
      3. (Optional) Preserve user settings.json
      4. Restart XBVault.exe
      5. Exit
```

**Projects:**
- `XBVault.Updater/` — separate project (or same solution)
  - `OutputType`: `Exe` (console, but no window — compile as Windows app)
  - `TargetFramework`: `net10.0` (or `net8.0` to minimize runtime)
  - No external dependencies — only `System.IO.Compression` + `System.Diagnostics`
  - Built in CI, attached as separate release asset

**Pros:** Robust, no flashing window, can show progress.
**Cons:** Extra project to maintain, updater needs to be compatible with older Windows.

### Option B — Helper script

Generate `xbv-update.ps1` (or `.cmd`) that:
1. Downloads the ZIP
2. Waits for XBVault to close (detection loop)
3. Replaces files
4. Restarts

**Pros:** Zero extra build, script already included in ZIP.
**Cons:** Visible terminal window, less robust, PowerShell depends on execution policy.

### Option C — Just open browser (current)

Notify → "Download available" → opens URL. User does everything else.

---

## Dev / SNAPSHOT build detection

### Problem
In dev, assembly version matches the previous release (`1.0.1`). GitHubReleaseCheckerService compares and says "update available" — for the very code being edited. False positive.

### .NET convention
No native `-SNAPSHOT`. Alternatives:

| Approach | Example | How to set |
|----------|---------|------------|
| **Pre-release suffix (recommended)** | `1.0.1-dev`, `1.0.1-ci.a1b2c3d` | `[AssemblyInformationalVersion("1.0.1-dev")]` in csproj via `VersionSuffix` |
| **Build metadata** | `1.0.1+sha.a1b2c3d` | SemVer build-metadata (after `+`). Less common. |
| **VersionSuffix in CI** | `1.0.1-ci-20260704` | CI sets `VersionSuffix` on `dotnet build` |

### Implementation in checker

```csharp
// If current version has a pre-release suffix (e.g. 1.0.1-dev, 1.0.1-ci.*)
// → don't compare against GitHub, skip check
var current = SemVer.Parse(BuildInfo.Version);
if (!string.IsNullOrEmpty(current.PreRelease))
{
    Logger.Debug("Skipping update check — dev build ({Version})", BuildInfo.Version);
    return;
}

// Only compare if it's a clean release (e.g. 1.0.1, 1.0.2)
var latest = SemVer.Parse(githubTag);
if (latest > current) { /* notify */ }
```

### CI integration
- Feature branch builds: `dotnet build -p:VersionSuffix=ci-$(git rev-parse --short HEAD)`
- Release builds (tag `v*`): no suffix, clean version
- GitHubReleaseCheckerService ignores any version with a pre-release suffix

### Impact on auto-update
- Dev builds never receive update notifications (correct — it makes no sense)
- Only official releases (tag `v*`, no suffix) trigger update check
- Regular users never see dev builds — only clean releases

## Related Topics
- [Crash Report](crash-report.md) — report bugs including current version
- [Version Checker + Bulk Update](version-checker-bulk-update.md) — update apps on Xbox (not XBVault itself)

## Files to modify
- `Models/AppSettings.cs` — field already exists, just validate
- `Services/GitHubReleaseCheckerService.cs` — new service
- `Services/UpdateService.cs` — download + extract + spawn updater
- `ViewModels/SettingsViewModel.cs` — checkbox binding
- `ViewModels/MainViewModel.cs` — check on startup (after connect or immediately)
- `MainWindow.axaml` — badge/indicator in sidebar and/or status bar
- `Program.cs` — DI for new services

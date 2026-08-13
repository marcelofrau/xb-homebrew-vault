## Why

Users launch the same homebrew app (e.g. their preferred emulator or game) every time they turn on the console and connect. XBVault can do that automatically: when it connects to the Xbox, launch the user's chosen app. Launch capability already exists (`XboxPackageService.LaunchPackageAsync`) — this change exposes it as a per-app "autostart" toggle.

## What Changes

- Add a per-app **"Autostart on connect"** option to each app's flyout in the **Installed** tab (same flyout that already has Play/Run/etc.), with an icon and a confirmation dialog.
- **Single-app exclusivity**: only one app can be autostart-enabled at a time. Enabling a new app prompts the user (previous selection is replaced); the current autostart app can be disabled/removed.
- **Visual indicator**: the autostart-enabled app gets a badge in the top-left corner of its card (matching the OUTDATED badge style/placement) with an indicative color, so it's recognizable at a glance.
- On successful connection to the Xbox, XBVault auto-launches the configured app via the existing `LaunchPackageAsync(fullName, rid)` path (suspending any running app first, same as manual Play).
- Selection persists in settings (`%APPDATA%/XBVault/settings.json`).
- No daemon, no Windows Run-key, no CLI changes — XBVault's own startup behavior is unchanged.

## Capabilities

### New Capabilities
- `app-autostart-toggle`: per-app autostart setting in the Installed tab flyout, single-app exclusivity, confirmation dialog, persistent selection.
- `app-autostart-badge`: card badge (top-left, OUTDATED-badge style) + indicative color on the autostart-enabled app.
- `app-autostart-launch`: on-connect auto-launch via `LaunchPackageAsync`, suspend-then-launch like manual Play, failure feedback (toast/log).

### Modified Capabilities
- (none)

## Impact

- **New files**: `XBVault/Services/AutostartService.cs` (persisted selection + exclusivity), `XBVault/ViewModels/AutostartViewModel.cs` (or logic in `InstalledViewModel`)
- **Modified files**: `XBVault/ViewModels/InstalledViewModel.cs` (flyout option + badge state + launch-on-connect hook), `XBVault/Views/InstalledView.axaml` (flyout menu item, badge overlay), `XBVault/Models/AppSettings.cs` (autostart app id), connection flow (`App.axaml.cs` or wherever connect completes)
- **Reuses existing**: `XboxPackageService.LaunchPackageAsync`, `SuspendAnyRunningAsync` logic in `InstalledViewModel`, OUTDATED badge template
- **Depends on**: `background-async-tasks` (toast/log feedback)
- **No breaking changes** — opt-in, off by default

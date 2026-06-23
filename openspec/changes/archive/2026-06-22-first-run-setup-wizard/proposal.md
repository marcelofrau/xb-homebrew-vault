## Why

On first launch, the app shows a blank "Not configured" status with no guidance. Users must discover the Connect button in the sidebar, then know to open Settings to enter Xbox IP, port, username, and password before they can connect. This friction hurts first impressions and increases support burden for a tool already niche (Xbox Dev Mode homebrew).

A first-run setup wizard solves this by guiding users step-by-step through the minimum required configuration, with clear explanations at each step.

## What Changes

- Add `SetupWizardWindow` — a 3-step wizard (Console → Authentication → Ready) shown automatically when no Xbox connection is configured
- Detect first-run / unconfigured state by checking `SettingsService.Current.XboxConnection.IsConfigured` (Address + Username + EncryptedPassword must all be non-empty)
- Show wizard as a modal dialog on top of MainWindow after the splash delay, so the app looks alive (catalog loading in background) while the user configures
- Step 1 (Console): Xbox IP address, port (default 11443), HTTPS toggle
- Step 2 (Authentication): Xbox Device Portal username and password
- Step 3 (Ready): Configuration summary + checkbox "Open connection window" (default checked)
- Cancel/close → no settings saved; wizard re-triggers on next launch
- Finish → saves settings via `SettingsService.Save()`, password obfuscated via `CryptoService.Obfuscate()`; if checkbox checked, opens `ConnectionWindow` immediately
- Use same window template as `CustomInstallWindow` (600x500, no chrome, green border, gradient title bar, drag, left sidebar with step indicators)
- Add `Assets/Views/SetupWizardWindow/` with icons from existing Icons8 set; generate grayscale disabled step icons via ImageMagick
- Reuse existing close icon from another view's asset folder (e.g., `connection-close-20.png`)
- No existing specs or capabilities are modified — this is a net-new addition

## Capabilities

### New Capabilities

- `first-run-setup-wizard`: Step-by-step initial configuration wizard for Xbox connection settings (Address, Port, HTTPS, Username, Password) with save-on-finish and optional post-wizard connection dialog

### Modified Capabilities

None — this is a net-new feature with no changes to existing specs.

## Impact

- **New files:** `ViewModels/SetupWizardViewModel.cs`, `Views/SetupWizardWindow.axaml`, `Views/SetupWizardWindow.axaml.cs`, 11 new icon files under `Assets/Views/SetupWizardWindow/`
- **Modified files:** `App.axaml.cs` — add first-run detection and wizard launch after `main.Show()` before splash closes
- **No changes to** `SettingsService`, `MainViewModel`, `ConnectionViewModel`, or any existing window
- **Zero new dependencies** — all patterns (window template, step wizard, settings, crypto) already exist in codebase

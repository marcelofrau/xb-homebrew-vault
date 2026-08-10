## Why

Every reconnect today is manual. When the console sleeps, times out, or the app restarts, the user must reconnect by hand. The connection-monitor foundation now detects `ConnectionLost`; this change acts on it — optional connect-on-launch and automatic reconnect with backoff.

## What Changes

- Add a single settings toggle **"Autoconnect & reconnect"** (default **off**). When enabled and credentials exist, the app connects to the Xbox Dev Portal automatically at launch; it also reconnects automatically after connection loss.
- Add a **reconnect manager** that subscribes to connection-monitor `ConnectionLost` and retries the connection with exponential backoff (1 s → 2 s → 4 s → 8 s → 16 s → 30 s → cap 60 s), stopping after a bounded number of consecutive failures (configurable in Settings, default 5, default shown) or when the user explicitly disconnects.
- Reconnect is visible: notification + task-center entry per attempt; success fires `ConnectionRestored` and a success notification.
- No silent surprises: if credentials are missing or connection never succeeds, the app logs the failure and shows a single notification; no infinite background loops while the UI is idle.

## Capabilities

### New Capabilities
- `auto-reconnect`: reconnect manager — subscribes to `ConnectionLost`, exponential backoff retries (max attempts configurable, default 5), stop conditions (explicit disconnect, app exit, success), visibility via notifications + task center.
- `autoconnect-startup`: connect-on-launch behavior — single toggle ("Autoconnect & reconnect"), credential-availability guard, one-time connect attempt at startup.

### Modified Capabilities
- (none — `connection-monitor` behavior is unchanged; autoconnect merely consumes its events)

## Impact

- **New files**: `XBVault/Services/ReconnectManager.cs`, `XBVault/ViewModels/SettingsViewModel.cs` (toggle binding, if not already present)
- **Modified files**: `XBVault/Services/AppStartup.cs` (or wherever startup sequence lives), `XBVault/MainWindow.axaml.cs` (wiring on launch), Settings view, `XBVault/Services/SettingsService.cs` (new settings fields)
- **Depends on**: `background-async-tasks` change (connection-monitor `ConnectionLost`/`ConnectionRestored`, notification center, task-center)
- **No breaking changes** — off by default, manual flows untouched


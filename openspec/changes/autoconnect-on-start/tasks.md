## 1. Settings + wiring

- [ ] 1.1 Add `AutoConnect` (default false) + `ReconnectMaxAttempts` (default 5) keys to `SettingsService` with defaults and persistence
- [ ] 1.2 Add to Settings view: single "Autoconnect & reconnect" toggle + max-attempts field (default 5 shown)

## 2. Reconnect manager

- [ ] 2.1 Create `ReconnectManager` subscribing to connection-monitor `ConnectionLost`; only active while `AutoConnect` enabled
- [ ] 2.2 Implement backoff loop (1/2/4/8/16/30/60 s) as a single `BackgroundTaskService` task with per-attempt Details
- [ ] 2.3 Add stop conditions: explicit disconnect, manual connect, app exit, toggle off, max `ReconnectMaxAttempts` consecutive failures
- [ ] 2.4 Notifications on success/failure; success resets backoff

## 3. Startup autoconnect

- [ ] 3.1 On `MainWindow.Opened`, if toggle on + credentials present + not connected, run connect as a background task
- [ ] 3.2 Guard against concurrent connects (skip if connecting/connected)

## 4. Validation

- [ ] 4.1 `dotnet build` passes
- [ ] 4.2 Manual: enable toggle, sleep console, watch backoff retries in task center, confirm bounded stop
- [ ] 4.3 Manual: disable toggle, confirm no reconnect after loss

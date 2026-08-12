## 1. Settings

- [x] 1.1 Add `AutoConnect` (bool, default false) to `AppSettings`/`SettingsService` with defaults and persistence. **`ReconnectMaxAttempts` dropped** — simplified design has no retry loop.
- [x] 1.2 Add "Autoconnect" toggle to Settings view (checkbox row after general settings). Toggle applies to `SettingsService.Current` immediately; persisted on Save.

## 2. Lazy-connect (replaces reconnect manager + connection monitor)

- [x] 2.1 `ConnectionMonitorService` **removed** (job, `ConnectionLost`/`ConnectionRestored` events, `ConnectionCheckIntervalSeconds` setting, Settings row). No recurring jobs; loss surfaces as operation errors.
- [x] 2.2 Add `IXboxAuthService.EnsureConnectedAsync(ct)` — no-op when connected; auto-connects (Configure + TestConnection + MarkConnected) only when `AutoConnect` on + configured + not explicitly disconnected; `SemaphoreSlim` in-flight guard (concurrent callers reuse first result); 30 s cooldown after a failed attempt bounds re-hammering of a dead console.
- [x] 2.3 Explicit `Disconnect()` sets a user-disconnect flag that blocks auto-connect until a manual `MarkConnected()` (clear on manual reconnect) or app restart.
- [x] 2.4 Wire lazy-connect into Xbox-touching commands: Installed refresh + running-state poll, Tools commands (screenshot/system info/processes/network/performance/custom install/crash data/loopback/dev portal/restart/shutdown), FileExplorer storage init, Inspector scan, Browse installed-check + install. Flag off → existing "not connected" flows unchanged.

## 3. Startup autoconnect

- [x] 3.1 On `MainWindow` open, if toggle on + credentials present + not connected, run connect as a visible one-shot background task ("Connecting to Xbox…") with a result toast (success/failure).
- [x] 3.2 Guard against concurrent connects via `EnsureConnectedAsync` in-flight lock + re-check on lock acquire.

## 4. Validation

- [x] 4.1 `dotnet build` passes (0 warnings/errors)
- [x] 4.2 `dotnet test` passes (180/180, incl. 8 new `EnsureConnectedTests`)
- [x] 4.3 Manual: toggle on + launch app → "Connecting to Xbox…" task in center, connected toast, status bar connected
- [x] 4.4 Manual: toggle on + console sleeping → operation auto-connect fails once, then cooldown blocks retries for ~30 s
- [x] 4.5 Manual: toggle off → existing "Not connected" dialogs/messages unchanged
- [x] 4.6 Manual: connect, Disconnect, toggle on → next operation does NOT auto-connect; manual connect then Disconnect → still blocked

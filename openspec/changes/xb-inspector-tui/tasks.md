## 1. Project Setup

- [ ] 1.1 Create `tools/XbInspector.Tui/` directory with `XbInspector.Tui.csproj` (`Exe`, net8.0, no Windows-specific properties)
- [ ] 1.2 Add Terminal.Gui v2 NuGet dependency
- [ ] 1.3 Create `Program.cs` with CLI arg parsing (`--host`, `--mock`, `--mock-port`, `--timeout`, `--port-start`, `--port-end`) and `System.CommandLine` or manual `args` parsing
- [ ] 1.4 Read `XB_HOST` env var fallback when `--host` not provided; exit with usage if neither set

## 2. Protocol & Models

- [ ] 2.1 Create `Models/InspectorMessage.cs` (event, protocol_version, id?, payload)
- [ ] 2.2 Create `Models/InspectorHandshake.cs` (app_name, app_version?, capabilities[])
- [ ] 2.3 Create `Models/InspectorLogEntry.cs` (level, tag, message, timestamp?, thread_id?, file?, line?)
- [ ] 2.4 Create `Protocol/InspectorProtocol.cs` — NDJSON serialize/deserialize with `protocol_version` validation

## 3. Network Services

- [ ] 3.1 Create `Services/ScanService.cs` — async scan range 9000-9010 with per-port timeout, collect valid handshakes
- [ ] 3.2 Create `Services/SessionService.cs` — TCP connect, read NDJSON stream, parse frames, expose events
- [ ] 3.3 Implement disconnect detection (socket closed, read timeout, parse error)
- [ ] 3.4 Implement bounded FIFO log buffer (5000 entries max, oldest dropped)
- [ ] 3.5 Implement backpressure counter (total received vs displayed)

## 4. Mock Agent

- [ ] 4.1 Create `Services/MockAgentService.cs` — `TcpListener` on configurable port, send handshake on connect
- [ ] 4.2 Implement periodic log emission (every 2s, rotating levels, ERROR every 10th)
- [ ] 4.3 Gate mock with `MOCK_ENABLED` conditional compilation for release builds

## 5. TUI Views

- [ ] 5.1 Create `Ui/LogFeedView.cs` — scrollable list view with log entries, auto-scroll, color-coded levels
- [ ] 5.2 Create `Ui/StatusBarView.cs` — persistent status bar (idle, scanning, connected, disconnected states)
- [ ] 5.3 Create `Ui/MainWindow.cs` — TUI layout: session list on left, log feed main area, status bar bottom
- [ ] 5.4 Wire keybindings: `F5` scan, `Enter` connect session, `Ctrl+L` clear, `End` auto-scroll, `Esc` back to list

## 6. Integration & Polish

- [ ] 6.1 Wire scan → session list → connect → log stream → disconnect → re-scan flow end-to-end
- [ ] 6.2 Test with `--mock`: scan finds mock, logs appear, disconnect detected, re-scan works
- [ ] 6.3 Test with `--host` pointing to real xb-inspector agent (when available)
- [ ] 6.4 Create `docs/INSPECTOR-TUI.md` — install, usage, flags, mock mode, threat model warning

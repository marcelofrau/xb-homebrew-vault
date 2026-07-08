## 1. Protocol & Models

- [ ] 1.1 Define `InspectorMessage` record (event, protocol_version, id?, payload JsonDocument)
- [ ] 1.2 Define `InspectorHandshake` record (app_name, app_version?, capabilities[])
- [ ] 1.3 Define `InspectorLogEntry` record (level, tag, message, timestamp?, thread_id?, file?, line?)
- [ ] 1.4 Define `InspectorSessionState` enum (Disconnected, Connecting, Connected, UnsupportedVersion, Error)
- [ ] 1.5 Define `InspectorSessionInfo` record (port, app_name, protocol_version, capabilities[], start_time)

## 2. Network Service

- [ ] 2.1 Create `InspectorScanService` — async TCP scan of ports 9000-9010 on given IP, timeout 3s per port
- [ ] 2.2 Create `InspectorSessionService` — connect, read NDJSON stream, parse frames, expose `IObservable<InspectorMessage>`
- [ ] 2.3 Implement disconnect detection (socket closed, read timeout, parse error)
- [ ] 2.4 Expose backpressure counter (total received messages, current buffer count)
- [ ] 2.5 Register services in DI (Program.cs)

## 3. Inspector View

- [ ] 3.1 Add `InspectorView.axaml` + `InspectorViewModel.cs` (scan button, session list, log feed, clear button)
- [ ] 3.2 Add `InspectorView.axaml.cs` code-behind for log auto-scroll
- [ ] 3.3 Create `InspectorLogItemTemplate` DataTemplate (color-coded level, tag, message, timestamp)
- [ ] 3.4 Add empty state ("Scan to discover inspector agents") and error state ("No agents found")
- [ ] 3.5 Add Inspector entry to main navigation sidebar

## 4. Log Feed & History

- [ ] 4.1 Implement bounded FIFO log buffer (5000 entries max, oldest dropped)
- [ ] 4.2 Implement level-filtering (optional: show/hide DEBUG/INFO/WARN/ERROR toggles)
- [ ] 4.3 Display total received vs displayed counter
- [ ] 4.4 Handle backpressure synthetic WARN log display (detect `tag: "xb-inspector"` + drop mention)
- [ ] 4.5 Implement Clear button

## 5. Mock Agent (Validation)

- [ ] 5.1 Create `tests/MockInspectorAgent/` standalone console app (or embedded mode in Vault debug build)
- [ ] 5.2 Implement TCP listener on configurable port, send handshake, emit periodic log events
- [ ] 5.3 Test: scan discovers mock, log feed populates, disconnect detected, re-scan works

## 6. Documentation

- [ ] 6.1 Add threat-model / security warning to `docs/INSPECTOR.md`
- [ ] 6.2 Document protocol v1 message schemas in `docs/INSPECTOR-PROTOCOL.md`
- [ ] 6.3 Document how to add `xb-inspector` agent to a homebrew app (C++/C#/Rust examples)
- [ ] 6.4 Document mock agent usage for local testing

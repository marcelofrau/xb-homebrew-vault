## ADDED Requirements

### Requirement: TUI startup with CLI flags and env vars

The application SHALL accept configuration via CLI flags and environment variables.

#### Scenario: --host flag
- **WHEN** the user runs `xb-inspector-tui --host 192.168.1.100`
- **THEN** the target IP SHALL be set to 192.168.1.100
- **WHEN** `--host` is not provided
- **THEN** the application SHALL check `XB_HOST` env var
- **WHEN** neither `--host` nor `XB_HOST` is set
- **THEN** the application SHALL print usage and exit with code 1

#### Scenario: --mock flag
- **WHEN** the user runs `xb-inspector-tui --mock`
- **THEN** the application SHALL start a local TCP listener (mock agent) on the configured mock port
- **THEN** the scan SHALL include the mock agent in results
- **THEN** the mock agent SHALL send a valid `handshake` and emit periodic simulated log events

#### Scenario: Default scan range
- **WHEN** no port range flags are provided
- **THEN** the scan range SHALL default to 9000-9010 inclusive

### Requirement: TUI scan discovers active agents

The TUI SHALL scan the configured host for xb-inspector agents on ports 9000-9010.

#### Scenario: Scan triggered by keypress
- **WHEN** the user presses `F5` (or equivalent scan key) in the TUI
- **THEN** the application SHALL attempt TCP connections on ports 9000-9010 to the configured host
- **THEN** each connection SHALL wait for a valid `handshake` message within the configured timeout (default 3000ms)
- **THEN** ports with valid handshakes SHALL appear in a session list
- **THEN** empty/unresponsive ports SHALL be silently skipped

#### Scenario: Scan progress shown
- **WHEN** a scan is in progress
- **THEN** the TUI SHALL display a status message ("Scanning 192.168.1.100:9000-9010...")
- **THEN** found sessions SHALL appear as they are discovered (incremental)

#### Scenario: No agents found
- **WHEN** scan completes with zero sessions
- **THEN** the TUI SHALL display "No inspector agents found on {host}"
- **THEN** the user SHALL be able to press `F5` to retry

### Requirement: Session connection and log streaming

The TUI SHALL connect to a selected session and stream logs in real time.

#### Scenario: Select and connect
- **WHEN** the user selects a session from the list (Enter or click)
- **THEN** the TUI SHALL connect to the selected agent
- **THEN** the application SHALL enter the log feed view
- **THEN** incoming `log` events SHALL appear in the feed in real time

#### Scenario: Log levels color-coded
- **WHEN** a log entry is displayed
- **THEN** DEBUG SHALL appear in dim/gray
- **THEN** INFO SHALL appear in default terminal color
- **THEN** WARN SHALL appear in yellow
- **THEN** ERROR SHALL appear in red
- **THEN** FATAL SHALL appear in red + bold

#### Scenario: Auto-scroll new entries
- **WHEN** new log entries arrive
- **THEN** the feed SHALL auto-scroll to show the latest entry
- **THEN** if the user scrolls up manually, auto-scroll SHALL pause
- **THEN** pressing `End` or equivalent SHALL re-enable auto-scroll

#### Scenario: Bounded log history
- **WHEN** log entries exceed 5000
- **THEN** the oldest entries SHALL be discarded (FIFO)
- **THEN** a counter in the status bar SHALL show "5000 entries (oldest dropped)"

#### Scenario: Clear logs
- **WHEN** the user presses `Ctrl+L` or equivalent
- **THEN** the log feed SHALL be cleared
- **THEN** the session SHALL remain connected

#### Scenario: Disconnect detection
- **WHEN** the TCP connection drops
- **THEN** the TUI SHALL display "Disconnected" status
- **THEN** the user SHALL be able to press `F5` to re-scan or `Esc` to return to session list

### Requirement: Status bar shows connection state

The TUI SHALL display a persistent status bar with current state.

#### Scenario: Status bar content
- **WHEN** idle (no scan)
- **THEN** status bar SHALL show "Idle — Press F5 to scan {host}"
- **WHEN** scanning
- **THEN** status bar SHALL show "Scanning {host}:9000-9010..."
- **WHEN** connected to a session
- **THEN** status bar SHALL show "{app_name} @ {host}:{port} — {entry_count} entries — Connected"
- **WHEN** disconnected
- **THEN** status bar SHALL show "Disconnected — Press F5 to re-scan"

### Requirement: Mock agent built-in for local testing

The TUI SHALL include a built-in mock xb-inspector agent for testing without a real Xbox.

#### Scenario: Mock handshake
- **WHEN** `--mock` flag is active and a connection arrives
- **THEN** the mock SHALL send a valid handshake with `app_name: "Mock Inspector Demo"`, `protocol_version: 1`, `capabilities: ["logs"]`

#### Scenario: Mock periodic log emission
- **WHEN** a client is connected to the mock
- **THEN** the mock SHALL emit a log event every 2 seconds with rotating levels (INFO, DEBUG, WARN)
- **THEN** every 10th log SHALL be ERROR level to test error display

#### Scenario: Mock disconnect
- **WHEN** the client disconnects
- **THEN** the mock SHALL stop emitting and close the listener
- **THEN** re-scan with `--mock` SHALL restart the mock listener

### Requirement: Protocol v1 NDJSON parsing

The TUI SHALL parse the same protocol v1 as defined by the xb-inspector specification.

#### Scenario: Handshake parse
- **WHEN** a `handshake` message is received
- **THEN** `protocol_version` SHALL be validated (only version 1 accepted)
- **THEN** `payload.app_name` SHALL be displayed in session list and status bar
- **THEN** `payload.capabilities` SHALL be displayed in session details

#### Scenario: Log message parse
- **WHEN** a `log` message is received
- **THEN** `payload.level`, `payload.tag`, `payload.message` SHALL be parsed
- **THEN** `payload.timestamp` SHALL be parsed as ISO 8601 if present

#### Scenario: Unsupported protocol version
- **WHEN** handshake `protocol_version` is not 1
- **THEN** the session SHALL be listed with "Unsupported protocol v{version}" status
- **THEN** connection SHALL NOT proceed to log feed

#### Scenario: Unknown event type
- **WHEN** a message with unrecognized `event` type is received
- **THEN** the message SHALL be silently ignored
- **THEN** parsing SHALL continue without error

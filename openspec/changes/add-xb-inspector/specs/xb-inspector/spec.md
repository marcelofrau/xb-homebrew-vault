## ADDED Requirements

### Requirement: Inspector scan discovers active agents

The system SHALL provide an Inspector capability that scans the configured Xbox IP for active `xb-inspector` agents on ports 9000-9010.

#### Scenario: Scan finds one agent
- **WHEN** user clicks "Scan" in the Inspector view
- **THEN** XBVault SHALL attempt TCP connections to the configured Xbox IP on ports 9000, 9001, ..., 9010
- **THEN** each connection SHALL wait for a valid `handshake` message within 3 seconds
- **THEN** ports that return a valid `handshake` SHALL be listed as discovered sessions
- **THEN** ports that timeout, refuse, or return invalid data SHALL be silently skipped

#### Scenario: Scan finds multiple agents
- **WHEN** multiple ports return valid `handshake` messages
- **THEN** the UI SHALL display a list of discovered sessions with app name and port
- **THEN** the user SHALL select one session to connect

#### Scenario: No agents found
- **WHEN** all ports 9000-9010 timeout, refuse, or return invalid data
- **THEN** the UI SHALL display "No inspector agents found on {IP}"
- **THEN** the user SHALL be able to retry the scan

#### Scenario: Connection lost during session
- **WHEN** the TCP connection drops during an active session
- **THEN** the UI SHALL display "Disconnected" status with the reason (remote close, timeout, network error)
- **THEN** the user SHALL be able to reconnect or re-scan

### Requirement: Protocol v1 defines framed NDJSON messages

All inspector communication SHALL use newline-delimited JSON (NDJSON) with a base envelope.

#### Scenario: Handshake message
- **WHEN** a TCP connection is established
- **THEN** the agent SHALL send a `handshake` event within 3 seconds
- **THEN** the handshake SHALL include `protocol_version` (integer), `app_name` (string), `app_version` (string, optional)
- **THEN** the handshake SHOULD include `capabilities` (array of strings) listing supported features (e.g., `"logs"`, `"repl"`, `"state"`)
- **THEN** unsupported `protocol_version` SHALL be displayed as an unsupported-version status with a recommendation to upgrade the agent or Vault

#### Scenario: Log event message
- **WHEN** the agent sends a log line
- **THEN** the message SHALL have `event: "log"`
- **THEN** `payload` SHALL include `level` (string: "DEBUG"|"INFO"|"WARN"|"ERROR"|"FATAL"), `tag` (string), `message` (string), `timestamp` (ISO 8601 string, optional)
- **THEN** `payload` MAY include `thread_id` (integer), `file` (string), `line` (integer) for source location

#### Scenario: Agent error message
- **WHEN** the agent encounters an internal error
- **THEN** the message SHALL have `event: "agent_error"`
- **THEN** `payload` SHALL include `code` (string) and `message` (string)
- **THEN** the UI SHALL display agent errors distinctly from app logs

#### Scenario: Unknown event type
- **WHEN** a message arrives with an unrecognized `event` value
- **THEN** the Vault SHALL NOT crash or disconnect
- **THEN** the unrecognized message MAY be logged to XBVault's own log as a warning

### Requirement: MVP supports log streaming only

The MVP implementation SHALL handle log streaming. REPL and state inspection SHALL be reserved in the protocol but not implemented.

#### Scenario: Live log feed
- **WHEN** a session is active and the agent sends `log` events
- **THEN** each log SHALL appear in the Inspector log feed in real time
- **THEN** each log SHALL display level (color-coded), tag, message, and timestamp (if available)
- **THEN** the log feed SHALL auto-scroll to new entries

#### Scenario: Log history bounded
- **WHEN** the number of log entries exceeds 5000 per session
- **THEN** the oldest entries SHALL be discarded (FIFO)
- **THEN** the UI SHALL display a counter of total received vs. displayed entries

#### Scenario: Backpressure warning from agent
- **WHEN** the agent sends a log with `level: "WARN"`, `tag: "xb-inspector"`, and a message about dropped events
- **THEN** the UI SHALL display this synthetic WARN log in the log feed
- **THEN** the UI SHOULD show a non-blocking status indicator about backpressure

#### Scenario: Clear logs
- **WHEN** the user clicks "Clear"
- **THEN** the log feed SHALL be emptied
- **THEN** the history counter SHALL reset
- **THEN** the session SHALL remain connected

### Requirement: REPL protocol reserved without implementation

The protocol SHALL define REPL message shapes so future implementation does not require a rewrite.

#### Scenario: REPL eval message (reserved)
- **WHEN** a future REPL feature sends a command
- **THEN** the message SHALL have `event: "repl_eval"`
- **THEN** `payload` SHALL include `language` (string: "lua"|"csharp"|etc), `code` (string), `id` (string correlating request to response)
- **THEN** the agent SHALL execute the code on the app/main thread via a command queue

#### Scenario: REPL result message (reserved)
- **WHEN** the agent has a REPL result
- **THEN** the message SHALL have `event: "repl_result"`
- **THEN** `payload` SHALL include `id` (matching the request), `ok` (boolean), `value` (string, on success), `error` (string, on failure)
- **THEN** the UI SHALL display the result or error in a REPL output area

### Requirement: Threat model limits to trusted LAN / Dev Mode

The Inspector SHALL be documented as a trusted-LAN developer tool, not a general-purpose debug transport.

#### Scenario: Documentation warning
- **WHEN** Inspector documentation is written
- **THEN** it SHALL include a prominent warning that Inspector enables remote code execution via REPL and MUST NOT be enabled in public/retail builds
- **THEN** it SHALL state that Inspector is designed for Xbox Dev Mode on trusted LAN only

#### Scenario: No authentication in protocol v1
- **WHEN** the handshake is received
- **THEN** there SHALL be no authentication or encryption in protocol v1
- **THEN** this SHALL be documented as a known limitation
- **THEN** the design SHALL recommend dedicated VLAN or firewall rules for untrusted networks

### Requirement: Vault Inspector testable against mock agent

The system SHALL support local testing without a real Xbox.

#### Scenario: Mock agent for manual validation
- **GIVEN** a local mock `xb-inspector` agent running on e.g. 127.0.0.1:9000
- **WHEN** the user runs Inspector Scan with Xbox IP set to 127.0.0.1
- **THEN** the mock agent SHALL be discovered and connectable
- **THEN** the mock agent SHALL simulate handshake, periodic log events, and disconnect

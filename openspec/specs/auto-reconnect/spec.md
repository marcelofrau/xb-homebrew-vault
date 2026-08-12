# auto-reconnect

## Purpose

Lazy auto-connect before console-touching operations, with cooldown and explicit-disconnect respect. No recurring background liveness probe.

## Requirements

### Requirement: Lazy auto-connect on operations
When autoconnect is enabled and an operation needs the Xbox Dev Portal, the system SHALL auto-connect before performing the operation, then proceed. If auto-connect cannot establish a connection, the operation SHALL surface the existing "not connected" flow unchanged.

#### Scenario: Auto-connect before operation
- **WHEN** the user triggers an Xbox-touching operation while not connected and autoconnect is enabled
- **THEN** the system connects using saved credentials, updates the app connection state, and proceeds with the operation

#### Scenario: No retry loop in background
- **WHEN** the console is unreachable and autoconnect is enabled
- **THEN** a failed auto-connect attempt is recorded and further auto-connect attempts are blocked by a cooldown (~30 s) instead of retrying in a loop

#### Scenario: Autoconnect disabled
- **WHEN** autoconnect is disabled and the user triggers an operation while not connected
- **THEN** the operation shows the existing "not connected" prompt/dialog and no automatic connection attempt occurs

### Requirement: Respect explicit disconnect
When the user explicitly disconnects, auto-connect SHALL NOT reconnect until the user connects manually or the app restarts.

#### Scenario: Disconnect blocks auto-connect
- **WHEN** the user clicks Disconnect while autoconnect is enabled
- **THEN** subsequent operations do not auto-connect

#### Scenario: Manual reconnect re-enables
- **WHEN** the user connects manually after an explicit disconnect
- **THEN** auto-connect is available again for later disconnects

### Requirement: No recurring connection monitoring
The system SHALL NOT run a recurring background liveness probe. Connection loss SHALL surface as an error on the next operation that needs the console.

#### Scenario: No periodic probe
- **WHEN** the app is running and the console goes to sleep
- **THEN** no background job probes the connection and no "Connection Lost" toast is raised; the next console-touching operation fails/auto-connects as appropriate

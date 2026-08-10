## ADDED Requirements

### Requirement: Periodic connection check
The system SHALL periodically check whether the Xbox Dev Portal connection is still alive by querying `GET /api/os/info` while a connection is active. This is a liveness *check* (the console may sleep regardless of connection) — it does not keep the console awake.

#### Scenario: Check while connected
- **WHEN** the connection is marked connected
- **THEN** the monitor queries `/api/os/info` on the configured interval

#### Scenario: No check when not configured
- **WHEN** the Xbox connection is not configured or not connected
- **THEN** the monitor no-ops and does not issue HTTP requests

### Requirement: Connection loss detection
The system SHALL detect connection loss (timeout, refused, console sleep) and raise a `ConnectionLost` event with the failure reason.

#### Scenario: Check times out
- **WHEN** a check fails with a timeout
- **THEN** the service raises `ConnectionLost` with reason "Connection timed out"

#### Scenario: Check refused
- **WHEN** a check fails with connection refused
- **THEN** the service raises `ConnectionLost` with the underlying socket reason

#### Scenario: Connection restored
- **WHEN** a subsequent check succeeds after a failure
- **THEN** the service raises `ConnectionRestored` and resumes normal checks

### Requirement: Reconnect hook
The system SHALL expose the `ConnectionLost` event so consumers (e.g. the autoconnect feature) can trigger automatic reconnect with backoff.

#### Scenario: Consumer subscribes to loss
- **WHEN** `ConnectionLost` fires
- **THEN** any subscribed consumer is invoked with the failure reason

### Requirement: Configurable interval
The check interval SHALL be configurable through settings, defaulting to 30 seconds. The Settings UI SHALL show the value with its default indicated.

#### Scenario: Custom interval
- **WHEN** the interval is set to 60 seconds in settings
- **THEN** checks occur approximately every 60 seconds

#### Scenario: Default shown in settings
- **WHEN** the user opens the connection-monitor settings
- **THEN** the interval field shows 30 s and is marked as the default

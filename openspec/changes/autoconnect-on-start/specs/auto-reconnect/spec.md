## ADDED Requirements

### Requirement: Reconnect on connection loss
When autoconnect is enabled and the connection monitor raises `ConnectionLost`, the system SHALL attempt to reconnect to the Xbox Dev Portal automatically.

#### Scenario: Reconnect after loss
- **WHEN** the connection monitor raises `ConnectionLost`
- **THEN** the reconnect manager attempts to reconnect using saved credentials

#### Scenario: Reconnect succeeds
- **WHEN** a reconnect attempt succeeds
- **THEN** the connection is restored, a success notification is shown, and retry backoff resets

### Requirement: Exponential backoff
Reconnect attempts SHALL use exponential backoff: 1 s, 2 s, 4 s, 8 s, 16 s, 30 s, then cap at 60 s per attempt. Each failed attempt SHALL be visible as a task-center entry and a notification.

#### Scenario: Consecutive failures back off
- **WHEN** the first reconnect attempt fails
- **THEN** the second attempt waits 1 s, the third 2 s, and so on up to 60 s

#### Scenario: Cap at one minute
- **WHEN** backoff reaches 60 s
- **THEN** subsequent attempts wait 60 s

#### Scenario: Failure visible
- **WHEN** a reconnect attempt fails
- **THEN** a failed task-center entry is recorded and a notification reports the failure reason

### Requirement: Stop conditions
Reconnect SHALL stop when any of: the user explicitly disconnects, the user manually reconnects, the app exits, or autoconnect is disabled.

#### Scenario: User disconnects stops retries
- **WHEN** the user clicks Disconnect while retries are active
- **THEN** pending retries are cancelled and no new attempts are scheduled

#### Scenario: Autoconnect disabled stops retries
- **WHEN** autoconnect is toggled off while retries are active
- **THEN** pending retries are cancelled

### Requirement: No retry loop when idle
When credentials are missing or the console is unreachable, the manager SHALL NOT retry indefinitely in the background while the user is idle — after a bounded number of consecutive failures (configurable in Settings, default 5, default shown), it SHALL stop and require a manual action.

#### Scenario: Bounded retries
- **WHEN** the configured max-attempts number of consecutive reconnect attempts all fail
- **THEN** the manager stops retrying and shows a single "could not reconnect" notification

#### Scenario: Default shown in settings
- **WHEN** the user opens the reconnect settings
- **THEN** the max-attempts field shows 5 and is marked as the default

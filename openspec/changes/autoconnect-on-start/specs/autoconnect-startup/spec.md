## ADDED Requirements

### Requirement: Autoconnect toggle
The system SHALL expose a single "Autoconnect & reconnect" setting, persisted, default **off**. Changing the toggle SHALL take effect immediately and persist.

#### Scenario: Toggle off by default
- **WHEN** a fresh install is first run
- **THEN** autoconnect-and-reconnect is off

#### Scenario: Toggle persists
- **WHEN** the user enables the toggle and restarts the app
- **THEN** the toggle is still enabled

### Requirement: Connect on launch
When the "Autoconnect & reconnect" toggle is enabled and the app launches, the system SHALL attempt a one-time connection to the Xbox Dev Portal after the window is shown, using saved credentials.

#### Scenario: Connect on launch when enabled
- **WHEN** the app starts with autoconnect enabled and valid credentials
- **THEN** a connection attempt runs and the UI reflects connection progress

#### Scenario: No attempt without credentials
- **WHEN** the app starts with autoconnect enabled but no saved credentials
- **THEN** no connection attempt occurs and no error is shown

#### Scenario: Disabled by default means no attempt
- **WHEN** the app starts with autoconnect disabled
- **THEN** no automatic connection attempt occurs

### Requirement: Visibility
The startup connect attempt SHALL be visible in the task center and report success/failure notifications, consistent with manual connect behavior.

#### Scenario: Success visible
- **WHEN** the startup connect attempt succeeds
- **THEN** the connection is active and a success notification appears

#### Scenario: Failure visible
- **WHEN** the startup connect attempt fails
- **THEN** a failed task-center entry records the attempt and a failure notification appears

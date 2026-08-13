# app-autostart-launch

## Purpose

Automatic launch of the configured app when a connection to the Xbox is established, reusing the manual Play path, with failure feedback.

## Requirements

### Requirement: Auto-launch on connect
When a connection to the Xbox is established and an autostart app is configured, the system SHALL launch that app automatically using the same path as manual Play (`LaunchPackageAsync`), suspending any currently running app first.

#### Scenario: Launch on connect
- **WHEN** the app connects to the Xbox and an autostart app is configured
- **THEN** the configured app is launched

#### Scenario: Suspend running app first
- **WHEN** another app is running at connect time
- **THEN** it is suspended before the autostart app launches (same behavior as manual Play)

#### Scenario: No autostart configured
- **WHEN** no app is autostart-enabled
- **THEN** no launch occurs on connect

### Requirement: Failure feedback
If the auto-launch fails, the system SHALL surface the failure without disrupting the connection.

#### Scenario: Launch failure
- **WHEN** auto-launch fails (e.g. app missing, already removed)
- **THEN** a failure toast/log appears and the connection remains intact

### Requirement: Not connected at launch
If the configured autostart app is not present on the console, the system SHALL skip launch and log the reason.

#### Scenario: App no longer installed
- **WHEN** the autostart app is not found among installed apps at connect time
- **THEN** launch is skipped, the selection is cleared, and a notification explains why

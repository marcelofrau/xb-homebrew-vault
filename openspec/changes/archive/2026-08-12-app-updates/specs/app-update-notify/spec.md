## ADDED Requirements

### Requirement: Consolidated notification
When a scan finds updatable apps, the system SHALL raise a single consolidated notification listing all updatable apps (not one toast per app).

#### Scenario: One notification for many
- **WHEN** a scan finds three updatable apps
- **THEN** one notification lists all three

#### Scenario: No apps to update
- **WHEN** a scan finds no updatable apps
- **THEN** no notification is raised

### Requirement: Per-app click action
Each app in the notification SHALL be individually clickable and open the existing update flow for that app (`ItemDetailWindow` in update mode, or the Installed tab with the OUTDATED badge highlighted).

#### Scenario: Click app opens update
- **WHEN** the user clicks an app in the notification
- **THEN** the existing update dialog opens for that app

### Requirement: Notification center follow-up
Update notifications SHALL land in the notification center (status-bar icon + panel) so the user can re-open them after dismissal and act later.

#### Scenario: Follow up later
- **WHEN** the user dismisses the update notification
- **THEN** it remains in the notification center and can be re-opened

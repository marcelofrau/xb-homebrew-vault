## ADDED Requirements

### Requirement: Show notification
The system SHALL display in-window notifications with a title, message, and optional icon, rendered in the Blades theme as an overlay in the main window.

#### Scenario: Show simple notification
- **WHEN** `NotificationCenterService.Notify(title, message)` is called
- **THEN** a notification appears in the main window overlay with the given title and message

#### Scenario: Show with icon
- **WHEN** a notification is shown with an icon URI
- **THEN** the icon is rendered in the notification

### Requirement: Click action
A notification SHALL support an optional click action invoked when the user clicks it.

#### Scenario: Click triggers action
- **WHEN** the user clicks a notification that has a click action
- **THEN** the action is invoked and the notification is dismissed

#### Scenario: Click without action dismisses
- **WHEN** the user clicks a notification that has no click action
- **THEN** the notification is dismissed without further side effects

### Requirement: Auto-dismiss
Notifications SHALL auto-dismiss after a configurable delay (default 6 seconds) unless the user interacts first.

#### Scenario: Auto-dismiss after delay
- **WHEN** a notification is shown and no interaction occurs
- **THEN** it disappears after the configured delay

### Requirement: Grouping / consolidation
The service SHALL support grouped notifications: multiple related items (e.g. several updatable apps) SHALL render as one notification with an item list rather than flooding the screen with one notification per item.

#### Scenario: Grouped notification
- **WHEN** a caller raises a grouped notification with several items
- **THEN** one notification is shown listing all items, each independently actionable

#### Scenario: Avoid flooding
- **WHEN** multiple related notifications would be raised at once
- **THEN** they are consolidated into a single grouped notification

### Requirement: Notification center history
The service SHALL keep a history of recent notifications (dismissed or auto-dismissed) accessible from a status-bar notification icon + panel, so the user can re-open and act on them later.

#### Scenario: Re-open dismissed notification
- **WHEN** the user dismisses a notification and later opens the notification-center panel
- **THEN** the dismissed notification is still listed and actionable

#### Scenario: Status-bar indicator
- **WHEN** there are unacknowledged notifications
- **THEN** the status-bar notification icon is visible (with a count badge); when none, the icon is hidden

### Requirement: UI thread safety
`NotificationCenterService` SHALL marshal notification display and dismissal to the UI thread regardless of the calling thread.

#### Scenario: Notification from background thread
- **WHEN** a notification is requested from a background thread
- **THEN** it is displayed without exception and appears on the UI thread

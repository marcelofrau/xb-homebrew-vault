## ADDED Requirements

### Requirement: Status bar indicator
The main window status bar SHALL show a task-center indicator to the left of the version text. The indicator SHALL display a task icon, a numeric badge of active tasks, and a busy animation while any task is `Running`; SHALL be hidden entirely when there are no tasks.

#### Scenario: No tasks — hidden
- **WHEN** `ActiveCount` is 0
- **THEN** the indicator is not visible in the status bar

#### Scenario: Active tasks — visible with badge
- **WHEN** one or more tasks are `Running`
- **THEN** the indicator shows the task icon, badge with the active count, and busy animation

### Requirement: Open task center
Clicking the status bar indicator SHALL open the task-center overlay panel inside the main window.

#### Scenario: Click indicator opens panel
- **WHEN** the user clicks the status bar indicator
- **THEN** the task-center overlay panel appears

#### Scenario: Toggle close
- **WHEN** the panel is open and the user clicks the indicator again (or presses Escape)
- **THEN** the panel closes

### Requirement: Running section
The panel SHALL list `Running` tasks, each with title, status message, progress bar (determinate or indeterminate), elapsed time, and a Cancel button when `IsCancellable`.

#### Scenario: Progress shown
- **WHEN** a running task reports determinate progress
- **THEN** the panel shows a determinate progress bar at the reported value

#### Scenario: Indeterminate progress
- **WHEN** a running task is `IsIndeterminate`
- **THEN** the panel shows an animated indeterminate progress bar

#### Scenario: Cancel button
- **WHEN** a running task is `IsCancellable`
- **THEN** a Cancel button is visible and cancels the task when clicked

### Requirement: Scheduled section
The panel SHALL list scheduled recurring jobs with their name and next-run time.

#### Scenario: Job listed
- **WHEN** a recurring job is registered
- **THEN** the job name and next-run time appear under Scheduled

### Requirement: Recent section
The panel SHALL list recently finished tasks with their final status (`Completed`, `Failed`, `Cancelled`) and duration, most recent first.

#### Scenario: Finished task listed
- **WHEN** a task finishes
- **THEN** it appears under Recent with its final status and duration

### Requirement: Expandable details
Each task SHALL support expanding to show its `Details` content (e.g. exception messages or logs).

#### Scenario: Expand shows details
- **WHEN** the user expands a task with details
- **THEN** the details text is revealed

## ADDED Requirements

### Requirement: Task registry
The system SHALL expose all tracked background work as an `ObservableCollection<BackgroundTask>` so UI can bind to live task state. The collection SHALL be modified only on the UI thread.

#### Scenario: Task appears in collection
- **WHEN** a one-shot task starts running
- **THEN** the task is added to the collection with status `Running`

#### Scenario: Completed task removed from active collection
- **WHEN** a task transitions to `Completed`, `Failed`, or `Cancelled`
- **THEN** the task is removed from the active collection and raised in the recent-tasks notification

#### Scenario: Collection access on UI thread
- **WHEN** any code adds or removes a task while the app is running
- **THEN** the mutation is marshaled to the UI thread via the Dispatcher

### Requirement: One-shot task execution
The service SHALL run one-shot background tasks with progress reporting and cancellation. Each task SHALL expose `CancellationTokenSource` for cancellation, `Progress` (0–1), `IsIndeterminate`, `StatusMessage`, and expandable `Details`.

#### Scenario: Run task with progress
- **WHEN** a task reports progress 0.5 with status message "Downloading..."
- **THEN** the task's `Progress` is 0.5 and `StatusMessage` is "Downloading..."

#### Scenario: Task completes successfully
- **WHEN** the task work returns without exception
- **THEN** the task status becomes `Completed`

#### Scenario: Task fails
- **WHEN** the task work throws an exception
- **THEN** the task status becomes `Failed` and the exception message is recorded in `Details`; the failure is logged

#### Scenario: Cancel propagates
- **WHEN** a caller cancels a task that is `IsCancellable`
- **THEN** the task's `CancellationToken` is signaled and the task status becomes `Cancelled`

### Requirement: Recurring job scheduling
The service SHALL support recurring jobs (schedulers) that run on a fixed interval. Jobs SHALL expose their name, interval, and last/next run time, and SHALL appear in the task collection when executing.

#### Scenario: Job runs on interval
- **WHEN** a job is registered with a 30-second interval
- **THEN** the job executes approximately every 30 seconds

#### Scenario: Job execution visible as task
- **WHEN** a recurring job begins executing
- **THEN** a task representing the job run appears in the collection and is removed when the run finishes

#### Scenario: Job failure does not stop scheduler
- **WHEN** a job run throws an exception
- **THEN** the run is marked `Failed`, logged, and the scheduler continues with the next interval

### Requirement: Activity signaling
The service SHALL raise activity events (`TaskAdded`, `TaskRemoved`, `TaskChanged`) and expose an `ActiveCount` so the status bar indicator can react.

#### Scenario: Indicator reacts to active count
- **WHEN** `ActiveCount` transitions from 0 to 1
- **THEN** a `TaskAdded` event fires and consumers update the busy indicator

#### Scenario: Indicator hides on zero
- **WHEN** `ActiveCount` returns to 0
- **THEN** consumers hide the busy indicator

### Requirement: Singleton lifecycle
The service SHALL be constructed once at application startup, started after services are wired, and provide `Start`/`Stop` for the job scheduler.

#### Scenario: Jobs stopped on app exit
- **WHEN** the application exits
- **THEN** `Stop` cancels all running tasks and disposes job timers

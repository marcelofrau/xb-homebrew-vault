## Why

XBVault has no background execution foundation. Everything runs on explicit user action (manual connect, manual refresh, manual install). The roadmap needs background work — autoconnect connection-monitor, auto-update checks, auto-backup — but there is no shared place to run, track, or cancel background work. Today the only timer is an 8s `DispatcherTimer` inside `InstalledViewModel` (running-state polling).

## What Changes

- Add `BackgroundTaskService`: a singleton that runs one-shot background tasks (with progress reporting + cancellation) and recurring jobs (schedulers). Exposes an `ObservableCollection<BackgroundTask>` and activity events so UI can react.
- Add `BackgroundTask` model: title, category, status, progress, status message, expandable details, elapsed time, cancellable flag, recurring flag.
- Add a **connection monitor** (check-alive): a recurring job that pings `GET /api/os/info` to detect whether the console is still alive (the Xbox can enter sleep regardless of connection, so this is a *check*, not a keepalive). Interval configurable, default 30 s (shown in Settings with its default). Raises `ConnectionLost`/`ConnectionRestored`.
- Add a **notification center**: status-bar notification icon + in-window panel listing current and recent notifications (toasts). Notifications support click actions, grouping/consolidation (e.g. one notification listing several updatable apps), and re-open after dismissal — so users can follow up later.
- Add an IntelliJ-style task center: status-bar indicator (icon + badge + busy animation, shown left of the version text) and an in-window overlay panel listing Running / Scheduled / Recent tasks with progress bars, cancel buttons, and expandable details.
- All future features (autoconnect, app-autostart, app backup, app updates) report progress through this service and notify through the notification center, making their work visible and cancellable.

## Capabilities

### New Capabilities
- `background-task-service`: BackgroundTaskService — one-shot task runner with progress/cancel, recurring job scheduler, ObservableCollection of tasks, activity events, UI-thread marshaling.
- `connection-monitor`: periodic check of the Xbox Dev Portal connection (configurable interval, default 30 s), sleep/timeout detection, ConnectionLost event, automatic reconnect hooks. *(Was "keepalive" — renamed: it checks liveness, it does not keep the console awake.)*
- `notification-center`: status-bar notification icon + in-window panel; toasts with title/message/icon/click action, auto-dismiss, grouping/consolidation, and history re-openable after dismissal.
- `task-center-ui`: status-bar indicator (icon, badge, busy animation) + in-window overlay panel with Running/Scheduled/Recent sections, progress, cancel, expandable details.

### Modified Capabilities
- (none — no existing specs change behavior)

## Impact

- **New files**: `XBVault/Models/BackgroundTask.cs`, `XBVault/Services/BackgroundTaskService.cs`, `XBVault/Services/ConnectionMonitorService.cs`, `XBVault/Services/NotificationCenterService.cs`, `XBVault/ViewModels/TaskCenterViewModel.cs`, `XBVault/Views/TasksPanel.axaml` (+`.cs`), `XBVault/Assets/Views/MainWindow/mainwindow-tasks-20.png`, `tests/XBVault.Tests/BackgroundTaskServiceTests.cs`
- **Modified files**: `XBVault/MainWindow.axaml` (status bar indicator + overlay host), `XBVault/MainWindow.axaml.cs`, `XBVault/App.axaml.cs` (service construction/startup wiring)
- **No new external dependencies** (uses `System.Threading`, `System.Threading.Tasks`, Avalonia built-ins)
- **No breaking changes** — existing manual flows unchanged; connection-monitor/toast/task center are opt-in additive infrastructure
- Follow-up features (autoconnect, app-autostart, app-backup, app-updates) consume this foundation in later changes

## Why

XBVault has no background execution foundation. Everything runs on explicit user action (manual connect, manual refresh, manual install). The roadmap needs background work — autoconnect connection-monitor, auto-update checks, auto-backup — but there is no shared place to run, track, or cancel background work. Today the only timer is an 8s `DispatcherTimer` inside `InstalledViewModel` (running-state polling).

## What Changes

- Add `BackgroundTaskService`: a singleton that runs one-shot background tasks (with progress reporting + cancellation) and recurring jobs (schedulers). Exposes an `ObservableCollection<BackgroundTask>` and activity events so UI can react.
- Add `BackgroundTask` model: title, category, status, progress, status message, expandable details, elapsed time, cancellable flag, recurring flag.
- Add a **connection monitor** (check-alive): a recurring job that pings `GET /api/os/info` to detect whether the console is still alive (the Xbox can enter sleep regardless of connection, so this is a *check*, not a keepalive). Interval configurable, default 30 s (shown in Settings with its default; `0` disables). Runs only while the app is actually connected. Raises `ConnectionLost`/`ConnectionRestored` — and is this change's first toast producer.
- Add a **notification center**: toast overlay + status-bar bell icon (with count badge). Notifications support click actions, grouping/consolidation (e.g. one notification listing several updatable apps). The bell is placed now but **not yet wired** — the history panel (re-open after dismissal) lands in a later change; in-memory history still kept in the service.
- Add an IntelliJ-style task center: status-bar indicator (icon + badge + busy animation, shown left of the version text) and an in-window overlay panel listing Running / Scheduled / Recent tasks with progress bars, cancel buttons, and expandable details.
- All future features (autoconnect, app-autostart, app backup, app updates) report progress through this service and notify through the notification center, making their work visible and cancellable.

## Capabilities

### New Capabilities
- `background-task-service`: BackgroundTaskService — one-shot task runner with progress/cancel, recurring job scheduler, ObservableCollection of tasks, activity events, UI-thread marshaling, 1 s elapsed ticker.
- `connection-monitor`: periodic check of the Xbox Dev Portal connection (configurable interval, default 30 s, `0` = disabled, only while connected), sleep/timeout detection, ConnectionLost/ConnectionRestored events, toast notifications. *(Was "keepalive" — renamed: it checks liveness, it does not keep the console awake.)*
- `notification-center`: toast overlay + status-bar bell icon (count badge); toasts with title/message/icon/click action, auto-dismiss, grouping/consolidation. History panel (re-open after dismissal) deferred to a later change.
- `task-center-ui`: status-bar indicator (icon, badge, busy animation) + in-window overlay panel with Running/Scheduled/Recent sections, progress, cancel, expandable details.

### Modified Capabilities
- (none — no existing specs change behavior)

## Impact

- **New files**: `XBVault/Models/BackgroundTask.cs`, `XBVault/Services/BackgroundTaskService.cs`, `XBVault/Services/ConnectionMonitorService.cs`, `XBVault/Services/NotificationCenterService.cs`, `XBVault/ViewModels/TaskCenterViewModel.cs`, `XBVault/Views/TasksPanel.axaml` (+`.cs`), `XBVault/Assets/Views/MainWindow/mainwindow-tasks-20.png`, `XBVault/Assets/Views/MainWindow/mainwindow-bell-20.png`, `tests/XBVault.Tests/XBVault.Tests.csproj`, `tests/XBVault.Tests/BackgroundTaskServiceTests.cs`
- **Modified files**: `XBVault/MainWindow.axaml` (status bar indicators + toast/task overlay hosts), `XBVault/MainWindow.axaml.cs`, `XBVault/App.axaml.cs` (service construction/startup wiring), `XBVault/Models/AppSettings.cs` (`ConnectionCheckIntervalSeconds`), `XBVault/Views/SettingsView.axaml` + `XBVault/ViewModels/SettingsViewModel.cs` (interval row), `.github/workflows/build.yml` (test step)
- **Deferred from this change**: `Views/NotificationsPanel.axaml` (+`.cs`) and bell→history-panel interaction (bell icon placed, not wired); notification expiry logic
- **No new external dependencies** (uses `System.Threading`, `System.Threading.Tasks`, Avalonia built-ins; test project adds xunit)
- **No breaking changes** — existing manual flows unchanged; connection-monitor/toast/task center are opt-in additive infrastructure
- Follow-up features (autoconnect, app-autostart, app-backup, app-updates) consume this foundation in later changes

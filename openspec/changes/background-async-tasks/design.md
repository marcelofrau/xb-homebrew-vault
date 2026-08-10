## Context

XBVault is a small .NET 8 + Avalonia app with no DI container, no existing background execution layer, and no test project. Current async work is scattered (manual connect, manual install with progress). The app is a WinExe targeting Windows primarily, with CI publishing Linux/macOS. The MVP philosophy is: small app, plain singletons, no heavy abstractions.

Future roadmap (autoconnect, Xbox app autostart, app backup, app update notifications) all need: background execution, progress visibility, cancellation, and non-blocking feedback. This change builds the shared foundation those consume. A side goal is keeping the app simple enough that unit tests are possible, so the core service is written testable (no hard Avalonia dependency at its center).

## Goals / Non-Goals

**Goals:**
- Singleton `BackgroundTaskService` for one-shot tasks + recurring jobs, UI-bound task collection, activity events.
- `BackgroundTask` model with status/progress/cancel/details/elapsed.
- `ConnectionMonitorService`: periodic liveness check of the console (configurable interval, default 30 s, shown in Settings), `ConnectionLost`/`ConnectionRestored` events.
- `NotificationCenterService`: in-window notifications with click actions, grouping/consolidation, auto-dismiss, and a history panel re-openable from a status-bar icon.
- Task-center UI: status-bar indicator (icon + badge + busy animation, hidden at 0) + in-window overlay panel (Running / Scheduled / Recent, progress, cancel, expandable details).
- Zero new NuGet dependencies.
- New test project `tests/XBVault.Tests`.

**Non-Goals:**
- Autoconnect, Xbox app autostart, app backup, app update notifications (they land in later changes and consume this foundation).
- A full DI container / IoC framework.
- OS-level notifications (beyond in-window overlays).
- Persistent notification/task history (in-memory only; cleared on exit).

## Decisions

### 1. Plain singleton services, no DI container
The app has no DI container and a handful of singletons (`SettingsService.Current`, services constructed in `App.axaml.cs`). Adding a container for one background layer is overkill.
- **Chosen**: `BackgroundTaskService.Instance` and `NotificationCenterService.Instance` plain singletons, constructed in `App.axaml.cs`.
- **Alternative considered**: CommunityToolkit `Ioc.Default` — rejected: introduces a pattern the rest of the app doesn't use.
- **Consequence**: the connection monitor keeps a reference to the connection client via constructor injection (`ConnectionMonitorService(XboxConnectionService)`), still constructed once in `App.axaml.cs`.

### 2. ObservableCollection mutated only on UI thread
Avalonia (like WPF) requires collection mutations on the UI thread. Background work runs on thread-pool threads.
- **Chosen**: all collection mutations funneled through `Dispatcher.UIThread.InvokeAsync`. The service itself never touches the collection from worker threads.
- **Consequence**: `BackgroundTaskService` references `Dispatcher.UIThread`. To keep unit tests working without a running Avalonia app, expose the dispatcher as a virtual/pluggable member (`virtual Dispatcher Dispatcher { get; }`) that tests can fake.

### 3. BackgroundTask = immutable metadata + mutable state (INotifyPropertyChanged)
Single class, two concerns. `INotifyPropertyChanged` lets Avalonia bind progress/status without a ViewModel wrapper.
- Metadata set at construction: `Id`, `Title`, `Category`, `IsRecurring`, `IsCancellable`, `CreatedAt`.
- Mutable via internal setters: `Status`, `Progress` (double 0–1), `IsIndeterminate`, `StatusMessage`, `Details`, `CompletedAt`.
- `Elapsed` computed from `CreatedAt`/`CompletedAt`, exposed as `TimeSpan`.

### 4. Recurring jobs with PeriodicTimer, run-in-task semantics
`System.Threading.PeriodicTimer` (not `Timer` + drift) for interval scheduling; each run becomes a one-shot task in the collection.
- `RegisterJob(name, interval, intervalUnits, async delegate)`.
- Run visible as a `BackgroundTask`; failure marks run `Failed`, logs, continues scheduling.
- `Stop()` cancels all jobs + running tasks; called on app exit.

### 5. Connection monitor = liveness CHECK, not keepalive
The Xbox console enters sleep regardless of whether the app is "connected", so pinging cannot keep it awake. The monitor is a **check**: `GET /api/os/info` on an interval (default 30 s, configurable in Settings with the default shown) only while connected. On failure: raises `ConnectionLost(reason)`. On subsequent success: raises `ConnectionRestored`. Interval is a `SettingsService` value; job re-reads it each cycle (no restart needed to change it). The autoconnect change (later) subscribes to `ConnectionLost`.

### 6. Notification center = overlay + history panel
- **Toasts**: an `ItemsControl` overlay `Border` (Blades style) in the MainWindow root grid. `Notify(...)` posts notification items; auto-dismiss via `System.Threading.Timer` + dispatcher marshal; click = command, dismisses.
- **Grouping**: `NotifyGrouped(title, items)` renders one notification with a list of actionable items (used later by app-updates). Concurrency cap (default 4 visible toasts) with oldest-dismiss policy.
- **History panel**: second status-bar icon (bell) + overlay panel listing recent notifications (re-openable, per-item action, clear-all). Unacknowledged count drives the icon badge.

### 7. Task center = status-bar indicator + in-window overlay panel
- Indicator: small `Border` (icon 20 px + badge `TextBlock` + busy animation) placed in the status bar next to the version text. Hidden when `ActiveCount == 0`.
- Panel: a `Grid` overlay covering the main content area (below title bar, above status bar), with header "Tasks", a close button, and three `ItemsControl` groups (Running / Scheduled / Recent). Toggle via clicking indicator; `Escape` closes.
- `TaskCenterViewModel` binds to `BackgroundTaskService` collections; updates flow through `PropertyChanged`.

## Architecture

```mermaid
flowchart TD
    App[App.axaml.cs] -->|constructs + Start| BTS[BackgroundTaskService]
    App -->|constructs| CMS[ConnectionMonitorService]
    BTS -->|job| CMS
    CMS -->|GET /api/os/info| XBOX[XboxConnectionService]
    CMS -.ConnectionLost/ConnectionRestored.-> FUTURE[autoconnect change]
    BTS -->|ActiveTasks/ActivityChanged| TCVM[TaskCenterViewModel]
    TCVM -->|bind| TCMainWindow[MainWindow task-center panel]
    BTS -->|UI-thread marshal| DISP[Avalonia Dispatcher]
    NCS[NotificationCenterService] -->|UI-thread marshal| DISP
    NCS -->|notifications/history| NCPanel[status-bar bell + history panel]
    DISP --> TCVM
    DISP --> MainWindow overlay
```

## File map

| File | Purpose |
| --- | --- |
| `XBVault/Models/BackgroundTask.cs` | Task model (metadata + mutable state) |
| `XBVault/Services/BackgroundTaskService.cs` | One-shot runner, job scheduler, collection, events |
| `XBVault/Services/ConnectionMonitorService.cs` | Liveness check job + loss detection (configurable interval) |
| `XBVault/Services/NotificationCenterService.cs` | Notifications + grouping + history panel |
| `XBVault/ViewModels/TaskCenterViewModel.cs` | Panel/indicator VM |
| `XBVault/Views/TasksPanel.axaml` (+`.cs`) | Overlay panel control |
| `XBVault/Views/NotificationsPanel.axaml` (+`.cs`) | History panel control |
| `XBVault/MainWindow.axaml` / `.axaml.cs` | Status-bar indicators (tasks + bell) + overlay hosts + wiring |
| `XBVault/App.axaml.cs` | Construct/start services |
| `XBVault/Models/AppSettings.cs` (mod) | `ConnectionCheckIntervalSeconds` (default 30) |
| `XBVault/Assets/Views/MainWindow/mainwindow-tasks-20.png` | Task indicator icon (Blades/Numix set) |
| `tests/XBVault.Tests/BackgroundTaskServiceTests.cs` | Unit tests (new test project) |

## Risks / Trade-offs

- **Collection mutation on wrong thread** → single choke point (`Dispatcher.UIThread.InvokeAsync`) in the service; tests fake the dispatcher. Mitigation: code review + unit tests.
- **Monitor checks console too aggressively** → default 30 s interval, configurable, no-op when not connected.
- **Toast accumulation if auto-dismiss blocked** → cap concurrent toasts (4), oldest dismisses; grouped notifications prevent per-item flooding.
- **Recent lists unbounded memory** → cap tasks (50) and notifications (50).
- **PeriodicTimer fires while UI busy** → job runs off UI thread; UI only observes. Mitigation: elapsed/progress marshaled.
- **WinExe lifecycle** → `Stop()` hooked into `MainWindow.Closed`.

## Migration Plan

- Additive. No existing behavior changes; existing manual flows untouched.
- Rollback: revert files; indicators hidden when no tasks/notifications, so no visual regression if a service fails to start.
- Later changes (autoconnect, app autostart, app backup, app updates) only ADD consumers; the service contract is stable.

## Open Questions

- Notification history: should dismissed-notification actions stay actionable indefinitely in-session, or expire (e.g. updates for apps no longer installed are dropped)? (Lean: drop stale items, keep v1 simple.)
- Whether the connection-check interval needs a Settings UI now or just the settings value (lean: value now, simple Settings row later with autoconnect change).

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
- `RegisterJob(name, interval, async delegate)` — interval is a `TimeSpan` (no `intervalUnits` enum).
- Run visible as a `BackgroundTask`; failure marks run `Failed`, logs, continues scheduling.
- Interval change: the job **re-creates the PeriodicTimer** when the interval value changes between cycles (option A). Interval `TimeSpan.Zero`/`<= 0` = job disabled (no-op, no timer).
- `Stop()` cancels all jobs + running tasks; called on app exit.

### 4b. Elapsed display via 1 s DispatcherTimer
`Elapsed` is a pure computed value (`now − CreatedAt` for running, `CompletedAt − CreatedAt` for finished) — testable without a UI thread. A 1 s `DispatcherTimer` in the service (UI thread, consistent with the marshal-everything-to-dispatcher rule) fires `PropertyChanged` on running tasks so the panel updates live. The timer is a thin UI-refresh layer; tests assert on the pure computation, not the timer.

### 5. Connection monitor = liveness CHECK, not keepalive
The Xbox console enters sleep regardless of whether the app is "connected", so pinging cannot keep it awake. The monitor is a **check**: `GET /api/os/info` (via existing `IXboxAuthService.TestConnectionAsync`, which already maps timeout/refused reasons matching the spec) on an interval (default 30 s, configurable in Settings with the default shown).
- **Gate**: pings only while `authService.IsConnected` (the `_connected` flag set by `MarkConnected()` on explicit connect) — **not** `IsConfigured`. This avoids pinging on startup before the user ever connects. The flag is re-checked at the start of each cycle (no event subscription needed).
- **Off-switch**: interval `0` = monitoring disabled (no-op). Same field, no extra checkbox.
- On failure: raises `ConnectionLost(reason)` + a toast via `NotificationCenterService` (first real toast producer, makes the toast center visible/validatable this change). On subsequent success: raises `ConnectionRestored` + a toast.
- Interval is a `SettingsService` value; the job re-creates its `PeriodicTimer` when the value changes (no restart needed). The autoconnect change (later) subscribes to `ConnectionLost`.

### 6. Notification center = overlay + history panel (panel deferred)
- **Toasts**: an `ItemsControl` overlay `Border` (Blades style) in the MainWindow content grid. `Notify(...)` posts notification items; auto-dismiss via `System.Threading.Timer` + dispatcher marshal; click = command, dismisses.
- **Grouping**: `NotifyGrouped(title, items)` renders one notification with a list of actionable items (used later by app-updates). Concurrency cap (default 4 visible toasts) with oldest-dismiss policy.
- **Status-bar bell**: icon (20 px) + unacknowledged-count badge, hidden at 0, rendered in this change. **No click action, no history panel yet** — `NotificationsPanel.axaml` + re-open-after-dismiss deferred to a later change (in-memory history still kept in the service, cap 50).
- First toast producer this change: the connection monitor (loss/restore notifications).

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
    CMS -.ConnectionLost/Restored + toast.-> NCS
    NCS -->|notifications| NCPanel[status-bar bell + toast overlay]
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
| `XBVault/Views/NotificationsPanel.axaml` (+`.cs`) | **Deferred** — bell icon only this change |
| `XBVault/MainWindow.axaml` / `.axaml.cs` | Status-bar indicators (tasks + bell) + overlay hosts + wiring |
| `XBVault/App.axaml.cs` | Construct/start services |
| `XBVault/Models/AppSettings.cs` (mod) | `ConnectionCheckIntervalSeconds` (default 30, 0 = disabled) |
| `XBVault/Views/SettingsView.axaml` / `ViewModels/SettingsViewModel.cs` (mod) | Interval row (value + "default 30" hint) |
| `XBVault/Assets/Views/MainWindow/mainwindow-tasks-20.png` | Task indicator icon (personal Icons8 set, `icons8-task-2d-20.png`) |
| `XBVault/Assets/Views/MainWindow/mainwindow-bell-20.png` | Bell icon (personal set, `icons8-bell-20.png`) |
| `tests/XBVault.Tests/BackgroundTaskServiceTests.cs` + `XBVault.Tests.csproj` | Unit tests (new test project) |
| `.github/workflows/build.yml` (mod) | Add `dotnet test tests/XBVault.Tests` to the build job |

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

- ~~Notification history staleness~~ → **Resolved**: history panel deferred; in-memory history keeps cap 50, no expiry logic this change.
- ~~Connection-check interval Settings UI~~ → **Resolved**: Settings row now (value + "default 30" hint); interval `0` disables the monitor.

## 1. Core model + service

- [ ] 1.1 Create `BackgroundTask` model in `XBVault/Models/` — immutable metadata + mutable state, INotifyPropertyChanged
- [ ] 1.2 Create `BackgroundTaskService` — one-shot runner with progress/cancel, UI-thread-marshaled `ObservableCollection`, virtual Dispatcher for testability
- [ ] 1.3 Add `RegisterJob` recurring scheduler (PeriodicTimer) with run-as-task semantics, failure-tolerant scheduling
- [ ] 1.4 Add activity events (`TaskAdded`/`TaskRemoved`/`TaskChanged`) + `ActiveCount`
- [ ] 1.5 Add `Start()`/`Stop()` lifecycle; wire construction in `App.axaml.cs` and stop on `MainWindow.Closed`

## 2. Connection monitor

- [ ] 2.1 Create `ConnectionMonitorService` — registers a liveness-check job (`GET /api/os/info` via existing `XboxConnectionService`) only while connected
- [ ] 2.2 Add `ConnectionLost`/`ConnectionRestored` events with failure reason
- [ ] 2.3 Add `ConnectionCheckIntervalSeconds` setting (default 30); job re-reads it each cycle; Settings shows value + default

## 3. Notification center

- [ ] 3.1 Create `NotificationCenterService` — `Notify(title, message, icon?, action?)`, UI-thread marshal, auto-dismiss (6 s default), concurrency cap (4)
- [ ] 3.2 Add `NotifyGrouped(title, items)` — consolidated notification with per-item actions (no toast flood)
- [ ] 3.3 Add overlay `Border` + `ItemsControl` host in MainWindow root grid; Blades-styled toast template
- [ ] 3.4 Add status-bar bell icon (count badge, hidden at 0) + `NotificationsPanel.axaml` history overlay (re-open dismissed, per-item action, clear-all)

## 4. Task center UI

- [ ] 4.1 Add status-bar indicator (icon 20 px + badge + busy animation) next to version text; hidden at ActiveCount 0
- [ ] 4.2 Create `TaskCenterViewModel` binding ActiveTasks/Scheduled/Recent + ActivityChanged
- [ ] 4.3 Create `Views/TasksPanel.axaml` overlay panel — Running (progress/cancel/elapsed/expandable), Scheduled (name/next run), Recent (status/duration)
- [ ] 4.4 Toggle open/close (indicator click + Escape); expandable details

## 5. Icon + tests

- [ ] 5.1 Add `mainwindow-tasks-20.png` indicator icon per assets guide (Blades/Numix set), reference via `avares://`
- [ ] 5.2 Create `tests/XBVault.Tests` test project (xunit) with `BackgroundTaskServiceTests` — progress, cancel, failure, job scheduling, UI-marshal with fake dispatcher

## 6. Build + validation

- [ ] 6.1 `dotnet build XBVault/XBVault.csproj` passes
- [ ] 6.2 `dotnet test tests/XBVault.Tests` passes
- [ ] 6.3 Run app manually: run a long install, watch indicator + panel, cancel mid-task; dismiss a notification and re-open from bell

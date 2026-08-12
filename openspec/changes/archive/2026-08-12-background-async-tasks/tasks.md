## 1. Core model + service

- [x] 1.1 Create `BackgroundTask` model in `XBVault/Models/` — immutable metadata + mutable state, INotifyPropertyChanged
- [x] 1.2 Create `BackgroundTaskService` — one-shot runner with progress/cancel, UI-thread-marshaled `ObservableCollection`, virtual Dispatcher for testability
- [x] 1.3 Add `RegisterJob` recurring scheduler (PeriodicTimer) with run-as-task semantics, failure-tolerant scheduling
- [x] 1.4 Add activity events (`TaskAdded`/`TaskRemoved`/`TaskChanged`) + `ActiveCount`
- [x] 1.5 Add `Start()`/`Stop()` lifecycle; wire construction in `App.axaml.cs` and stop on `MainWindow.Closed`

## 2. Connection monitor

> **SUPERSEDED** — `ConnectionMonitorService` + `ConnectionCheckIntervalSeconds` removed by `autoconnect-on-start` (simplified design: no recurring liveness job; lazy auto-connect via `EnsureConnectedAsync`; loss surfaces as operation errors). Items below shipped and were later removed.

- [x] 2.1 Create `ConnectionMonitorService` — registers a liveness-check job that reuses `IXboxAuthService.TestConnectionAsync` (`GET /api/os/info`); gates on `authService.IsConnected` (not `IsConfigured`), re-checked each cycle
- [x] 2.2 Add `ConnectionLost`/`ConnectionRestored` events with failure reason; raise toast via `NotificationCenterService` on each (first toast producer)
- [x] 2.3 Add `ConnectionCheckIntervalSeconds` setting (default 30, 0 = disabled); job re-reads it and re-creates its PeriodicTimer on change; Settings row shows value + default hint

## 3. Notification center

- [x] 3.1 Create `NotificationCenterService` — `Notify(title, message, icon?, action?)`, UI-thread marshal, auto-dismiss (6 s default), concurrency cap (4), in-memory history (cap 50)
- [x] 3.2 Add `NotifyGrouped(title, items)` — consolidated notification with per-item actions (no toast flood)
- [x] 3.3 Add overlay `Border` + `ItemsControl` host in MainWindow content grid; Blades-styled toast template
- [x] 3.4 Add status-bar bell icon (20 px, count badge, hidden at 0) — **icon only, no click handler, no history panel** (NotificationsPanel deferred to later change)
- [x] 3.5 Toast restyle: dark-green `#284325` card, 1 px black border, light text, always-on 20 px icon (default info icon; autoconnect passes success/error icons); flyout-positioned bottom-right above status bar
- [x] 3.6 Toast fade-in/out: `Style.Animations` enter (opacity 0→1, 250 ms) + `.toast-closing` exit (1→0) via `ToastHost` wrapper that intercepts `Active.CollectionChanged` and holds removal until fade completes

## 4. Task center UI

- [x] 4.1 Add status-bar indicator (icon 20 px + badge + busy animation) next to version text; hidden at ActiveCount 0
- [x] 4.2 Create `TaskCenterViewModel` binding ActiveTasks/Scheduled/Recent + ActivityChanged
- [x] 4.3 Create `Views/TasksPanel.axaml` overlay panel — Running (progress/cancel/elapsed/expandable), Scheduled (name/next run), Recent (status/duration)
- [x] 4.4 Toggle open/close (indicator click + Escape); expandable details

## 4b. Flyout refinements (anchored flyouts + fade)

- [x] 4.5 Move tasks/notifications panels to anchored `Popup` flyouts — gear (`TaskIndicator`) / bell (`NotificationsButton`) in top-right `TopBarIcons` StackPanel, `Placement="BottomEdgeAlignedRight"`
- [x] 4.6 Create `Views/NotificationsPanel.axaml` (+`.cs`) — ACTIVE (per-item dismiss) + HISTORY + empty state + ✕ (`CloseRequested`)
- [x] 4.7 Fade in/out on open/close via manual `FadeOpacityAsync` (8-step `Task.Delay` loop) — XAML `DoubleTransition` on popup child stuck at opacity 0 (animation clock never ticked in popup host), removed from both panels
- [x] 4.8 Outside-click close **with fade** — `IsLightDismissEnabled="False"` + window-level `PointerPressed` (tunnel) → `ClosePopupWithFadeAsync`; gear/bell buttons and popup content excluded (their Click handlers manage toggling)
- [x] 4.9 `BackgroundTaskService.RemoveActive` dedups recent by `Name` (Connection Monitor doesn't stack); `NextRunAtLocal` shows local time (UTC internal, `ToLocalTime()` + notify)
- [x] 4.10 `TaskCenterViewModel.IsOpen` drives `TasksPopup.IsOpen` in code (no `IsOpen` binding); Escape closes notifications then tasks
- [x] 4.11 NotificationsPanel: HISTORY always visible (no count-gated visibility), per-item ✕ to clear one entry, header "Clear all" button (clears active + history, badge → 0); history is an `ObservableCollection` (`RemoveFromHistory`/`ClearAll`) so the list updates live

## 5. Icons + tests

- [x] 5.1 Add `mainwindow-tasks-20.png` (from personal set `icons8-task-2d-20.png`) and `mainwindow-bell-20.png` (from `icons8-bell-20.png`) per assets guide, reference via `avares://`
- [x] 5.2 Create `tests/XBVault.Tests` test project (`XBVault.Tests.csproj`, xunit, references `XBVault.csproj`) with `BackgroundTaskServiceTests` — progress, cancel, failure, job scheduling, elapsed (pure computation), UI-marshal with fake dispatcher

## 6. Build + validation

- [x] 6.1 `dotnet build XBVault/XBVault.csproj` passes
- [x] 6.2 `dotnet test tests/XBVault.Tests` passes (172/172)
- [x] 6.3 Add `dotnet test tests/XBVault.Tests` step to `.github/workflows/build.yml` build job — already present as a dedicated `test` job (windows + ubuntu)
- [x] 6.4 Run app manually: run a long install, watch indicator + panel, cancel mid-task; verify flyout fade-out on click-outside + gear/bell toggle. **Connection-lost toast part superseded** — monitor removed by `autoconnect-on-start`

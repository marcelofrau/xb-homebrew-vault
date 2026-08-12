# Scheduled Tasks

**Impact:** Medium | **Effort:** High | **Suggested priority:** Phase 3

## Problem

Managing Xbox involves repetitive tasks: restart after installing something, shutdown at night, catalog refresh, save backup. Today everything is manual.

## Proposal

### TaskSchedulerService
Background task scheduling engine. Doesn't need to run 24/7 — uses `System.Threading.Timer` while the app is open. For persistence beyond the session, would integrate with Windows Task Scheduler (`schtasks`).

### Task types

| Task | Action | Typical schedule |
|------|--------|-----------------|
| Restart Xbox | POST restart | After install batch |
| Shutdown Xbox | POST shutdown | Night time |
| Catalog Refresh | GET catalog → clear cache | Daily |
| Backup Saves | SFTP download saves | Weekly |
| Health Check Email (future) | Send status | Daily |

### UI: Task Scheduler
New window accessible from Settings or Tools:

```
┌────────────────────────────────────────────────────┐
│ ⚙️ Scheduled Tasks               [+ New Task]      │
│────────────────────────────────────────────────────│
│ 🔄 Catalog Refresh    Daily 03:00    ✅ Active     │
│ ⏻ Shutdown            Daily 23:30    ✅ Active     │
│ 💾 Backup Saves       Weekly Sun 04:00 ❌ Disabled │
│                                                    │
│ ── New Task ───────────────────────────────────────│
│ Action: [Restart ⬇]                                │
│ Schedule: ○ Once ○ Daily ● Weekly ○ Custom         │
│ Day: [Sunday ⬇]  Time: [04:00]                     │
│ [Save] [Cancel]                                    │
└────────────────────────────────────────────────────┘
```

### Persistence
- Tasks saved in `%APPDATA%/XBVault/tasks.json`
- Simple JSON format
- On app start, check pending tasks and execute if needed

### Limitations
- App needs to be open to execute (unless integrated with system)
- Windows: `schtasks` allows scheduling even with app closed
- macOS/Linux: `crontab` or `launchd`
- Native integration is optional (phase 2)

### Dependencies
- No new library
- Reuses `XboxSystemService` (restart/shutdown)
- Reuses `SftpService` (backup)
- Reuses `CatalogApiService` (refresh)

### Files to create
- `Services/TaskSchedulerService.cs`
- `Models/ScheduledTask.cs`
- `Views/SchedulerWindow.axaml` + `.axaml.cs`
- `ViewModels/SchedulerViewModel.cs`

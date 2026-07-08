# System Health Dashboard

**Impact:** Medium | **Effort:** Medium | **Suggested priority:** Phase 2

## Problem

Xbox info is scattered across separate windows: System Info, Network Info, Performance Chart, Process List. No unified "health status" view of the console.

## Proposal

### Consolidated dashboard
New window/tab accessible from Tools view with side-by-side cards:

```
┌────────────────────────────────────────────────────┐
│  🟢 System Health               Xbox Series S      │
│────────────────────────────────────────────────────│
│ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐│
│ │ Latency  │ │ Uptime   │ │ Storage  │ │ Memory   ││
│ │ 2ms      │ │ 3d 14h   │ │ 312/512GB│ │ 62% used ││
│ └──────────┘ └──────────┘ └──────────┘ └──────────┘│
│ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐│
│ │ CPU      │ │ GPU      │ │ Temp     │ │ Processes││
│ │ 12%      │ │ 45%      │ │ 52°C     │ │ 47       ││
│ └──────────┘ └──────────┘ └──────────┘ └──────────┘│
│ [Refresh All]                                       │
└────────────────────────────────────────────────────┘
```

### Data to aggregate
| Card | Source | API |
|------|--------|-----|
| Latency | TestConnectionAsync with timer | `/api/os/info` |
| Uptime | SystemInfo | `/api/systeminfo` |
| Storage | SystemInfo + /ext/smb/developerfolder | SMB info |
| Memory | PerformanceSnapshot | WebSocket `/api/resourcemanager/systemperf` |
| CPU | PerformanceSnapshot | WebSocket |
| GPU | PerformanceSnapshot | WebSocket |
| Temp | PerformanceSnapshot (when available) | WebSocket |
| Processes | GetProcessesAsync (count) | `/api/resourcemanager/processes` |

### Features
- Manual refresh + auto-refresh every 10s
- Alert-colored cards (green/yellow/red based on thresholds)
- Tooltip with detailed value on each card
- Simple history: mini sparkline of last N CPU/memory points

### Dependencies
- Reuses existing `XboxDeviceService` (or [split services](refactor-xboxdeviceservice.md))
- Performance snapshot already has WebSocket implemented
- No new NuGet dependencies

### Files to create
- `Views/HealthDashboardView.axaml` + `.axaml.cs`
- `ViewModels/HealthDashboardViewModel.cs`

### Files to modify
- `ToolsView.axaml` — new "Health Dashboard" button
- `ToolsViewModel.cs` — `OpenHealthDashboardCommand`

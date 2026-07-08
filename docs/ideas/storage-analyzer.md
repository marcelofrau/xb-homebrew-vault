# Storage Analyzer

**Impact:** Medium | **Effort:** Medium | **Suggested priority:** Phase 2

## Problem

Xbox disk space is limited (364 GB usable on Series S). No visibility into who is consuming storage — user only finds out when running out of space.

## Proposal

### "Storage Analyzer" view
New window that scans Xbox storage via SMB/SFTP and presents:

```
XB Storage
Total: 364.0 GB | Used: 281.3 GB (77%) | Free: 82.7 GB

[████████████████████████████░░░░░░░░░░] 77%

┌────────────────────────────────────────────────────┐
│ App                          Size     %             │
│────────────────────────────────────────────────────│
│ 🎮 Streets of Rage 4        8.2 GB   12.1%         │
│ 🎮 Burnout Paradise          6.8 GB   10.0%         │
│ 🎮 GTA Vice City             3.4 GB    5.0%         │
│ 📦 RetroArch                 1.2 GB    1.8%         │
│ 📦 PPSSPP                    0.8 GB    1.2%         │
│ ...                                                 │
│ 🗑️ Temp / Cache              4.1 GB    6.0%         │
│ 📁 Other                    12.3 GB   18.1%         │
└────────────────────────────────────────────────────┘
     [Clean Temp] [Export Report]
```

### Approaches
1. **SFTP scan** (recommended): traverse `T:\\` recursively, sum by directory. More complete but slower.
2. **SMB scan** (`\\XBOX\DevelopmentFiles\`): same idea, different protocol, potentially better performance.
3. **Xbox API**: Xbox Device Portal doesn't expose per-app storage. REST API `/api/systeminfo` gives total used/free but no breakdown.

### Features
- Pie chart or bar chart by app
- Drill-down: click an app → view individual files
- "Clean Temp/Cache" → navigate to `T:\\temp\\` or similar and delete
- Refresh with 30s cache
- "Export Report" → save CSV/JSON of scan

### Dependencies
- SFTP scan: reuses existing `SftpService`
- SMB scan: `XboxDeviceService.FetchSmbPasswordAsync()` + `System.IO` (SMB mount not trivial on macOS/Linux)
- Recommended: SFTP as primary (cross-platform), SMB as Windows-only fallback

### Files to create
- `Views/StorageAnalyzerWindow.axaml` + `.axaml.cs`
- `ViewModels/StorageAnalyzerViewModel.cs`
- `Services/StorageAnalyzerService.cs` — scan + cache logic

### Files to modify
- `ToolsView.axaml` — new button
- `ToolsViewModel.cs` — command

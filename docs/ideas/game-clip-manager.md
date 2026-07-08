# Game Clip / Screenshot Manager

**Impact:** Medium | **Effort:** High | **Suggested priority:** Phase 3

## Problem

Xbox captures screenshots and game clips automatically, but accessing them from PC requires going through Xbox Device Portal via browser, downloading one by one. No dedicated UI.

## Current state

- Screenshot capture exists in `ToolsView` — single capture, preview, local save
- API `/ext/screenshot` already used
- Game clips not addressed

## Proposal

### GameClipManagerService
- `ListClipsAsync()` → GET `/ext/gameclips` or similar
- `GetClipThumbnailAsync(clipId)` → thumbnail
- `DownloadClipAsync(clipId, destination)` → progressive download
- `DeleteClipAsync(clipId)`

### UI: Media Browser
New window with grid/tiles of:

**Screenshots:**
- Thumbnail grid with full-size preview
- Multi-select, batch download, delete
- Uses existing API with visual navigation

**Game Clips:**
- List with thumbnail, game title, duration, date, size
- Preview (if embed VLC/native player possible — complex)
- Single or batch download

### UX
- Separate tab in Tools or sub-tab under Screenshot
- "Select all" checkbox, "Download Selected", "Delete Selected" buttons
- Download status with progress
- Sort by date (newest first) or game

### Dependencies
- Xbox Device Portal API: media endpoints (research which exist)
- Screenshot: already works, just expand UI
- Game clips: may require undocumented Portal endpoints

### Risks
- Game clip API may not exist in Device Portal (only on Xbox dashboard)
- Large clip downloads (multiple GB) without progress can be frustrating
- Video preview requires codecs or embedded player — complex in Avalonia

### Alternative strategy
If game clip API doesn't exist:
- Offer via SMB (`\\XBOX\DevelopmentFiles\`) — clips in shared folder?
- Or focus only on screenshots with improved UI

### Files to create
- `Services/MediaService.cs`
- `Views/MediaBrowserWindow.axaml` + `.axaml.cs`
- `ViewModels/MediaBrowserViewModel.cs`

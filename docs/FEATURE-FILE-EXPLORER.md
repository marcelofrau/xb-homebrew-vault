# File Explorer — Xbox Dev Portal Browser

## Goal
Replace the `FileExplorerView` placeholder tab (index 2) with a functional file browser for the Xbox Dev Mode filesystem, using the Dev Portal REST API.

## Layout

```
+------------------------------------------------------------------+
| FILE EXPLORER  header + path breadcrumb                          |
+----------------------------------+-------------------------------+
| TreeView (left, 280px)           | Right pane: contents list     |
|  /Apps                           | of selected folder, or       |
|  /Data                           | empty state if nothing       |
|  /LocalAppData                   | selected                     |
|  /Logs                           |                              |
|  (single-expand: expand a root   |                              |
|   collapses any other root)      |                              |
+----------------------------------+-------------------------------+
| Upload card: [Drop zone + Browse Files button]                   |
| Target: <selected folder path>                                   |
+------------------------------------------------------------------+
```

## TreeView Behavior
- Single-expand: expanding a root collapses any previously expanded root
- Clicking a file shows its details in the right pane
- Right-click context menu: Delete, Download

## Upload Card (bottom)
- Drag-and-drop zone (accepts multiple files)
- "Browse Files" button using `OpenFileDialog` (multi-file)
- Uploads to the currently selected folder in the tree
- Progress indicator per file (or batch success/fail toast)

## REST Endpoints
**Blocked — user provides curl calls when feature is resumed.**

Endpoints needed:
- List directory contents
- Upload file
- Delete file/directory
- Create folder
- Download file

## Models
- `FileEntry` (name, path, size, isDirectory, modified date, extension)

## ViewModel
- `ObservableCollection<FileEntry>` Roots (top-level dirs)
- `FileEntry? SelectedEntry`
- `string CurrentPath`
- `IsUploading` + upload progress properties
- Commands: `ExpandFolderCommand`, `SelectEntryCommand`, `UploadFilesCommand`, `DeleteEntryCommand`, `DownloadEntryCommand`, `RefreshCommand`

## Icons (from personal set — 16x16 for tree items, 20x20 for toolbar)
- `fileexplorer-folder-closed-16.png`
- `fileexplorer-folder-open-16.png`
- `fileexplorer-file-16.png`
- `fileexplorer-upload-20.png`
- `fileexplorer-download-20.png`
- `fileexplorer-delete-20.png`
- `fileexplorer-refresh-20.png`
- `fileexplorer-new-folder-20.png`

## Files to Create/Modify
| File | Action |
|---|---|
| `XBVault/Models/FileEntry.cs` | New model |
| `XBVault/Services/XboxDeviceService.cs` | Add filesystem API methods |
| `XBVault/ViewModels/FileExplorerViewModel.cs` | Rewrite skeleton |
| `XBVault/Views/FileExplorerView.axaml` | Rewrite placeholder |
| `XBVault/Views/FileExplorerView.axaml.cs` | Wire code-behind if needed |
| `Assets/Views/FileExplorerView/*.png` | New icons |

## Questions to Answer When Resuming
1. **API endpoints** — provide curl calls for:
   - `GET` to list root directories (`/api/filesystem/apps/`? what are the roots?)
   - `GET` to list a folder's contents (same endpoint with path?)
   - `PUT/POST` to upload a file
   - `DELETE` to remove files/folders
   - Any `mkdir` endpoint?

2. **Operations** — beyond browse + upload, want:
   - Download file (click to save?)
   - Delete (right-click context menu?)
   - Create new folder
   - Rename

3. **Layout preference** — split-pane (tree left + contents right) as drawn above, or full-width tree with upload card at bottom?

4. **Upload feedback** — show progress per file? or just success/fail toast?

5. **Delete behavior** — confirm dialog before delete? (following existing `ConfirmWindow` pattern)

## Future Ideas (discuss before implementing)
- File preview in right pane (text, image, hex)
- Multi-select in contents list
- Drag files from tree to upload card to copy/move

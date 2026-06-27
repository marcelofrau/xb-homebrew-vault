## ADDED Requirements

### Requirement: SFTP connection uses Xbox DevTools credentials

The system SHALL establish an SFTP connection to the Xbox using the same credentials obtained from the Dev Portal SMB endpoint.

#### Scenario: Get SSH credentials from XboxDeviceService
- **GIVEN** the user is connected to an Xbox via WDP
- **WHEN** `FileExplorerViewModel` activates
- **THEN** it SHALL call `XboxDeviceService.GetSshCredentials()` to obtain host, port (22), username (`DevToolsUser`), and password
- **THEN** `SftpService.ConnectAsync()` SHALL be called with these credentials

#### Scenario: Connection failure shows error
- **GIVEN** the SSH connection attempt fails
- **WHEN** `ConnectAsync()` throws or returns false
- **THEN** `ErrorMessage` SHALL be set to a descriptive message
- **THEN** the view SHALL display "Not connected" overlay with the error message

### Requirement: TreeView shows filesystem hierarchy

The system SHALL display a TreeView on the left side showing `D:\DevelopmentFiles\` as the root, with lazy-loaded children.

#### Scenario: Load tree roots
- **WHEN** the SSH connection succeeds
- **THEN** `SftpService.ListDirectoryAsync(@"D:\DevelopmentFiles\")` SHALL be called
- **THEN** the result SHALL populate `TreeRoots` with top-level entries
- **THEN** directories SHALL show a folder icon, files SHALL show a file icon

#### Scenario: Expand folder loads children
- **WHEN** the user expands a directory node in the tree
- **THEN** `SftpService.ListDirectoryAsync(fullPath)` SHALL be called
- **THEN** children SHALL be loaded and displayed under the expanded node
- **THEN** the parent node SHALL show the open folder icon

#### Scenario: Single-expand collapses other roots
- **WHEN** the user expands a different root node
- **THEN** the previously expanded root SHALL collapse
- **THEN** only the new root's children SHALL remain visible

#### Scenario: Mount Drives button
- **WHEN** the user clicks "Mount Drives"
- **THEN** `SftpService.RunShellCommandAsync("mklink /J D:\\DevelopmentFiles\\C C:\\")` SHALL run
- **THEN** similar commands for `D:` and `E:` SHALL run
- **THEN** the TreeView SHALL refresh
- **THEN** if any command fails, a descriptive error SHALL be shown
- **THEN** if all succeed, a success message SHALL be shown

### Requirement: File list shows contents of selected folder

The system SHALL display a file list on the right side showing contents of the selected folder from the tree or breadcrumb.

#### Scenario: Click folder in tree shows contents
- **WHEN** the user clicks (or selects) a folder in the tree
- **THEN** `CurrentEntries` SHALL be populated with the folder's contents
- **THEN** each entry SHALL show: icon, name, size (formatted), last modified date
- **THEN** directories SHALL be listed before files, sorted alphabetically

#### Scenario: Breadcrumb shows current path
- **WHEN** the user navigates to a folder
- **THEN** the breadcrumb bar SHALL show the full path as clickable segments
- **WHEN** the user clicks a breadcrumb segment
- **THEN** the view SHALL navigate to that parent folder

#### Scenario: Empty folder shows empty state
- **WHEN** a folder has no visible entries
- **THEN** the file list SHALL show a centered "This folder is empty" message

### Requirement: Upload files via drag-drop or file picker

The system SHALL accept files via drag-drop onto the upload card or via a file picker dialog.

#### Scenario: Drop files onto upload card
- **GIVEN** the user drags files from the OS onto the upload card
- **WHEN** the files are dropped
- **THEN** `SftpService.UploadFileAsync()` SHALL be called for each file
- **THEN** the upload progress SHALL be displayed via `UploadProgress` and `UploadStatusText`
- **THEN** on completion, a success toast SHALL appear per file
- **THEN** the file list SHALL refresh

#### Scenario: Browse Files button
- **WHEN** the user clicks "Browse Files"
- **THEN** an `OpenFileDialog` SHALL open (multi-file selection enabled)
- **WHEN** the user selects files and confirms
- **THEN** upload SHALL proceed same as drag-drop above

#### Scenario: Upload progress shown
- **WHEN** an upload is in progress
- **THEN** `IsUploading` SHALL be true
- **THEN** `UploadProgress` SHALL update (0.0–1.0)
- **THEN** `UploadStatusText` SHALL show "Uploading filename... (X%)"
- **WHEN** upload completes or fails
- **THEN** `IsUploading` SHALL return to false
- **THEN** error or success SHALL be communicated

### Requirement: Download files via right-click context menu

The system SHALL allow downloading files from the Xbox to the local machine.

#### Scenario: Download file
- **WHEN** the user right-clicks a file and selects "Download"
- **THEN** a `SaveFileDialog` SHALL open with the file name pre-filled
- **WHEN** the user selects a save location
- **THEN** `SftpService.DownloadFileAsync()` SHALL run
- **THEN** progress SHALL be shown (indeterminate or bytes-based)

### Requirement: Delete files/folders with confirmation

The system SHALL allow deleting files and folders via right-click context menu, with confirmation.

#### Scenario: Delete entry
- **WHEN** the user right-clicks a file/folder and selects "Delete"
- **THEN** a `ConfirmWindow` SHALL appear (reuse existing pattern): "Delete [name]?"
- **WHEN** the user confirms
- **THEN** `SftpService.DeleteFileAsync()` or `SftpService.DeleteDirectoryAsync()` SHALL run
- **THEN** the file list SHALL refresh
- **WHEN** the user cancels
- **THEN** no action SHALL be taken

### Requirement: Rename files/folders

The system SHALL allow renaming files and folders via right-click context menu.

#### Scenario: Rename entry
- **WHEN** the user right-clicks a file/folder and selects "Rename"
- **THEN** an inline rename text box SHALL appear (or a small dialog)
- **WHEN** the user enters a new name and confirms
- **THEN** `SftpService.RenameAsync()` SHALL run
- **THEN** the file list SHALL refresh

### Requirement: Create new folder

The system SHALL allow creating a new folder in the current directory.

#### Scenario: Create folder
- **WHEN** the user clicks the "New Folder" toolbar button
- **THEN** a prompt SHALL ask for the folder name
- **WHEN** the user enters a name and confirms
- **THEN** `SftpService.CreateDirectoryAsync()` SHALL run
- **THEN** the file list SHALL refresh

### Requirement: SftpService is disposable and reconnectable

The system SHALL properly manage SSH connection lifecycle.

#### Scenario: Disconnect on tab leave or app close
- **WHEN** the File Explorer tab is hidden or the app closes
- **THEN** `SftpService.Disconnect()` SHALL be called
- **THEN** the SSH connection SHALL be closed

#### Scenario: Reconnect on tab re-entry
- **WHEN** the user returns to the File Explorer tab
- **THEN** if `IsConnected` is false, `ConnectAsync()` SHALL be called again

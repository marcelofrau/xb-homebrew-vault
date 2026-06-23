# usb-permission-wizard Specification

## Purpose
TBD - created by archiving change usb-permission-wizard. Update Purpose after archive.
## Requirements
### Requirement: Wizard opens from Tools tab

The system SHALL provide a button labeled "Activate USB Media Drive" in the Tools tab's MANAGEMENT section that opens the USB permission wizard.

#### Scenario: Button visible when connected or disconnected
- **WHEN** the Tools tab is active
- **THEN** the "Activate USB Media Drive" button SHALL be visible
- **THEN** the button SHALL NOT be disabled when Xbox is disconnected (wizard works offline)

#### Scenario: Clicking button opens wizard
- **WHEN** the user clicks "Activate USB Media Drive"
- **THEN** the `UsbPermissionWindow` SHALL open as a modal dialog centered on `MainWindow`

### Requirement: Step 1 enumerates USB drives via WMI

The system SHALL enumerate all USB-attached drives using WMI and display them for selection.

#### Scenario: USB drives detected
- **WHEN** the wizard opens
- **THEN** WMI SHALL query `Win32_DiskDrive WHERE InterfaceType='USB'`
- **THEN** each drive's partitions SHALL be resolved to logical drive letters
- **THEN** each drive SHALL display as: `D: - Label (HDD)` or `F: - Label (USB Stick)`
- **THEN** the system drive (where Windows is installed) SHALL be filtered out

#### Scenario: No USB drives detected
- **WHEN** WMI returns zero USB drives
- **THEN** a message "No USB drives detected" SHALL be displayed
- **THEN** a "Refresh" button SHALL be available to re-query

#### Scenario: Drive details shown on selection
- **WHEN** the user selects a drive from the list
- **THEN** the following details SHALL be displayed: drive letter, volume label, total size (formatted), filesystem type, drive type (HDD/USB Stick)
- **THEN** if the filesystem is not NTFS, an informational message SHALL be shown: "Drive is not NTFS. Formatting to NTFS is optional but recommended for Xbox compatibility."
- **THEN** the "Next" button SHALL remain enabled regardless of filesystem type
- **THEN** the Format step (step 1) SHALL present formatting instructions as optional advice with an "Open Disk Management" shortcut; the user MAY skip it

### Requirement: Step 2 grants ALL APPLICATION PACKAGES permission

The system SHALL recursively grant `ALL APPLICATION PACKAGES` (SID `S-1-15-2-1`) FullControl on the selected drive.

#### Scenario: Permissions applied successfully
- **WHEN** the user clicks "Apply" on step 2
- **THEN** the system SHALL run `icacls <drive>:\ /grant "ALL APPLICATION PACKAGES:(OI)(CI)(F)" /T /Q`
- **THEN** an indeterminate progress bar SHALL be shown
- **THEN** status text SHALL read "Applying permissions..."
- **THEN** on exit code 0, the system SHALL advance to step 3 with a success state

#### Scenario: Permissions fail
- **WHEN** the `icacls` process exits with a non-zero code
- **THEN** the system SHALL capture stderr output
- **THEN** the system SHALL advance to step 3 with a failure state
- **THEN** the error details SHALL be displayed in a scrollable text block

#### Scenario: Admin rights missing
- **WHEN** the user is not running as Administrator
- **THEN** BEFORE applying, the system SHALL attempt `icacls` and detect access denied
- **THEN** a message SHALL suggest: "Run XBVault as Administrator to set drive permissions"

#### Scenario: Protected system directories are skipped
- **WHEN** applying permissions recursively
- **THEN** the system SHALL skip `System Volume Information` and `$Recycle.Bin` directories to avoid access-denied errors from TrustedInstaller-protected folders
- **THEN** the root ACL SHALL be set first without recursion to establish inheritance flags
- **THEN** each top-level item SHALL be processed individually with `/T`, excluding protected directories
- **THEN** the wizard SHALL NOT fail when only protected directories could not be modified

#### Scenario: Partial success (protected dirs skipped)
- **WHEN** permissions apply successfully to all user-visible items but protected system directories are skipped
- **THEN** the wizard SHALL show success (drive is usable for Xbox)
- **THEN** a note MAY indicate that some system-protected folders were skipped

#### Scenario: Drive becomes unavailable during apply
- **WHEN** the drive is disconnected during permission application
- **THEN** the `icacls` process SHALL fail with an error
- **THEN** step 3 SHALL show the failure with the disconnect message

### Requirement: Step 3 shows result with instructions

The system SHALL display a clear result screen after permission application completes.

#### Scenario: Success screen
- **WHEN** permissions were applied successfully
- **THEN** a green checkmark SHALL be displayed
- **THEN** the heading SHALL read "Drive ready for Xbox!"
- **THEN** instructions SHALL be displayed:
  - "1. Safely eject the USB drive from your PC"
  - "2. Plug it into your Xbox"
  - "3. On the Xbox, go to Media Player (NOT Games & Apps)"
  - "4. Your drive should appear as a media source"
- **THEN** only a "Close" button SHALL be available

#### Scenario: Failure screen
- **WHEN** permissions failed to apply
- **THEN** a red X SHALL be displayed
- **THEN** the heading SHALL read "Failed to apply permissions"
- **THEN** the error details SHALL be shown in a scrollable bordered area
- **THEN** a "Try Again" button SHALL return to step 1
- **THEN** a "Close" button SHALL close the wizard

### Requirement: Wizard follows the existing window template

The system SHALL use the same visual template as `CustomInstallWindow`.

#### Scenario: Window appearance
- **WHEN** the wizard is displayed
- **THEN** it SHALL be 600x500 pixels, non-resizable, centered on the owner window
- **THEN** it SHALL have no window decorations (`WindowDecorations="None"`)
- **THEN** the root border SHALL be `#447F3E` with 2px thickness
- **THEN** the title bar SHALL have the `#447F3E → #9ACA3C` gradient background
- **THEN** a 32x32 close button SHALL be in the top-right corner with red hover (#CC3333)
- **THEN** dragging the title bar SHALL move the window via `BeginMoveDrag`

### Requirement: Step indicators use active/disabled icon pairs

The system SHALL show colored step icons for the active step and grayscale versions for inactive steps.

#### Scenario: Step indicator states
- **WHEN** a step is active
- **THEN** its icon SHALL be displayed in full color
- **THEN** its label SHALL use the active foreground color
- **WHEN** a step is inactive (not yet reached)
- **THEN** its icon SHALL be displayed in grayscale
- **THEN** its label SHALL use a muted foreground color
- **WHEN** a step has been completed (previous step)
- **THEN** its icon SHALL remain in full color (same as active)


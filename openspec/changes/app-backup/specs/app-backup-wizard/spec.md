## ADDED Requirements

### Requirement: Per-app backup entry
The Installed tab SHALL offer a "Backup app" action in each app's flyout.

#### Scenario: Open backup dialog
- **WHEN** the user selects "Backup app" for an installed app
- **THEN** a backup dialog opens for that app

### Requirement: Remote folder selection
The backup dialog SHALL let the user multi-select additional remote folders (SSH/SFTP, same style as the file explorer) to include in the backup. This selection SHALL be optional.

#### Scenario: Select folders
- **WHEN** the user selects one or more remote folders
- **THEN** those folders are included in the backup

#### Scenario: No folders selected
- **WHEN** the user selects no extra folders
- **THEN** the backup proceeds without the custom-folders part

### Requirement: Destination selection
The user SHALL choose the backup destination, defaulting to `%USERPROFILE%/XBVault-backups`.

#### Scenario: Default destination
- **WHEN** the user does not change the destination
- **THEN** the ZIP is written to `%USERPROFILE%/XBVault-backups`

## ADDED Requirements

### Requirement: Appx package pull
When the `.appx` package for the app is retrievable from the console, the system SHALL download it into the backup. If not retrievable, the backup SHALL continue without it and record the omission.

#### Scenario: Appx retrieved
- **WHEN** the appx is available via a supported console source
- **THEN** it is downloaded into the backup

#### Scenario: Appx not retrievable
- **WHEN** no reliable appx source is available for the app
- **THEN** the backup completes without the appx and the manifest records it

### Requirement: LocalAppData / LocalState pull
The system SHALL recursively pull the app's LocalAppData/LocalState folder via `PortalAppFilesService` (REST filesystem) with progress and cancellation.

#### Scenario: LocalState downloaded
- **WHEN** the app has a LocalAppData/LocalState folder
- **THEN** its contents are recursively copied into the backup

#### Scenario: Pull cancelled
- **WHEN** the user cancels during the pull
- **THEN** the backup task is cancelled cleanly and no partial ZIP is left

### Requirement: Custom folder pull
The system SHALL recursively pull each user-selected remote folder via SFTP with progress.

#### Scenario: Custom folders downloaded
- **WHEN** the user selected remote folders
- **THEN** each is recursively copied into the backup

#### Scenario: SFTP failure
- **WHEN** a selected remote folder cannot be read
- **THEN** the failure is recorded in the manifest and the backup continues with the remaining parts (or fails per task rules)

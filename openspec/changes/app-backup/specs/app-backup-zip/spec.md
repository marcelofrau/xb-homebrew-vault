## ADDED Requirements

### Requirement: Backup ZIP assembly
The system SHALL assemble the backup into a single timestamped `.xvbk` ZIP containing the appx (when present), the LocalAppData/LocalState folder, the selected custom folders, and a `manifest.json` describing app, version, parts present/omitted, and timestamps.

#### Scenario: Complete ZIP
- **WHEN** all selected parts succeed
- **THEN** one `.xvbk` ZIP is produced with all parts and a manifest

#### Scenario: Partial ZIP
- **WHEN** some parts are omitted (no appx, no custom folders)
- **THEN** the ZIP still assembles and the manifest lists the omitted parts

### Requirement: Progress + task center
Backup SHALL run as a `BackgroundTaskService` task reporting progress, visible in the task center, with clean cancellation (temp file + move, no partial ZIP at destination).

#### Scenario: Progress reported
- **WHEN** the backup is running
- **THEN** progress is reported through the task center

#### Scenario: No partial ZIP
- **WHEN** the backup is cancelled or fails
- **THEN** no incomplete ZIP remains at the destination

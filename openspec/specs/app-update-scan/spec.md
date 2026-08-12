# app-update-scan

## Purpose

Background scanning for installed-package updates against the catalog. Defines when the scan runs, its gating conditions, and dedupe so users are only alerted once per version pair.

## Requirements

### Requirement: Background update scan
While connected to the Xbox, the system SHALL run an update-scan job that compares installed apps against the catalog (same PFN-match/version logic used by the OUTDATED badge) and reports the set of updatable apps.

#### Scenario: Scan on connect
- **WHEN** the app connects to the Xbox and the catalog is loaded
- **THEN** an update scan runs

#### Scenario: Periodic scan while connected
- **WHEN** the configured interval elapses while still connected
- **THEN** another scan runs

#### Scenario: No scan when disconnected
- **WHEN** the app is not connected to the Xbox
- **THEN** no scan runs

#### Scenario: Catalog not loaded
- **WHEN** the catalog is unavailable during a scan
- **THEN** the scan is skipped with a log entry

### Requirement: Dedupe with cache
The system SHALL reuse `UpdateVersionCache` so the same installed→catalog version pair is not re-notified.

#### Scenario: No duplicate notification
- **WHEN** a second scan finds the same version pair
- **THEN** no new notification is raised for it

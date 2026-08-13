# app-autostart-badge

## Purpose

Visual indicator on the installed-app card showing which app is autostart-enabled.

## Requirements

### Requirement: Autostart badge
The autostart-enabled app SHALL display a badge in the top-left corner of its card, matching the OUTDATED badge style and placement, with an indicative color distinguishing it from other badges.

#### Scenario: Badge shown on enabled app
- **WHEN** an app is autostart-enabled
- **THEN** its card shows the autostart badge (top-left) with indicative color

#### Scenario: Badge removed on disable
- **WHEN** autostart is removed from the app
- **THEN** the badge disappears

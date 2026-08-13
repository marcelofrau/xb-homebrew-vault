# app-autostart-toggle

## Purpose

Per-app autostart selection from the Installed tab: enable with confirmation, single-app exclusivity with replace flow, and persistence in app settings.

## Requirements

### Requirement: Autostart option per app
The Installed tab SHALL offer an "Autostart on connect" action in each app's flyout, with an icon and a confirmation dialog before applying.

#### Scenario: Enable from flyout
- **WHEN** the user selects "Autostart on connect" for an app and confirms
- **THEN** that app becomes the autostart app

#### Scenario: Confirmation required
- **WHEN** the user selects the autostart action
- **THEN** a confirmation dialog is shown before any change is applied

### Requirement: Single-app exclusivity
Only one app SHALL be autostart-enabled at a time. Enabling a different app SHALL replace the previous selection after user confirmation, and SHALL NOT require a manual step to remove the old one.

#### Scenario: Replace selection
- **WHEN** app B is set as autostart while app A was autostart
- **THEN** the user is asked to confirm replacing A; on confirm, B becomes autostart and A is cleared

#### Scenario: Disable autostart
- **WHEN** the user removes autostart from the enabled app
- **THEN** no app is autostart and the selection is cleared

### Requirement: Persistence
The autostart selection SHALL be persisted in app settings and restored on next launch.

#### Scenario: Restored after restart
- **WHEN** the app relaunches after autostart was enabled
- **THEN** the same app is still marked as autostart

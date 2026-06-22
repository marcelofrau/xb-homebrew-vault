## ADDED Requirements

### Requirement: Wizard triggers on first launch when settings are empty

The system SHALL detect that no Xbox connection is configured and automatically show the setup wizard.

#### Scenario: No settings exist on first launch
- **WHEN** the application starts for the first time (no `settings.json` exists)
- **THEN** `SettingsService.Current.XboxConnection.IsConfigured` SHALL return false
- **THEN** the `SetupWizardWindow` SHALL open as a modal dialog centered on `MainWindow`

#### Scenario: Partial settings exist (e.g., address without password)
- **WHEN** the application starts and `XboxConnection` has some fields populated but not all required fields
- **THEN** `IsConfigured` SHALL return false
- **THEN** the wizard SHALL still appear (all fields will be pre-populated from existing partial settings)

#### Scenario: All settings are complete
- **WHEN** `XboxConnection.Address`, `XboxConnection.Username`, and `XboxConnection.EncryptedPassword` are all non-empty
- **THEN** `IsConfigured` SHALL return true
- **THEN** the wizard SHALL NOT appear

### Requirement: Wizard has three sequential steps

The system SHALL present a 3-step wizard with a left sidebar showing step indicators and a right panel with content for the active step.

#### Scenario: Wizard shows step 1 (Console) by default
- **WHEN** the wizard opens
- **THEN** step indicator 1 SHALL be highlighted as active
- **THEN** step indicators 2 and 3 SHALL appear in disabled/grayscale state
- **THEN** the content panel SHALL show fields for Xbox IP address, port (default 11443), and HTTPS toggle

#### Scenario: User advances to step 2 (Authentication)
- **WHEN** the user clicks "Next" on step 1 with valid inputs
- **THEN** step indicator 2 SHALL become active
- **THEN** step indicator 1 SHALL show as completed
- **THEN** the content panel SHALL show fields for Xbox username and password

#### Scenario: User advances to step 3 (Ready)
- **WHEN** the user clicks "Next" on step 2 with valid inputs
- **THEN** step indicator 3 SHALL become active
- **THEN** the content panel SHALL show a summary of all entered settings and a checkbox "Open connection window" (default: checked)

#### Scenario: User goes back to a previous step
- **WHEN** the user clicks "Back" on step 2 or 3
- **THEN** the previous step SHALL be shown with all previously entered values preserved

### Requirement: Step inputs are validated before advancing

The system SHALL validate that required fields are filled before allowing navigation to the next step.

#### Scenario: Next button disabled when step 1 fields are empty
- **WHEN** step 1 is active and Address is empty
- **THEN** the "Next" button SHALL be disabled

#### Scenario: Next button disabled when step 2 fields are empty
- **WHEN** step 2 is active and Username or Password is empty
- **THEN** the "Next" button SHALL be disabled

### Requirement: Settings are saved only on Finish

The system SHALL persist the entered configuration only when the user explicitly clicks Finish on step 3.

#### Scenario: User clicks Finish
- **WHEN** the user clicks "Finish" on step 3
- **THEN** `SettingsService.Save()` SHALL be called
- **THEN** `CryptoService.Obfuscate(Password)` SHALL be used before persisting
- **THEN** `XboxDeviceService.Configure()` SHALL be called with the new settings
- **THEN** the wizard SHALL close

#### Scenario: User cancels or closes wizard
- **WHEN** the user clicks the Cancel button, the close (X) button, or presses Escape
- **THEN** settings SHALL NOT be saved
- **THEN** the wizard SHALL close
- **THEN** `IsConfigured` SHALL remain false
- **THEN** the wizard SHALL reappear on the next application launch

### Requirement: Post-wizard connection dialog is optional

The system SHALL offer to open the ConnectionWindow immediately after Finish, with the option opt out.

#### Scenario: "Open connection window" is checked
- **WHEN** the user clicks Finish with the checkbox checked (default)
- **THEN** after the wizard closes, `MainViewModel.ShowConnectAction` SHALL be invoked
- **THEN** the standard `ConnectionWindow` SHALL open, using the just-saved settings

#### Scenario: "Open connection window" is unchecked
- **WHEN** the user clicks Finish with the checkbox unchecked
- **THEN** the wizard SHALL close
- **THEN** no further dialog SHALL open
- **THEN** the MainWindow status SHALL show "Disconnected" (settings exist but not connected)

### Requirement: Wizard follows the existing window template

The system SHALL use the same visual template as CustomInstallWindow.

#### Scenario: Window appearance
- **WHEN** the wizard is displayed
- **THEN** it SHALL be 600x500 pixels, non-resizable, centered on the owner window
- **THEN** it SHALL have no window decorations (`WindowDecorations="None"`)
- **THEN** the root border SHALL be `#447F3E` with 2px thickness
- **THEN** the title bar SHALL have the `#447F3E → #9ACA3C` gradient background
- **THEN** a 32x32 close button SHALL be in the top-right corner with red hover (#CC3333)
- **THEN** dragging the title bar SHALL move the window via `BeginMoveDrag`

### Requirement: Step indicators use active/disabled icon pairs

The system SHALL show colored step icons for the active/completed step and grayscale versions for inactive steps.

#### Scenario: Step indicator states
- **WHEN** a step is active
- **THEN** its icon SHALL be displayed in full color
- **THEN** its label SHALL use the active foreground color
- **WHEN** a step is inactive (not yet reached)
- **THEN** its icon SHALL be displayed in grayscale
- **THEN** its label SHALL use a muted foreground color
- **WHEN** a step has been completed (previous step)
- **THEN** its icon SHALL remain in full color (same as active)

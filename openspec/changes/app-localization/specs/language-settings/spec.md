## ADDED Requirements

### Requirement: Language dropdown in SettingsView

The system SHALL display a language selector in the SettingsView that shows all available cultures.

#### Scenario: Dropdown populated from AvailableCultures
- **WHEN** SettingsView loads
- **THEN** the language dropdown SHALL be populated from `LocalizationService.AvailableCultures`
- **THEN** each item SHALL show the culture display name (e.g., "English", "Português (Brasil)", "Español")
- **THEN** the currently selected culture SHALL be pre-selected

#### Scenario: Change language triggers restart prompt
- **WHEN** the user selects a different language from the dropdown
- **THEN** the new culture SHALL be saved to `SettingsService.Language`
- **THEN** a prompt SHALL appear: "Language changed. Restart now? [Restart] [Later]"
- **WHEN** the user clicks "Restart"
- **THEN** the app SHALL restart: launch a new process via `Process.Start()` and call `Application.Current.Shutdown()`
- **WHEN** the user clicks "Later"
- **THEN** the prompt SHALL close and the dropdown SHALL show the new language
- **THEN** the change SHALL take effect on next manual restart

### Requirement: Language persisted in settings.json

The system SHALL save the selected language to persistent settings and restore it on startup.

#### Scenario: Save on selection
- **WHEN** the user selects a language
- **THEN** `SettingsService.Save()` SHALL persist `language: "pt-BR"` (or equivalent code) to settings.json
- **THEN** the value SHALL survive app restart

#### Scenario: Restore on startup
- **WHEN** the application starts
- **THEN** `SettingsService.Language` SHALL be read
- **THEN** if a valid language is stored, `LocalizationService` SHALL load the corresponding JSON file
- **THEN** if no language is stored, default to `"en"`

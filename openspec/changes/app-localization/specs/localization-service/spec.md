## ADDED Requirements

### Requirement: LocalizationService loads JSON resources

The system SHALL provide an `ILocalizationService` that loads translation strings from JSON files at startup and serves them by key.

#### Scenario: Load default language on startup
- **WHEN** the application starts
- **THEN** `LocalizationService` SHALL read `CurrentCulture` from `SettingsService`
- **THEN** it SHALL load the corresponding JSON file from `Resources/{culture}.json`
- **THEN** all keys SHALL be available via `this[string key]`

#### Scenario: Fallback to English when key missing
- **WHEN** `this[string key]` is called with a key not present in the current culture file
- **THEN** the service SHALL look up the key in `Resources/en.json`
- **THEN** if found in English, SHALL return the English value
- **THEN** if not found in any file, SHALL return the key itself

#### Scenario: Format strings with positional parameters
- **WHEN** `GetString(key, arg0, arg1)` is called and the value contains `{0}`, `{1}`, etc.
- **THEN** the service SHALL call `string.Format(value, args)` and return the result

#### Scenario: AvailableCultures lists all JSON files
- **WHEN** `AvailableCultures` is accessed
- **THEN** it SHALL return the list of culture codes found as JSON files in `Resources/`
- **THEN** it SHALL always include at least the current culture and `"en"`

### Requirement: LocalizationService is a singleton

The service SHALL be registered as a singleton in the DI container and accessible globally.

#### Scenario: DI registration
- **WHEN** `ILocalizationService` is resolved from DI
- **THEN** the same instance SHALL be returned across all resolutions

#### Scenario: Static accessor for markup extension
- **WHEN** `I18nExtension.Provide()` runs
- **THEN** it SHALL access `LocalizationService.Instance` (static singleton)
- **THEN** the Instance SHALL be the same as the DI singleton

### Requirement: Adding a new language requires no code changes

The system SHALL detect available languages from the `Resources/` directory.

#### Scenario: New JSON file detected
- **GIVEN** a new file `Resources/fr.json` exists
- **WHEN** `AvailableCultures` is read
- **THEN** `"fr"` SHALL appear in the list
- **WHEN** the user selects `"fr"` and restarts
- **THEN** the app SHALL load `Resources/fr.json`
- **THEN** keys present in `fr.json` SHALL be used; missing keys SHALL fall back to English

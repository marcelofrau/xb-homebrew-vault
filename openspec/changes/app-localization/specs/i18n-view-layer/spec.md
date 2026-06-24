## ADDED Requirements

### Requirement: `{i18n Key}` markup extension resolves string at markup time

The system SHALL provide an Avalonia markup extension that looks up a translation key at control load time.

#### Scenario: Basic usage in XAML
- **GIVEN** a XAML file with `<TextBlock Text="{i18n ConnectionWindow.Title}" />`
- **WHEN** the control is loaded
- **THEN** the `I18nExtension.Provide()` method SHALL return the localized string from `LocalizationService.Instance["ConnectionWindow.Title"]`
- **THEN** if the key is valid, the localized value SHALL appear in the TextBlock

#### Scenario: Missing key shows placeholder
- **GIVEN** a XAML file with `{i18n Nonexistent.Key}`
- **WHEN** the control is loaded
- **THEN** the TextBlock SHALL display "Nonexistent.Key" (the key itself)

### Requirement: ViewModels inject ILocalizationService for dynamic strings

The system SHALL inject `ILocalizationService` into ViewModels that contain user-facing string properties.

#### Scenario: Constructor injection
- **GIVEN** a ViewModel with user-facing strings
- **WHEN** the ViewModel is constructed via DI
- **THEN** `ILocalizationService` SHALL be injected via constructor parameter
- **THEN** the ViewModel SHALL expose public string properties that delegate to `_loc["Key"]`
- **THEN** these properties SHALL be readable from XAML via data binding

#### Scenario: Strings with parameters in ViewModel
- **GIVEN** a ViewModel that needs formatted strings like "Downloading {0}... ({1}%)"
- **WHEN** the formatted string is accessed
- **THEN** it SHALL call `_loc.GetString("Key", param1, param2)`
- **THEN** the result SHALL be fully formatted

### Requirement: Shared/common keys are available app-wide

The system SHALL provide common keys (OK, Cancel, Save, Close, Error, etc.) for consistent use across all views.

#### Scenario: Common keys used in multiple views
- **WHEN** a view uses `{i18n Common.OK}` or `{i18n Common.Cancel}`
- **THEN** the same translation SHALL be used wherever that key appears
- **THEN** changing the value in the JSON file SHALL update every usage (after restart)

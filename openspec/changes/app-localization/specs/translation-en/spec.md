## ADDED Requirements

### Requirement: English base translation file

The system SHALL provide `Resources/en.json` as the authoritative source of all translation keys.

#### Scenario: All keys present
- **WHEN** the application is built
- **THEN** `en.json` SHALL contain every translation key used in the application
- **THEN** each value SHALL be the English text for that key

#### Scenario: Used as fallback
- **WHEN** any other language is active and a key is missing
- **THEN** the English value from `en.json` SHALL be used as fallback

### Requirement: Portuguese (Brazil) translation

The system SHALL provide `Resources/pt-BR.json` with Portuguese (Brazil) localized strings.

#### Scenario: Complete translation
- **WHEN** culture is set to `"pt-BR"`
- **THEN** all translated keys from `pt-BR.json` SHALL be used
- **THEN** any missing keys SHALL fall back to English

### Requirement: Spanish translation

The system SHALL provide `Resources/es.json` with Spanish localized strings.

#### Scenario: Complete translation
- **WHEN** culture is set to `"es"`
- **THEN** all translated keys from `es.json` SHALL be used
- **THEN** any missing keys SHALL fall back to English

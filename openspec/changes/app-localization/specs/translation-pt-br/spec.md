## ADDED Requirements

### Requirement: Portuguese (Brazil) translation

The system SHALL provide `Resources/pt-BR.json` with Portuguese (Brazil) localized strings.

#### Scenario: Complete translation
- **WHEN** culture is set to `"pt-BR"`
- **THEN** all translated keys from `pt-BR.json` SHALL be used
- **THEN** any missing keys SHALL fall back to English

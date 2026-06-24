## ADDED Requirements

### Requirement: Spanish translation

The system SHALL provide `Resources/es.json` with Spanish localized strings.

#### Scenario: Complete translation
- **WHEN** culture is set to `"es"`
- **THEN** all translated keys from `es.json` SHALL be used
- **THEN** any missing keys SHALL fall back to English

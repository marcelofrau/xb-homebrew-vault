## Why

XBVault is used by a global Xbox homebrew community. The UI is entirely in English, which limits accessibility for Portuguese and Spanish speakers — two of the largest homebrew audiences. Adding i18n support opens the app to non-English users and makes community-contributed translations easy.

## What Changes

- Add `Resources/` directory with JSON translation files (`en.json`, `pt-BR.json`, `es.json`)
- Create `ILocalizationService` / `LocalizationService` to load and serve strings
- Create custom Avalonia markup extension `{i18n Key}` for XAML usage
- Wire `LocalizationService` into ViewModels via DI
- Add language selector (dropdown) to SettingsView
- Persist selected language in `settings.json`
- On language change: save setting + prompt restart
- ~400 existing hardcoded strings moved to translation keys

## Capabilities

### New Capabilities
- `localization-service`: ILocalizationService + JSON loading + fallback chain + format strings
- `i18n-view-layer`: XAML markup extension, ViewModel integration, common/shared keys
- `language-settings`: Language dropdown in SettingsView, persist + restart flow
- `translation-en`: English base translation file
- `translation-pt-br`: Portuguese (Brazil) translation
- `translation-es`: Spanish translation

### Modified Capabilities
- (none — no existing specs change behavior)

## Impact

- **New files**: `XBVault/Resources/*.json`, `XBVault/Services/LocalizationService.cs`, `XBVault/MarkupExtensions/I18nExtension.cs`
- **Modified files**: `XBVault/XBVault.csproj`, `XBVault/Views/SettingsView.axaml`, `XBVault/ViewModels/SettingsViewModel.cs`, `XBVault/Services/SettingsService.cs`
- **Every .axaml file**: replace hardcoded strings with `{i18n Key}`
- **Every ViewModel**: inject `ILocalizationService` for user-facing strings
- **No new external dependencies** (uses built-in `System.Text.Json`)
- **No breaking changes** — all existing functionality preserved

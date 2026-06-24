## 1. Core Infrastructure

- [ ] 1.1 Create `XBVault/Services/ILocalizationService.cs` interface (GetString, indexer, AvailableCultures, CurrentCulture)
- [ ] 1.2 Create `XBVault/Services/LocalizationService.cs` (singleton, JSON loading, fallback chain, format support)
- [ ] 1.3 Register `ILocalizationService` as singleton in DI (Program.cs)
- [ ] 1.4 Create `Resources/en.json` with all ~350 English translation keys extracted from current UI
- [ ] 1.5 Add `Resources/` directory to csproj as `Content` for copy-to-output

## 2. XAML Markup Extension

- [ ] 2.1 Create `XBVault/MarkupExtensions/I18nExtension.cs` (implement `IMarkupExtension`, resolve from `LocalizationService.Instance`)
- [ ] 2.2 Register namespace in AXAML (`xmlns:i18n="using:XBVault.MarkupExtensions"`)

## 3. Settings Integration

- [ ] 3.1 Add `Language` property to `SettingsService` (string, default `"en"`)
- [ ] 3.2 Add language dropdown to `SettingsView.axaml` with `{i18n}` bindings
- [ ] 3.3 Add `Languages` and `SelectedLanguage` to `SettingsViewModel` with restart prompt logic
- [ ] 3.4 Implement restart flow: `Process.Start` + `Application.Current.Shutdown()`

## 4. ViewModel Localization

- [ ] 4.1 Inject `ILocalizationService` into ViewModels that contain user-facing strings
- [ ] 4.2 Add localized string properties to each ViewModel, delegating to `_loc["Key"]`

## 5. AXAML String Migration

- [ ] 5.1 Migrate **UsbPermissionWindow.axaml** (41 strings) to `{i18n}` keys
- [ ] 5.2 Migrate **SetupWizardWindow.axaml** (38 strings) to `{i18n}` keys
- [ ] 5.3 Migrate **CustomInstallWindow.axaml** (33 strings) to `{i18n}` keys
- [ ] 5.4 Migrate **ItemDetailWindow.axaml** (29 strings) to `{i18n}` keys
- [ ] 5.5 Migrate **SettingsView.axaml** (28 strings) to `{i18n}` keys
- [ ] 5.6 Migrate remaining 16 views (~138 strings) to `{i18n}` keys
- [ ] 5.7 Migrate all common/shared keys (OK, Cancel, Close, etc.) to `Common.*` prefix

## 6. Translation Files

- [ ] 6.1 Create `Resources/pt-BR.json` with all keys translated to Brazilian Portuguese
- [ ] 6.2 Create `Resources/es.json` with all keys translated to Spanish

## 7. Polish & Docs

- [ ] 7.1 Create `docs/I18N-GUIDE.md` with key naming conventions and instructions for adding new languages
- [ ] 7.2 Verify all views render correctly with each language (en, pt-BR, es)
- [ ] 7.3 Verify fallback behavior by temporarily removing a key from pt-BR.json

## Context

XBVault has ~393 hardcoded user-facing strings spread across 21 AXAML views and 19 ViewModels. Zero localization infrastructure exists. The app uses Avalonia UI with MVVM pattern, DI via constructor injection, and settings persisted in `%APPDATA%/XBVault/settings.json`.

Language support targets: English (base), Portuguese-BR, Spanish. Community translators must be able to add new languages by dropping a single JSON file.

## Goals / Non-Goals

**Goals:**
- All user-facing strings externalized to JSON translation files
- `{i18n Key}` markup extension works in any AXAML file
- ViewModels access strings via injected `ILocalizationService`
- Language selector dropdown in SettingsView
- Language persists across restarts via settings.json
- Adding a new language = drop `xx.json` in Resources/ + restart
- Zero new NuGet dependencies

**Non-Goals:**
- Runtime language switch without restart (explicitly deferred)
- Plural forms engine (simple string.Format with positional args only)
- RTL language support
- Localization of log messages or internal identifiers
- Translation management UI or crowdsourcing portal

## Decisions

### Format: JSON over RESX
JSON is universally editable, works with translation tools (Crowdin/POEditor), and requires no code generation. The existing `System.Text.Json` dependency is reused. RESX would require `.Designer.cs` regeneration and is unfriendly to translators.

### Restart-only switch over runtime reactivity
Avalonia supports `DynamicResource` and custom bindings for live updates, but the complexity (notifying 300+ bindings, handling in-progress operations) outweighs the benefit. A restart dialog ("Language changed. Restart now? [Later] [Restart]") is simpler, more reliable, and avoids edge cases.

### Flat internal storage with dotted keys
Translation JSON uses `ConnectionWindow.Title` style keys for readability and grouping. Internally the service loads into `Dictionary<string, string>` — no nested JSON traversal needed. The XAML extension and ViewModel API accept the same dotted key format.

### Fallback chain: requested → en → key-as-displayed
If `pt-BR.json` is missing key `Foo.Bar`, fall back to `en.json`. If `en.json` also missing, return the key itself (`Foo.Bar`) as a visible signal that the translation is missing.

```
Architecture:

Resources/en.json        Resources/pt-BR.json
     │                         │
     ▼                         ▼
LocalizationService ─── Dictionary<string,string>
     │
     ├── this[string key]  →  ViewModel usage
     ├── GetString(key, args) →  format support
     └── CurrentCulture     →  "pt-BR"
              │
              ▼
       SettingsService (persist)
```

### Markup Extension returns string at Provide time
Since restart-only, `I18nExtension.Provide()` resolves immediately from `LocalizationService.Instance[key]`. No bindings, no observables.

```xml
<!-- usage -->
<TextBlock Text="{i18n ConnectionWindow.Title}" />
```

### DI injection for ViewModels
`ILocalizationService` registered as singleton in DI. ViewModels that display user-facing strings receive it via constructor injection.

```csharp
public class SettingsViewModel : ViewModelBase
{
    private readonly ILocalizationService _loc;
    public string LanguageLabel => _loc["Settings.Language"];
}
```

### Settings integration
`SettingsService` gains a `Language` property (string, default `"en"`). `SettingsViewModel` exposes a `Languages` collection (from `LocalizationService.AvailableCultures`) and a `SelectedLanguage` property. On change: save + show restart dialog.

### No base class changes
Existing ViewModelBase untouched. Localization can be adopted incrementally — ViewModels opt in by injecting the service and adding properties.

## Risks / Trade-offs

- **Key naming drift**: Over time, developers might invent inconsistent key names. Mitigation: establish a naming convention doc in `docs/I18N-GUIDE.md` and review in PRs.
- **Missing translations**: If a key is missing from all JSON files, the raw key shows in UI. Mitigation: document this behavior so testers recognize it.
- **Performance**: Loading JSON at startup adds ~5ms per file. Mitigation: negligible for <10KB files with ~400 keys.
- **Scope creep**: 21 views × ~15 strings each = ~315 AXAML edits. Mitigation: do views in batches, not all at once.

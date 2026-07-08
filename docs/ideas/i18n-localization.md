# Localization (i18n)

**Impact:** High | **Effort:** High | **Suggested priority:** Phase 2

## Problem

All ~393 UI strings are hardcoded across 21 AXAML files + 19 ViewModels. Non-PT/EN users are excluded.

## Current state

Full specification already exists in `openspec/changes/app-localization/` with:
- `proposal.md` — rationale and scope
- `design.md` — architecture, decisions, risks
- `tasks.md` — 45 tasks in 7 sections (0% complete)
- `specs/localization-service/spec.md` — interface, fallback chain, format strings
- `specs/i18n-view-layer/spec.md` — markup extension `{i18n Key}`
- `specs/language-settings/spec.md` — dropdown + restart
- `specs/translation-en/spec.md`, `specs/translation-pt-br/spec.md`, `specs/translation-es/spec.md`

## Design decisions (from design.md)

| Decision | Choice | Reason |
|----------|--------|--------|
| Format | JSON (not RESX) | Editable manually, no codegen |
| Language switch | Requires restart | Avoids complex runtime reactivity |
| Keys | Dotted (`ConnectionWindow.Title`) | Clear hierarchy, flat lookup |
| Fallback | Requested → en.json → key itself | Resilience |
| Markup extension | `{i18n Key}` resolves in `Provide()` | Clean XAML syntax |
| Dependencies | Zero new NuGet | Keeps footprint small |

## 45 tasks summary

1. **Core** (6 tasks): interface, service, DI, `en.json`, csproj
2. **XAML** (5 tasks): markup extension, namespace registration, designer fallback
3. **Settings** (5 tasks): Language prop, dropdown, restart flow, persist
4. **ViewModels** (7 tasks): inject `ILocalizationService`
5. **AXAML** (12 tasks): migrate ~393 strings in 21 views
6. **Translations** (6 tasks): `pt-BR.json`, `es.json`, review
7. **Polish** (4 tasks): `docs/I18N-GUIDE.md`, visual verification

## Risks
- Large scope — 393 strings to migrate
- Markup extension requires registration in `App.axaml` and XAML namespace
- Strings in ViewModels (error messages, log) need manual migration
- Restart on language switch is acceptable UX but not ideal

## Files to create
- `Services/ILocalizationService.cs` + `Services/LocalizationService.cs`
- `Converters/I18nExtension.cs` (markup extension)
- `Assets/i18n/en.json`
- `Assets/i18n/pt-BR.json`
- `Assets/i18n/es.json`
- `docs/I18N-GUIDE.md`

## Files to modify
- All 21 AXAMLs (~380 substitutions)
- 19 ViewModels (error/log strings)
- `SettingsView.axaml` — Language dropdown
- `SettingsViewModel.cs` — Language prop + restart
- `Program.cs` — DI
- `App.axaml` — namespace + resource

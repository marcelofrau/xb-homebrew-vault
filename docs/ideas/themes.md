# Additional Themes

**Impact:** Medium | **Effort:** Medium | **Suggested priority:** Phase 3

## Problem

The Blades (Xbox 360) theme is beautiful and unique, but may not appeal to all users. Some may prefer a light theme for daytime use, or a default dark theme.

## Current state

- Blades theme fully implemented in `App.axaml` via `ResourceDictionary`
- Colors defined as resources (`BgBrush`, `SurfaceBrush`, `AccentBrush`, `TextBrush`, etc.)
- Title bar gradient via `TitleGradient`
- Buttons globally styled

## Proposal

### Theme system
- `ThemeService` — switches between `ResourceDictionary` at runtime
- Themes are separate AXAML files in `Assets/Themes/`
- Switch via dropdown in SettingsView (no restart)

### Suggested themes

| Theme | Description | Audience |
|-------|-------------|----------|
| **Blades** (current) | Xbox 360 dark green + gradient | Default, nostalgic |
| **Xbox Dark** | Black/dark gray, green accent (Series X|S dashboard) | Modern Xbox |
| **Light** | Light background, dark text, blue accent | Daytime use |
| **High Contrast** | Maximum contrast, accessibility | Low-vision users |
| **Custom** | Accent color + background picker | Advanced users |

### ThemeService
```csharp
class ThemeService
{
    event Action<Theme> ThemeChanged;
    void ApplyTheme(Theme theme);
    Theme CurrentTheme { get; }
    IReadOnlyList<Theme> AvailableThemes { get; }
}

record Theme(string Name, Uri ResourceUri);
```

### Runtime switch
- `Application.Current.Resources.MergedDictionaries.Clear()`
- `Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri })`
- `ThemeChanged` event → ViewModels can react if needed

### Implementation considerations
- Themes must define **exactly the same resource keys** as the current Blades theme
- Custom buttons and controls (`CdSpinner`, `DialogFadeBehavior`) must work on all themes
- Assets/images can differ per theme (e.g., different sidebar bg in Light)
- Test on all views before releasing

### Dependencies
- No new NuGet
- Only reorganization of `App.axaml` resources

### Files to create
- `Services/ThemeService.cs`
- `Assets/Themes/Blades.axaml`
- `Assets/Themes/XboxDark.axaml`
- `Assets/Themes/Light.axaml`
- `Assets/Themes/HighContrast.axaml`

### Files to modify
- `App.axaml` — extract current resources to `Blades.axaml`, register `ThemeService`
- `SettingsView.axaml` — "Theme" dropdown + accent color picker
- `SettingsViewModel.cs` — Theme prop + ApplyTheme
- `Program.cs` — DI
- `App.axaml.cs` — init ThemeService

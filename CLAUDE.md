# XBVault — Session Memory

## Objective
Implement outdated package UI enhancements, update flow improvements, and About window changelog link for XBVault v1.2.0.

## Key Facts
- **Project**: .NET 8 + Avalonia UI desktop app at `F:\workspace\xb-homebrew-vault`
- **Dotnet**: use `rtk dotnet` prefix or `& "C:\Program Files\dotnet\dotnet.exe"` — NOT plain `dotnet`
- **Version**: `1.2.0` in `XBVault/XBVault.csproj:15`
- **Branching**: `feat/<name>`, `fix/<name>`, `chore/<name>` — branch off `main`, merge back
- **Icons**: copy from `F:\workspace\icons8-personal-set\20x20\` → `XBVault/Assets/Views/{ViewName}/`, named `{viewname}-{descriptor}-{size}.png`. Disabled variants: ImageMagick with `-alpha set -channel A -evaluate set 35% +channel PNG32:` (PNG32 sRGB with alpha, NOT Grayscale)
- **No git commit/push** unless explicitly asked and confirmed

## Completed Work

### Version bump 1.1.2 → 1.2.0
- `XBVault/XBVault.csproj:15`

### Changelog link in About window
- **Files**: `XBVault/Views/AboutWindow.axaml`, `XBVault/Views/AboutWindow.axaml.cs:29`
- Button "Release Notes" under GitHub link with `about-changelog-20.png` icon
- `OnChangelogClick` opens `https://github.com/marcelofrau/xb-homebrew-vault/releases`

### Descriptive tooltips on sidebar nav items
- **File**: `XBVault/MainWindow.axaml` lines 269-352
- 5 items: Browse ("Browse homebrew catalog"), Installed ("Manage installed packages"), File Explorer ("Browse Xbox filesystem"), Tools ("Utilities and diagnostics"), Inspector ("Xbox connection inspector")

### Auto-update check via GitHub Releases API
- **Files**: `XBVault/Services/GitHubReleaseCheckerService.cs`, `XBVault/App.axaml.cs:686`
- Hits `https://api.github.com/repos/marcelofrau/xb-homebrew-vault/releases/latest`
- Parses tag, compares with `BuildInfo.Version`, shows info ErrorDialog with "Download" button
- Dialog uses `errordialog-download-20.png` icon + `DownloadAction` property

### Sort catalog by ReleaseDate + NEW/UPDATE badge
- **Files**: `XBVault/Models/CatalogItem.cs:79`, `XBVault/ViewModels/BrowseViewModel.cs:608`, `XBVault/Views/BrowseView.axaml:199`, `XBVault/Models/CatalogApi.cs:53`, `XBVault/Services/CatalogApiService.cs:190,346`
- `FirstReleaseDate` parsed from API JSON
- `IsNewRelease`: `releaseDate == firstReleaseDate` within 14d → green `#2ECC71` "NEW"
- `IsUpdate`: `releaseDate > firstReleaseDate` within 14d → blue `#3498DB` "UPDATE"
- `ShowBadge` = `IsNewRelease || IsUpdate`
- `BadgeBrush` returns `SolidColorBrush` (IBrush) — works with Avalonia binding directly
- `ApplyFilters()` sorts descending by parsed `ReleaseDate`

### ErrorDialog Download button
- **Files**: `XBVault/Views/ErrorDialog.axaml:115`, `XBVault/Views/ErrorDialog.axaml.cs:42`
- `DownloadAction` property shows/hides button; `OnDownloadClick` invokes it
- Icon: `errordialog-download-20.png`

## Relevant Files
- `XBVault/XBVault.csproj`: Version 1.2.0
- `XBVault/App.axaml.cs`: Auto-update check at line 686; `Process` import
- `XBVault/Helpers/BuildInfo.cs`: Version from `AssemblyInformationalVersionAttribute`
- `XBVault/Views/AboutWindow.axaml.cs`: `OnChangelogClick`
- `XBVault/MainWindow.axaml:269-352`: Sidebar nav tooltips
- `XBVault/Services/GitHubReleaseCheckerService.cs`: Auto-update service
- `XBVault/Models/CatalogItem.cs:79`: `IsNew` property
- `XBVault/ViewModels/BrowseViewModel.cs:587-614`: `ApplyFilters()` with sort
- `XBVault/Views/BrowseView.axaml:199`: NEW badge overlay
- `XBVault/Views/ErrorDialog.axaml`: Download button added

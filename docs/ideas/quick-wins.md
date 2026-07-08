# Quick Wins

Low-effort ideas that add value. Can be implemented individually as part of polish sprints.

---

| # | Idea | Effort | Value | Files affected |
|---|------|--------|-------|----------------|
| 1 | **Disconnect confirmation** — "Disconnect from Xbox?" dialog before disconnecting | ~30 min | Medium | `MainWindow.axaml.cs` |
| 2 | **Install complete notification** — toast at bottom-right when package finishes installing | ~2h | Medium | `CustomInstallViewModel.cs` + new UserControl |
| 3 | **Favorite apps in catalog** — star icon + "Favorites" filter in Browse | ~4h | High | `CatalogItem.cs` (prop IsFavorite), `BrowseViewModel.cs`, `BrowseView.axaml` |
| 4 | **Cache size visible + Clear Cache** — show cache size in Settings, "Clear Cache" button calling `CacheService.ClearCache()` | ~1h | Medium | `SettingsView.axaml`, `SettingsViewModel.cs` |
| 5 | **Reset settings** — "Reset to defaults" button that deletes `settings.json` and recreates | ~30 min | Medium | `SettingsViewModel.cs` |
| 6 | **Export/Import settings** — manually save/load `settings.json` | ~2h | Medium | `SettingsViewModel.cs`, `SettingsService.cs` |
| 7 | **Compact mode** — allow resizing window to smaller sizes (min ~800x500) | ~1h | Medium | `MainWindow.axaml` (MinWidth/MinHeight) |
| 8 | **Descriptive tooltips** — add tooltips to sidebar icons (Browse, Installed, etc.) | ~30 min | Low | `MainWindow.axaml` (ToolTip.Tip on buttons) |
| 9 | **Changelog link in About** — "View Changelog" button opening `CHANGELOG.md` in browser | ~15 min | Low | `AboutWindow.axaml`, `AboutWindow.axaml.cs` |
| 10 | **Search in Installed list** — text filter like in Browse | ~2h | High | `InstalledViewModel.cs`, `InstalledView.axaml` |
| 11 | **Install order in Custom Install** — reorder dependencies via drag | ~3h | Medium | `CustomInstallViewModel.cs` |
| 12 | **Open Dev Portal in browser** — button opening `https://{ip}:{port}/` with credentials (`GetDevPortalUrl()` already exists) | ~15 min | Low | `ToolsViewModel.cs` (check if already exists) |
| 13 | **File size in Tooltip** — show package sizes in catalog (already has `CatalogItem.Size`) | ~1h | Low | `BrowseView.axaml` |
| 14 | **Connected/disconnected icon in window** — show status in title or window icon | ~1h | Low | `MainWindow.axaml`, `MainViewModel.cs` |
| 15 | **Select all / deselect all** checkbox in install grids | ~1h | Medium | `CustomInstallView.axaml` |

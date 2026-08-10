## 1. AutostartService + settings

- [ ] 1.1 Create `AutostartService` — `SetAutostart(app)` (returns previous), `ClearAutostart()`, `GetAutostart()`, persisted
- [ ] 1.2 Add `AutostartPackageFullName` to `AppSettings` + wire into `SettingsService`

## 2. Installed tab UI

- [ ] 2.1 Add "Autostart on connect" flyout item with icon (Blades/Numix set) + confirmation dialog; label toggles to "Remove autostart" when active
- [ ] 2.2 Add autostart badge overlay (top-left, OUTDATED style, indicative color) bound to selection state
- [ ] 2.3 Exclusivity UX: enabling new app prompts to replace previous

## 3. Launch on connect

- [ ] 3.1 Extract shared `PackageLauncher` helper from `InstalledViewModel.LaunchPackageAsync` (suspend + launch + refresh); reuse for Play
- [ ] 3.2 Hook connect-complete event → launch autostart app via `PackageLauncher`; clear + notify if app missing

## 4. Validation

- [ ] 4.1 `dotnet build` passes
- [ ] 4.2 Manual: set autostart → badge shows → disconnect/reconnect → app launches automatically
- [ ] 4.3 Manual: set autostart on app B while A active → confirm prompt → A cleared, B badge shown
- [ ] 4.4 Manual: uninstall autostart app → reconnect clears selection + notification

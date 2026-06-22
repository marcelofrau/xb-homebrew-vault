## 1. Icons & Assets

- [x] 1.1 Create `Assets/Views/SetupWizardWindow/` directory
- [x] 1.2 Copy 48px originals from icons8 set, resize to 20px, and save as step icons: `setupwizard-step1-20.png` (xbox-series-x-2d), `setupwizard-step2-20.png` (password-key), `setupwizard-step3-20.png` (rocket)
- [x] 1.3 Generate grayscale disabled variants via `magick -colorspace Gray` for each step icon
- [x] 1.4 Copy wizard header icon: `fluentui-magic-wand` → `setupwizard-wizard-48.png` and `setupwizard-wizard-100.png`
- [x] 1.5 Copy close button icon from `Assets/Views/ConnectionWindow/connection-close-20.png` → `setupwizard-close-20.png`

## 2. ViewModel

- [x] 2.1 Create `ViewModels/SetupWizardViewModel.cs` with `ObservableObject`, `[ObservableProperty]` for `CurrentStep` (default 0), `Address`, `Port` (default "11443"), `UseHttps` (default true), `Username`, `Password`, `OpenConnectionAfter` (default true), `StatusText`
- [x] 2.2 Add computed properties: `IsConsoleStep`, `IsAuthStep`, `IsReadyStep`, `CanGoNext` (validates fields per step), `CanGoBack` (step > 0), `CanCancel` (not finishing)
- [x] 2.3 Add `partial void OnCurrentStepChanged` to notify computed property changes
- [x] 2.4 Add partial change handlers for each input field to re-evaluate `CanGoNext`
- [x] 2.5 Implement `[RelayCommand] GoNext()` — validate current step, advance (step 0→1→2)
- [x] 2.6 Implement `[RelayCommand] GoBack()` — decrement step
- [x] 2.7 Implement `[RelayCommand] Finish()` — call `SaveToSettings()`, invoke `CloseAction`
- [x] 2.8 Implement `[RelayCommand] Cancel()` — invoke `CloseAction` without saving
- [x] 2.9 Implement `SaveToSettings()` — populate `SettingsService.Current.XboxConnection`, call `CryptoService.Obfuscate(Password)`, `SettingsService.Save()`, `XboxDeviceService.Configure()`
- [x] 2.10 Add `Action? CloseAction` delegate property

## 3. View (AXAML)

- [x] 3.1 Create `Views/SetupWizardWindow.axaml` following the CustomInstallWindow template (600x500, WindowDecorations=None, CenterOwner, green border, gradient title bar, close button)
- [x] 3.2 Build left sidebar with wizard icon, 3 step indicators (icon + label with active/disabled image pairs and `BoolInverse`/`StepLabelFg` converters)
- [x] 3.3 Build Step 0 content (Console): IP address TextBox, Port TextBox with default "11443", HTTPS CheckBox, helper text
- [x] 3.4 Build Step 1 content (Authentication): Username TextBox, Password TextBox with PasswordChar, helper text
- [x] 3.5 Build Step 2 content (Ready): summary card (Address, Port, Username, HTTPS status), "Open connection window" CheckBox (default checked)
- [x] 3.6 Build footer with Cancel, Back, and Next/Finish buttons bound to corresponding commands; visibility toggled per step

## 4. Code-Behind

- [x] 4.1 Create `Views/SetupWizardWindow.axaml.cs` with `InitializeComponent()`, constructor logging, `OnTitleBarPointerPressed` (drag), `OnCloseClick` (close)

## 5. Integration

- [x] 5.1 In `App.axaml.cs` `InitAfterSplashAsync`, after `main.Show()` and before `splash.Close()`, add: `if (!SettingsService.Current.XboxConnection.IsConfigured)` → create `SetupWizardViewModel`, create window, `ShowDialog`, on Finish with `OpenConnectionAfter==true` call `await mainViewModel.ShowConnectAction!()`
- [x] 5.2 Wire `vm.CloseAction = () => win.Close()` in the App.axaml.cs lambda (same pattern as CustomInstallWindow)

## 6. Verify

- [x] 6.1 Build project with `dotnet build XBVault/XBVault.csproj` — ensure zero errors
- [ ] 6.2 Run app (with empty settings) and confirm wizard appears after MainWindow loads
- [ ] 6.3 Walk through all 3 steps, verify validation (Next disabled when fields empty), Back preserves values, Cancel discards
- [ ] 6.4 Complete wizard with checkbox checked — verify ConnectionWindow opens with saved settings
- [ ] 6.5 Complete wizard with checkbox unchecked — verify app shows "Disconnected" status
- [ ] 6.6 Close wizard via X — verify settings NOT saved, wizard reappears on next launch
- [ ] 6.7 Run app again with saved settings — verify wizard does NOT appear

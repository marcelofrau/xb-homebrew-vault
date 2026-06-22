## Context

The app currently has no onboarding. On first launch, the MainWindow shows "Not configured" in the status bar, and users must discover the Connect sidebar button, then fill in Xbox IP/port/username/password. The `SettingsViewModel` already handles these fields in a full-settings page, and the `ConnectionWindow` handles the connect-test flow — but neither is surfaced to first-time users.

The `CustomInstallWindow` provides an established wizard pattern: 600x500 no-chrome window, green border, gradient title bar, InstallShield-style left sidebar with step indicators, content panel on the right, and a footer with Back/Next/Cancel buttons. This design reuses that pattern exactly.

## Goals / Non-Goals

**Goals:**
- Detect unconfigured state on startup and show the wizard automatically
- Guide users through 3 steps: Console (IP/port/HTTPS), Authentication (username/password), Ready (summary + "Open connection" checkbox)
- Save settings on Finish via existing `SettingsService.Save()` with `CryptoService.Obfuscate()` for password
- Cancel/close discards changes; wizard re-triggers on next launch until completed
- Optional post-wizard: auto-open ConnectionWindow if checkbox checked
- Reuse existing window template, existing services, and existing icons

**Non-Goals:**
- Not modifying SettingsViewModel, ConnectionViewModel, XboxDeviceService, or any existing service
- Not adding a "Test connection" button inside the wizard (handled by ConnectionWindow post-wizard)
- Not adding a "Skip" option — wizard must be completed or cancelled; cancelled state retriggers on next launch

## Decisions

### Decision 1: Detection via `XboxConnection.IsConfigured`

- **Choice:** Check `SettingsService.Current.XboxConnection.IsConfigured` (Address + Username + EncryptedPassword all non-empty)
- **Alternatives considered:**
  - *Dedicated `IsFirstRunComplete` flag in AppSettings* — rejected because the user prefers that clearing settings (e.g., deleting settings.json) naturally re-triggers the wizard without maintaining a separate flag
- **Rationale:** Simple, no new settings fields, naturally handles reset/clear scenarios. If a user has partial settings (e.g., Address but no password), the wizard still shows — they need all three fields.

### Decision 2: Wizard launch timing

- **Choice:** After `main.Show()` in `InitAfterSplashAsync`, inside the existing `Dispatcher.UIThread.InvokeAsync` block, before `splash.Close()`
- **Alternatives considered:**
  - *Before MainWindow* — rejected because user wanted the app visible/alive (catalog loading in background)
  - *Replace splash* — rejected because splash serves a separate purpose (branding, initializing)
- **Rationale:** MainWindow shows first (looks alive, catalog starts loading), then wizard opens as a modal. ConnectionWindow (if checked) opens after wizard closes. Splash closes last for smooth visual flow.

### Decision 3: 3-step structure (not 4)

- **Choice:** Console (step 0) → Authentication (step 1) → Ready (step 2)
- **Alternatives considered:**
  - *4 steps with separate Review step* — rejected as unnecessary; the summary fits naturally on the final step alongside the checkbox
- **Rationale:** Fewer clicks to finish, summary + action on same step reduces cognitive load.

### Decision 4: Icon generation via ImageMagick

- **Choice:** Copy 48px colored originals from Icons8 set, resize to 20px, then generate grayscale "disabled" variants via `magick -colorspace Gray`
- **Alternatives considered:**
  - *Separate disabled icon files from Icons8 set* — rejected due to limited selection in the set
  - *CSS/Opacity-only disabled state* — rejected because the existing wizard pattern uses distinct files for active vs disabled step icons
- **Rationale:** Consistent with `CustomInstallWindow` pattern. ImageMagick is available via scoop and handles the conversion in a one-liner per file.

### Decision 5: Close icon from existing assets

- **Choice:** Copy `connection-close-20.png` from `Assets/Views/ConnectionWindow/` to `Assets/Views/SetupWizardWindow/setupwizard-close-20.png`
- **Alternatives considered:**
  - *Any existing close icon* — they're all identical in the project
- **Rationale:** Avoids hunting through the icons8 set for a close icon that may not match the existing style.

### Decision 6: No settings saved on Cancel

- **Choice:** Cancel/close button does NOT call `SettingsService.Save()`. Settings only persist when Finish is clicked.
- **Rationale:** Partial configuration would leave the app in a "configured but broken" state. The wizard re-triggers on next launch if `IsConfigured` is false, so no data is lost.

### Decision 7: Reuse step indicator pattern from CustomInstallWindow

- **Choice:** Same left sidebar layout: wizard icon at top, 3 step indicators (icon + label, active/disabled pair), "XB Homebrew Vault" branding at bottom
- **Rationale:** Consistent UX, same AXAML structure, same converters (`BoolInverse`, `StepLabelFg`), same `ObservableProperty` pattern for `CurrentStep` and computed bools

## Data Flow

```
[App start]
    │
    ├── SettingsService.Current.XboxConnection.IsConfigured?
    │   └── true → normal flow (no wizard)
    │
    └── false → SetupWizardWindow.ShowDialog(main)
         │
         ├── X (close) or Cancel → no save, wizard closes
         │   └── next launch → wizard again (IsConfigured still false)
         │
         └── Finish → SaveSettings()
              │
              ├── checkbox "Open connection" = true → mainViewModel.ShowConnectAction()
              │   └── ConnectionWindow → TestConnectionAsync → user connected or sees error
              │
              └── checkbox = false → just close, next launch sees "Disconnected" state
```

## ViewModel State

```
SetupWizardViewModel
├── [ObservableProperty] CurrentStep (0-2)
├── [ObservableProperty] Address
├── [ObservableProperty] Port (= "11443")
├── [ObservableProperty] UseHttps (= true)
├── [ObservableProperty] Username
├── [ObservableProperty] Password
├── [ObservableProperty] OpenConnectionAfter (= true)
├── [ObservableProperty] StatusText (validation messages)
│
├── Computed: CanGoNext, CanGoBack, CanCancel
├── Computed: IsConsoleStep, IsAuthStep, IsReadyStep
│
├── Action? CloseAction (set by App.axaml.cs)
│
├── [RelayCommand] GoNext → validate current step, advance
├── [RelayCommand] GoBack → decrement step
├── [RelayCommand] Finish → save, invoke CloseAction
├── [RelayCommand] Cancel → invoke CloseAction (no save)
│
├── SaveToSettings() → CryptoService.Obfuscate, SettingsService.Save(), _xboxService.Configure()
```

## AXAML Structure

Same as CustomInstallWindow but with custom step labels and content:

```
Window 600x500, WindowDecorations=None, CenterOwner
└── Border #447F3E 2px
    └── Grid (2 rows: 32px title | *, 2 cols: 160px sidebar | *)
        ├── TitleBar (span 2 cols)
        │   ├── LinearGradientBrush #447F3E → #9ACA3C
        │   ├── TextBlock "Setup Wizard"
        │   └── Close Button (32x32, red hover, icon)
        │
        ├── Sidebar (col 0, row 1)
        │   ├── ImageBrush sidebar bg (or solid SurfaceBrush — simpler)
        │   ├── Wizard icon (48px)
        │   └── 3 step indicators (icon + label, active/disabled pair)
        │       ├── Step 0: Xbox icon, "Console"
        │       ├── Step 1: Key icon, "Authentication"
        │       └── Step 2: Rocket icon, "Ready"
        │
        ├── Content (col 1, row 1)
        │   ├── Step 0 (Console): Address TextBox, Port TextBox, HTTPS CheckBox
        │   ├── Step 1 (Auth): Username TextBox, Password TextBox
        │   ├── Step 2 (Ready): summary card + "Open connection" CheckBox
        │   └── Footer: [Cancel] [Back] [Next/Finish] buttons
        │
        └── (green border wraps all)
```

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Wizard blocks until closed; user may be annoyed | It's 3 short steps with minimal required fields. Checkbox default matches common desire (connect now). If they close via X, no harm — wizard reappears next time but they can go straight to Settings manually. |
| `XboxConnection.IsConfigured` only checks string emptiness — a user could enter garbage and never see wizard again | Same as current Settings page behavior. No new risk here. |
| Icon copy/generation script is manual (one-time) | Documented in tasks. If an icon needs updating, dev runs magick again. Not part of CI/build. |
| Overlapping modals (wizard + ConnectionWindow) | They are sequential: wizard closes, then ConnectionWindow opens. No overlap. |

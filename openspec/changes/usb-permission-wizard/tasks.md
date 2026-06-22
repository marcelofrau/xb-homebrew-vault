## 1. Icons & Assets

- [ ] 1.1 Create `Assets/Views/UsbPermissionWindow/` directory
- [ ] 1.2 Copy icons from `F:\workspace\icons8-personal-set`:
  - `icons8-usb-{size}.png` → `usbperm-usb-20.png` (step 0 active, 20px)
  - Grayscale variant → `usbperm-usb-disabled-20.png` (step 0 disabled, 20px via `magick -colorspace Gray`)
  - `icons8-gear-{size}.png` → `usbperm-gear-20.png` (step 1 active, 20px)
  - Grayscale variant → `usbperm-gear-disabled-20.png` (step 1 disabled, 20px)
  - `icons8-checkmark-{size}.png` → `usbperm-flag-20.png` (step 2 active, 20px)
  - Grayscale variant → `usbperm-flag-disabled-20.png` (step 2 disabled, 20px)
  - `icons8-usb-48.png` (or similar) → `usbperm-wizard-48.png` (sidebar wizard icon, 48px)
  - `icons8-hdd-{size}.png` → `usbperm-drive-48.png` (drive icon in step 0, 48px)
  - `icons8-checkmark-{size}.png` → `usbperm-success-100.png` (success, 100px)
  - `icons8-error-{size}.png` → `usbperm-failure-100.png` (failure, 100px)
- [ ] 1.3 Copy close button from `Assets/Views/ConnectionWindow/connection-close-20.png` → `usbperm-close-20.png`
- [ ] 1.4 If sidebar background image desired, create or reuse gradient placeholder

## 2. Model

- [ ] 2.1 Create record/class `UsbDriveInfo` with: `DriveLetter`, `VolumeLabel`, `SizeBytes`, `FormattedSize`, `FileSystem`, `DriveTypeLabel` (HDD/USB Stick), `DisplayName` (for ComboBox), `IsSystemDrive`
- [ ] 2.2 Add implicit ordering: non-system USB drives sorted by drive letter

## 3. WMI / USB Detection

- [ ] 3.1 Add `using System.Management;` — verify it's available in .NET 8 (it's a NuGet reference in some targets, may need `<PackageReference Include="System.Management" />`)
- [ ] 3.2 Implement static method `ListUsbDrives()` returning `List<UsbDriveInfo>`:
  - Query: `SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'`
  - For each disk, get partitions via `ASSOCIATORS OF {Win32_DiskDrive.DeviceID='...'} WHERE AssocClass = Win32_DiskDriveToDiskPartition`
  - For each partition, get logical disks via `ASSOCIATORS OF {Win32_DiskPartition.DeviceID='...'} WHERE AssocClass = Win32_LogicalDiskToPartition`
  - Extract `DeviceID` (drive letter), `VolumeName`, `Size`, `FileSystem`
  - Determine HDD vs USB Stick: check `MediaType` contains "Fixed" or size > 64GB → HDD
  - Filter out system drive: compare `DeviceID` root with `Path.GetPathRoot(Environment.SystemDirectory)`
  - Wrap each operation in try/catch, skip drives that fail WMI queries
- [ ] 3.3 Add `using System.Diagnostics;` — for `Process.Start("icacls", ...)` permission application

## 4. ViewModel

- [ ] 4.1 Create `ViewModels/UsbPermissionViewModel.cs`:
  - Inject no services (self-contained)
  - `[ObservableProperty]` fields: `CurrentStep` (0-2), `UsbDrives` (list), `SelectedDriveIndex`, `SelectedDrive`,
    `DriveLetter`, `DriveLabel`, `DriveSize`, `DriveTypeLabel`, `DriveFileSystem`, `IsDriveValid`,
    `ValidationMessage`, `IsApplying`, `ApplyProgressText`, `ApplySuccess`, `ApplyComplete`,
    `ResultMessage`, `ResultDetails`
  - Computed: `CanGoNext` (step 0: `IsDriveValid`), `CanGoBack` (step > 0 && !IsApplying), `CanCancel` (!IsApplying && !ApplyComplete)
  - Computed: `IsSelectStep`, `IsApplyStep`, `IsDoneStep`, `IsSuccess`, `IsFailure`
  - `Action? CloseAction`

- [ ] 4.2 Implement `[RelayCommand] LoadDrives()`:
  - Run WMI query on background thread
  - Populate `UsbDrives` list, auto-select first if available
  - Handle no-drives case

- [ ] 4.3 Implement `partial void OnSelectedDriveIndexChanged(int value)`:
  - Update all drive info fields from selected `UsbDriveInfo`
  - Validate NTFS → set `IsDriveValid`, `ValidationMessage`
  - Notify `CanGoNext`

- [ ] 4.4 Implement `[RelayCommand] GoNext()` / `[RelayCommand] GoBack()`:
  - Step navigation with can-go guards
  - `OnCurrentStepChanged` partial → notify computed step bools + `CanGoNext`/`CanGoBack`/`CanCancel`

- [ ] 4.5 Implement `[RelayCommand] async ApplyAsync()`:
  - Set `IsApplying = true`, `CurrentStep = 1`
  - Build arguments: `<drive>:\ /grant "ALL APPLICATION PACKAGES:(OI)(CI)(F)" /T /Q`
  - Run `Process.Start("icacls", args)` with `RedirectStandardOutput`, `RedirectStandardError`, `UseShellExecute = false`
  - Wait for exit, collect stdout/stderr
  - On exit code 0 → `ApplySuccess = true`,`ResultMessage = "Drive ready for Xbox!"`
  - On non-zero → `ApplySuccess = false`, `ResultMessage = "Failed"`, `ResultDetails = stderr`
  - Handle exceptions (drive removed, access denied, etc.)
  - Set `ApplyComplete = true`, `IsApplying = false`, advance to step 2

- [ ] 4.6 Implement `[RelayCommand] Retry()`:
  - Reset all apply/result state
  - `CurrentStep = 0`

- [ ] 4.7 Implement `[RelayCommand] Close()` / `[RelayCommand] Cancel()`:
  - Invoke `CloseAction`

## 5. Views

- [ ] 5.1 Create `Views/UsbPermissionWindow.axaml`:
  - Same template as `CustomInstallWindow.axaml` (green border, title bar, sidebar, content, footer)
  - Step 0 content:
    - Heading: "Select USB Drive"
    - ComboBox bound to `UsbDrives` (DisplayMemberPath = `DisplayName`), `SelectedIndex`
    - Drive info card (Border with Grid): letter, label, size, filesystem, type
    - Validation message (red text if not NTFS)
    - Refresh button (re-runs `LoadDrives`)
  - Step 1 content:
    - Heading: "Apply Permissions"
    - Drive summary card showing selection
    - Warning text: "This will grant ALL APPLICATION PACKAGES full access to this drive"
    - Progress: indeterminate bar + status text "Applying permissions..."
  - Step 2 content:
    - Success block: green checkmark icon, "Drive ready for Xbox!", instruction list
    - Failure block: red X icon, "Failed", scrollable error details, Retry button
  - Footer buttons: Back, Cancel, Next (disabled on step 1), Apply (step 1), Retry (step 2 failure), Close (step 2)

- [ ] 5.2 Create `Views/UsbPermissionWindow.axaml.cs`:
  - Constructor: `InitializeComponent()` in try/catch with `Logger`
  - `OnCloseClick`, `OnTitleBarPointerPressed`
  - (No spinner needed — progress is indeterminate bar, not rotating image)

## 6. Tools tab integration

- [ ] 6.1 Add to `ViewModels/ToolsViewModel.cs`:
  - `public Action? ShowUsbPermissionAction { get; set; }`
  - `[RelayCommand] void OpenUsbPermission()` — check `IsConnected` (optional, wizard works offline), call `ShowUsbPermissionAction`

- [ ] 6.2 Add to `Views/ToolsView.axaml`:
  - New button in MANAGEMENT section, after "Custom Install":
    ```
    <Button Command="{Binding OpenUsbPermissionCommand}" Padding="14,8">
      <Grid ColumnDefinitions="Auto,*" ColumnSpacing="6">
        <Image Source="avares://XBVault/Assets/Views/ToolsView/tools-usb-20.png"
               Width="20" Height="20" Stretch="Uniform"/>
        <TextBlock Grid.Column="1" Text="Activate USB Media Drive" VerticalAlignment="Center"/>
      </Grid>
    </Button>
    ```
  - Copy USB icon from icons8 set → `tools-usb-20.png`

- [ ] 6.3 Wire in `App.axaml.cs`:
  - Set `toolsViewModel.ShowUsbPermissionAction = () => { ... new UsbPermissionWindow { DataContext = new UsbPermissionViewModel() }.ShowDialog(main); }`
  - Or resolve via DI if ViewModel has dependencies

## 7. Admin rights detection

- [ ] 7.1 Add helper `static bool IsAdministrator()`:
  - `using System.Security.Principal;`
  - `new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)`
- [ ] 7.2 If not admin, show warning in step 1 before applying: "XBVault is not running as Administrator. icacls requires admin rights. Click Apply anyway, or restart as Administrator."

## 8. Verify

- [ ] 8.1 Verify `System.Management` is available. If not, add `<PackageReference Include="System.Management" />` to csproj
- [ ] 8.2 Build project with `dotnet build XBVault/XBVault.csproj` — zero errors
- [ ] 8.3 Run app, open Tools tab — verify "Activate USB Media Drive" button visible
- [ ] 8.4 Click button — verify wizard opens with correct template
- [ ] 8.5 Verify USB drives appear in dropdown (connect a USB drive if available)
- [ ] 8.6 Select a drive — verify info card shows details, NTFS validation works
- [ ] 8.7 Select non-NTFS drive — verify Next disabled + warning shown
- [ ] 8.8 Click Apply on NTFS drive — verify progress shown, icacls runs
- [ ] 8.9 Verify success screen with instructions appears
- [ ] 8.10 Verify failure screen shows error details (test by removing drive during apply)
- [ ] 8.11 Verify Close, Cancel, Back, Retry buttons work
- [ ] 8.12 Verify admin rights detection shows warning
- [ ] 8.13 Verify no USB drives shows appropriate message

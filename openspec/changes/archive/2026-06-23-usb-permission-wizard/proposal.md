## Why

Xbox Dev Mode can read and install packages from external USB drives formatted as NTFS, but the drive needs specific NTFS permissions: the `ALL APPLICATION PACKAGES` security principal (SID `S-1-15-2-1`) must have access. Manually setting these permissions via `icacls` or drive Properties → Security is error-prone and unfamiliar to most users.

The existing third-party tool (Xbox-UWP-Permission-Tool) is a standalone WinForms app — separate download, no integration with XBVault's workflow. Integrating a permission wizard directly into the Tools tab keeps the user inside XBVault.

## What Changes

- Add `UsbPermissionWindow` — a 3-step wizard (Select Drive → Apply → Done) accessible from the Tools tab
- Step 1 (Select): Enumerate USB drives via WMI (`Win32_DiskDrive WHERE InterfaceType='USB'`), show letter, label, size, type (HDD vs USB Stick), and filesystem. Validate NTFS. Disable Next if not NTFS.
- Step 2 (Apply): Recursively grant `ALL APPLICATION PACKAGES` `FullControl` with `ContainerInherit | ObjectInherit` via `icacls` (preferred for reliability on drive roots) or `DirectorySecurity`.
- Step 3 (Done): Success message with instructions ("plug into Xbox, set up as Media drive, not Games & Apps"). Error state with details on failure.
- Button "Activate USB Media Drive" in Tools tab, section MANAGEMENT, following the existing `ShowCustomInstallAction` delegate pattern.
- Use same window template as `CustomInstallWindow` (600x500, no chrome, green border, gradient title bar, left sidebar with step indicators).
- Add `Assets/Views/UsbPermissionWindow/` with icons from the personal Icons8 set.

## Capabilities

### New Capabilities

- `usb-permission-wizard`: Step-by-step wizard to grant ALL APPLICATION PACKAGES NTFS permissions on a USB drive, making it readable by Xbox Dev Mode for package installation and media access.

### Modified Capabilities

None — this is a net-new feature with no changes to existing specs.

## Impact

- **New files:** `ViewModels/UsbPermissionViewModel.cs`, `Views/UsbPermissionWindow.axaml`, `Views/UsbPermissionWindow.axaml.cs`, icon files under `Assets/Views/UsbPermissionWindow/`
- **Modified files:** `ViewModels/ToolsViewModel.cs` — add `ShowUsbPermissionAction` delegate and `OpenUsbPermission` command. `Views/ToolsView.axaml` — add button in MANAGEMENT section. `App.axaml.cs` — wire delegate.
- **Zero new NuGet dependencies** — WMI (`System.Management`) is included in .NET 8.
- **No existing services modified** — permissions logic is self-contained in the ViewModel with helper calls to `icacls` or `DirectorySecurity`.

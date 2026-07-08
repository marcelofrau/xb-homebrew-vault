# QR Code for Connection

**Impact:** Low | **Effort:** Low | **Suggested priority:** Phase 3

## Problem

Setting up connection requires manually typing IP + credentials. In setups where the PC is far from the Xbox, the user needs to write down the IP or use the Xbox mobile app to discover it.

## Proposal

### QR Code Generator
- Generate QR code with connection string: `xbvault://{ip}:{port}?user={user}&pass={pass}`
- Display on connection screen (ConnectionWindow) or Settings
- User scans with phone → opens Xbox Device Portal in mobile browser

### QR Code Scanner (future, reverse)
- Mobile app scans QR on TV (Xbox dashboard) → extracts IP
- Auto-fills IP in connection field

### Technical implementation
- Lightweight library: `QRCoder` (NuGet, ~50KB, no native dependencies)
- Generates PNG in memory, displays in `Image.Source`
- No data sent to external server — 100% local

### UX
- "Show QR Code" button on ConnectionWindow (next to Test Connection)
- Popup with QR code + instruction "Scan with your phone to open Xbox Device Portal"
- Optionally: "Copy connection string" (plain text)

### Security
- QR code contains password — should only be shown in secure environment
- Can optionally be masked (show IP only)
- Never saved to disk

### Dependencies
- `QRCoder` NuGet package

### Files to create
- `Helpers/QrCodeGenerator.cs`
- `Views/QrCodePopup.axaml` + `.axaml.cs` (or include in ConnectionWindow)

### Files to modify
- `ConnectionWindow.axaml` — "Show QR" button
- `ConnectionViewModel.cs` — `GenerateQrCodeCommand`

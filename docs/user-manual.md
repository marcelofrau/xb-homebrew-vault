---
layout: default
title: User Manual
---

# User Manual

Complete guide to using XB Homebrew Vault — from first launch to advanced features.

---

## Quick Start

### System Requirements

| Platform | Minimum | Recommended |
|----------|---------|-------------|
| **Windows** | Windows 10/11 64-bit |
| **macOS** | macOS 12 (Monterey), Apple Silicon or Intel | macOS 14 (Sonoma) |
| **Linux** | glibc-based distro (Ubuntu 22.04+, Fedora 38+) | Ubuntu 24.04+ |
| **Android** | Android 6.0+ (API 23), ARM64 | Recent Android, ARM64 |
| **Xbox** | Xbox One or Series S\|X in Developer Mode | Latest Dev Mode update |

> The Android app is documented separately in the [Mobile Guide](mobile).

### Download & Installation

1. Go to the [latest release](https://github.com/marcelofrau/xb-homebrew-vault/releases/latest) page
2. Download the ZIP for your platform:
   - `win-x64.zip` — Windows 64-bit
   - `win-arm64.zip` — Windows ARM (Surface Pro X, etc.)
   - `osx-x64.zip` — macOS Intel
   - `osx-arm64.zip` — macOS Apple Silicon
   - `linux-x64.zip` — Linux 64-bit
   - `linux-arm64.zip` — Linux ARM (Raspberry Pi, ARM servers)
   - `android-arm64.apk` — Android ARM64 (see [Mobile Guide](mobile))
3. Extract the ZIP to any folder
4. Run `XBVault.exe` (Windows) or `XBVault` (macOS/Linux)

**Optional:** Use the helper scripts included in the ZIP:
- `xbv-run.cmd` / `xbv-run.sh` — launch the app normally
- `xbv-console.cmd` / `xbv-console.sh` — launch with a visible console window (useful for debugging)

### First Launch

On first launch, a setup wizard walks you through three steps:

1. **Connection** — Enter your Xbox IP address, port (default `11443`), username (`DevToolsUser`), and password (set during Dev Mode setup)
2. **Test Connection** — The app verifies it can reach your Xbox
3. **Done** — You're ready to browse and install

### Enabling Xbox Dev Mode

> Full guide: [USB Device Discovery & Permission Setup](integration-usb-device-discovery)

1. On your Xbox, open **Dev Mode** from the home screen
2. Go to **Home** in Dev Mode, note the IP address shown
3. Go to **Remote Access**, enable it
4. Set a username and password (these are your Dev Mode credentials)
5. Back on your PC, enter these details in the connection wizard

---

## Browsing the Catalog

### Catalog Overview

The **Browse** tab (default view) displays the Emulation Revival catalog — a curated collection of homebrew apps, emulators, games, and tools for Xbox Dev Mode.

- Items are shown as cards with title, category, and thumbnail
- Each card shows the **compatibility badge**: your Xbox architecture is checked against the package requirements
- Cards link to the full detail view

### Search & Filtering

- **Search bar** — type to filter by name or description (minimum 3 characters)
- **Category filter** — narrow by type: Emulator, Application, Game, Tool, etc.
- **Compatibility filter** — show only packages compatible with your Xbox architecture
- Results update in real-time as you type or change filters

### Categories

The catalog organizes packages into these categories:

| Category | Examples |
|----------|----------|
| Emulator | RetroArch, DuckStation, XBSnes |
| Application | Jellyfin, Kodi, Spotify |
| Game | Space Cadet Pinball, Quake |
| Tool | SMWRP, SMBR |
| Library | Mono, .NET Runtime |

### Item Details

Click any card to open the **Item Detail** window:

- **Header** — title, version, author, and category badge
- **Thumbnail** — package artwork or banner image
- **Description** — detailed description from the catalog
- **Compatibility** — shows supported Xbox architectures
- **Downloads** — lists available download options
- **Developer & Contributors** — clickable names with links to GitHub, Ko-fi, Patreon, and more

### Author & Contributor Links

Each item may show:
- **Developer** — the person who built the package
- **Porter** — the person who ported it to Xbox
- **Maintainer** — the person maintaining the package

Click any name to see their donation and profile links (GitHub, Ko-fi, Patreon, PayPal, Buy Me a Coffee / Stripe).

### Community Discord

Click the Discord icon in the sidebar to open the community dialog with curated Xbox homebrew servers.

---

## Installing Packages

### One-Click Install

From the Browse view:
1. Click a package card to open the detail window
2. Click the **Install** button
3. The app downloads, analyzes, and installs the package automatically
4. Progress is shown in real-time

### Multi-Option Install

Some packages offer multiple download variants (e.g., RetroArch with different cores or configurations). When available:
1. Click **Install** — a menu lists each variant
2. Select your preferred variant
3. Installation proceeds with your choice

### Custom Install Wizard

For advanced installs (local files, drag-drop, or specific configurations):

1. Click **Custom Install** from the sidebar (folder icon)
2. **Step 1 — Source**: Choose the source package file (`.appxbundle`, `.msixbundle`, `.appx`, `.msix`)
3. **Step 2 — Analyze**: The app examines the package structure and lists all dependencies
4. **Step 3 — Confirm**: Review dependencies and install targets; optionally skip specific dependencies
5. **Step 4 — Install**: The wizard uploads and installs the package and all selected dependencies

**Cancel**: You can cancel an ongoing install at any time using the Cancel button.

### Understanding Dependencies

Xbox packages often need other packages (frameworks, runtimes). The app detects these automatically:

- **Direct dependencies** — required by the package itself
- **Transitive dependencies** — required by other dependencies
- **Already installed** — skipped automatically
- **Needs install** — uploaded and installed in dependency-first order

If a dependency is already on your Xbox, it's skipped. Only missing dependencies are transferred.

---

## Managing Installed Apps

### Installed Packages View

The **Installed** tab shows all packages currently on your Xbox:
- Cards display the installed version, publisher, and architecture
- Running apps are highlighted with a green overlay
- Each card has action buttons: Launch, Suspend, Terminate

### Launch, Suspend, Terminate

- **Launch** — starts a package (only available when not running)
- **Suspend** — pauses a running app (saves state)
- **Terminate** — force-stops a running app

### Uninstalling Packages

1. Select a package in the Installed view
2. Click **Uninstall**
3. Confirm the action — the package is removed from your Xbox

### Refreshing Package List

Click the **Refresh** button to re-scan installed packages on your Xbox. This also checks for any packages that were installed outside of XB Homebrew Vault.

---

## Dev Tools

### File Explorer

Browse and manage files on your Xbox's file system:
- Navigate folders on the Xbox
- Upload files by drag-and-drop from Windows Explorer
- Download files to your PC
- Delete files and folders
- Uses SFTP over your existing Dev Mode connection

### User Files (Portal Browser)

Browse the Xbox `UserFiles` folder over the Device Portal (REST):
- List, rename, delete, and create folders
- Uses the same credentials as your main connection
- Lives alongside the SFTP File Explorer for quick access to user content

### Process Manager

View and control running processes on your Xbox:
- List all active processes
- View process IDs and names
- Kill unresponsive processes
- Refresh process list in real-time

### Performance Monitor

Real-time performance metrics from your Xbox:
- **CPU** — overall utilization
- **Memory** — used and available
- **GPU** — utilization and clock speed
- **Temperature** — CPU and GPU temperatures
- **Network** — current throughput
- Data updates via WebSocket for low-latency monitoring

### Screenshot Capture

Capture what's currently displayed on your Xbox:
- One-click screenshot capture
- Saves as PNG on your PC
- Auto-saves with timestamp in the screenshots folder
- Visual confirmation when a screenshot is saved

### Crash Dumps

View and manage crash dumps collected from your Xbox:
- List all available crash dumps with timestamps
- View dump details
- Delete individual or all dumps
- Refresh dump list

### System Information

View your Xbox's hardware and software details:
- Console type (Xbox One, Series S, Series X)
- OS version and build
- CPU model and specs
- Memory (total and available)
- Network configuration (IP, MAC, WiFi, link speed)

### USB Permission Wizard

When using an external USB drive with your Xbox:
1. Connect the USB drive to your PC
2. Open the **USB Permission** tool from the sidebar
3. Select your USB drive from the list
4. Click **Grant Permissions** — this sets up NTFS permissions for `ALL APPLICATION PACKAGES`
5. Move the drive to your Xbox — it will be recognized and writable

### Loopback Exempt & X-Files

Two tools in the **Tools** tab help development-mode apps reach the network:
- **X-Files Enablement** — one-click wizard: detects the `X-Files` homebrew explorer and applies the loopback exemption so it can reach the Xbox portal REST API on itself (browse `LocalAppData` / `DevelopmentFiles`)
- **Loopback Exempt** — full manager for loopback exemptions on the Xbox, listing and toggling development-mode packages

### Open Dev Portal

Opens your Xbox's Device Portal web interface in the default browser, pre-filled with your connection address.

> **Note:** USB detection is Windows-only. On macOS/Linux, a manual file path is required.

---

## Settings

The Settings screen is divided into three areas. Changes are applied only when you press **Save** — you can edit freely and decide later.

### Toolbar (top-right)

- **Save** — persists all changes on the screen (connection, preferences, and log level)
- **Discard Changes** — reverts the form to the last saved state, discarding any edits
- **Reset to Default** — resets the form to factory defaults (saved only after you press Save)

A small **● Unsaved changes** badge appears whenever the form differs from what is saved — a reminder to hit Save or Discard.

### Connection Configuration

- **Address** — IP or hostname of your Xbox in Dev Mode
- **Port** — Dev Mode API port (default `11443`)
- **Username** — your Dev Mode credentials (default `DevToolsUser`)
- **Password** — stored obfuscated (not plaintext)
- **Use HTTPS** — connects over HTTPS when your Dev Mode serves it
- **Test Connection** — verifies the connection without saving

### Application Settings

- **Log level** — controls verbosity (Info, Warn, Error, Debug)
- **UI Scale** — 80–120% zoom for the whole interface (applies immediately)
- **Connection check interval** — how often the app re-checks the Xbox connection
- **Reset window size** — returns the main window to its default dimensions

### Maintenance

- **Logs folder** — opens the folder where logs are stored
- **Logs screen** — jumps to the Logs tab
- **Clear cache** — empties the package catalog cache (re-fetched on next load)
- **Restart application** — closes and relaunches the app
- **Open settings folder** — opens the folder containing `settings.json`
- **Reset all settings** — wipes all saved settings and restores defaults immediately

### Logging

- Logs are written to `%APPDATA%/XBVault/logs/` (Windows) or `~/.local/share/XBVault/logs/` (Linux/macOS)
- Log level controls what gets recorded — `Debug` for troubleshooting, `Info` for normal use
- Older logs are automatically cleaned up

---

## Advanced

### Drag & Drop Installation

From Windows Explorer:
1. Open the **Custom Install Wizard** or **File Explorer**
2. Drag a `.appxbundle` or `.msixbundle` file into the window
3. A confirmation dialog appears
4. The wizard pre-fills with the dropped file — just click Analyze to begin

### SFTP Configuration

The File Explorer uses SFTP over your Dev Mode SSH connection:
- Host and port are inherited from your connection settings
- Username and password are the same Dev Mode credentials
- Root path is the Xbox Dev Mode sandbox

### Command-Line Interface

Launch `XBVault.exe` with these options:

| Flag | Description |
|------|-------------|
| `--help`, `-h`, `-?` | Show help message and exit |
| `--version`, `-v` | Show version and exit |
| `--console`, `-c` | Open a console window for log output (Windows only) |
| `--reset-data`, `-r` | Reset all app data (settings, cache, logs) — requires confirmation |
| `--check` | Run health diagnostics and print report, then exit |

**Examples:**

```bash
# Normal startup
XBVault.exe

# Start with visible console (debugging)
XBVault.exe --console

# Reset all data
XBVault.exe --reset-data

# Run health check
XBVault.exe --check
```

---

## Troubleshooting

### Connection Problems

| Problem | Solution |
|---------|----------|
| "Connection refused" | Verify the Xbox is in Dev Mode and Remote Access is enabled. Check the IP address. |
| "Authentication failed" | Verify your Dev Mode username and password. Reset them in Dev Mode if needed. |
| "Connection timed out" | Ensure your PC and Xbox are on the same network. Check firewall settings. |
| "No Xbox found" | Use the Test Connection button in Settings to verify the connection. |

### Install Failures

| Problem | Solution |
|---------|----------|
| "Package manager busy" | Wait a moment and try again. An install may already be in progress. |
| "Dependency missing" | The custom install wizard should resolve dependencies automatically. Try using Custom Install. |
| "Disk space" | Free up space on your Xbox's internal storage. |
| "Corrupted download" | Delete the cache and try again. Use `--reset-data` if needed. |

### App Crashes

If the application crashes:
1. Restart `XBVault.exe`
2. Check the log files at `%APPDATA%/XBVault/logs/` for error details
3. Run `XBVault.exe --check` for a health report
4. Run `XBVault.exe --reset-data` to reset all data if corruption is suspected

### Health Check

The `--check` flag runs diagnostics and reports:
- **Settings** — are settings valid or corrupted
- **Cache** — is the catalog cache intact and at the expected schema version
- **Log directory** — is the log folder writable
- **System** — platform info, architecture, .NET version

Results are printed to the console and logged.

### Common Errors

- **"Fatal error on startup"** — The pre-flight checker caught a problem. A native dialog explains what went wrong. If it persists, try `--reset-data`.
- **"Settings file corrupted"** — Automatically reset to defaults. No action needed.
- **"Cache schema mismatch"** — Automatically cleared. The catalog will be re-fetched.

---

## Privacy & Data

### Where Data Is Stored

| Data | Location |
|------|----------|
| **Settings** (connection, preferences) | `%APPDATA%/XBVault/settings.json` |
| **Logs** | `%APPDATA%/XBVault/logs/` |
| **Package cache** (downloaded files) | `%LOCALAPPDATA%/XBVault/cache/` |
| **Catalog cache** | `%LOCALAPPDATA%/XBVault/cache/catalog-api.json` |

On Linux/macOS:
- Settings & Logs: `~/.local/share/XBVault/`
- Cache: `~/.cache/XBVault/`

### What We Collect

**Nothing.** XB Homebrew Vault does not collect telemetry, analytics, or usage data. It has no "phone home" feature.

- All connection data stays on your machine
- Outbound requests: fetching the catalog from Emulation Revival, checking GitHub for app updates / version overrides, and any share/upload you explicitly trigger (QR share, GoFile upload, indirect download links)
- No account, no login, no tracking

### Resetting Your Data

Two ways to reset all application data:

1. **Via CLI:** `XBVault.exe --reset-data` — shows a confirmation dialog, then deletes settings, cache, and logs
2. **Manual:** Delete the folders listed above

After reset, the app starts fresh as if it were the first launch.

---

## Quick Fixes

For common problems — app won't open, can't connect, install fails — see the [Troubleshooting page](troubleshooting). It covers solutions you can try before reporting a bug.

## Getting Help

- **GitHub Issues** — [Report a bug or request a feature](https://github.com/marcelofrau/xb-homebrew-vault/issues)
- **Discord** — Join the Xbox homebrew community via the Discord icon in the app
- **Logs** — Include relevant log files when reporting issues (found in `%APPDATA%/XBVault/logs/`)

### Reporting Bugs

The best bug reports include:
1. App version (see `--version`)
2. Your platform (Windows/macOS/Linux version)
3. Xbox model and OS version
4. Steps to reproduce
5. Log files (from `%APPDATA%/XBVault/logs/`)
6. Screenshots, if applicable

# Troubleshooting

Quick fixes for common problems with XB Homebrew Vault.

---

## App Won't Open

### Nothing happens when I double-click XBVault.exe

1. Open a terminal in the app folder
2. Run `XBVault.exe --check` to see the health report
3. Run `XBVault.exe --reset-data` to wipe corrupted data
4. Try launching again

Still won't open? Download a fresh copy from the [latest release](https://github.com/marcelofrau/xb-homebrew-vault/releases/latest).

### "The application failed to start" dialog

This is a fatal boot error. Common causes:

| Cause | Fix |
|-------|-----|
| **File access denied** | Extract the ZIP to a writable folder (not Program Files) |
| **Missing DLL** | Re-extract the ZIP — make sure all files are present |
| **Corrupted settings** | Run with `--reset-data` to wipe and restart |

### App opens but window is blank / hangs

1. Wait 10–15 seconds — the catalog may be loading for the first time
2. Check if your firewall is blocking the app
3. Run with `--console` to see log output in real-time
4. Check log files for error messages

### macOS: "Unable to load shared library 'libAvaloniaNative'" or "code signature not valid"

This is a **macOS Gatekeeper** issue. When you download the ZIP, macOS marks all files with a quarantine attribute. The Avalonia native library (`libAvaloniaNative.dylib`) is then blocked from loading.

**Fix — run this in Terminal:**

```bash
xattr -cr /path/to/xbvault
```

Replace `/path/to/xbvault` with the actual folder path. For example, if you extracted to Desktop:

```bash
xattr -cr ~/Desktop/XBVault
```

**Or use the included helper script:**

```bash
cd /path/to/xbvault
./xbv-fix-macos.sh
```

You only need to do this **once** after extracting. If it still doesn't work:

```bash
sudo spctl --master-disable    # temporarily lower security
# launch XBVault
sudo spctl --master-enable     # re-enable after
```

> **Note:** The `--check` command works fine because it doesn't load the UI native libraries — it only runs diagnostics.

---

## Can't Connect to Xbox

### "Connection refused"

Your Xbox is not accepting connections.

- Is the Xbox **powered on** and in **Dev Mode**? (not retail mode)
- Is **Remote Access** enabled? (Dev Mode Home → Remote Access → Enable)
- Is the IP address correct? (shown on Dev Mode Home screen)
- Try pinging the Xbox: `ping <xbox-ip>` — if it fails, the network is the issue

### "Authentication failed"

Wrong username or password.

- Default username is `DevToolsUser`
- Password is whatever you set in Dev Mode
- **Reset password** in Dev Mode: Settings → Remote Access → Reset Credentials
- Update the credentials in XB Vault Settings and test again

### "Connection timed out"

Xbox is reachable but not responding to the Dev Mode API.

| Cause | Fix |
|-------|-----|
| **Firewall blocking port 11443** | Try a different network or check router settings |
| **Different networks** | PC and Xbox must be on the same local network |
| **Dev Mode issue** | Quit Dev Mode and re-enter on the Xbox |
| **Xbox needs restart** | Full power cycle (hold power button 10 seconds) |

### Connection works but "No packages found"

- Your Xbox may have no packages installed — install something from the catalog first
- Try clicking **Refresh** in the Installed view

---

## Install Problems

### "Package manager busy"

The Xbox is already installing something. Wait 30–60 seconds and try again.

### "Dependency missing"

Use the **Custom Install Wizard** instead — it resolves dependencies automatically:

1. Open Custom Install
2. Select your package file
3. Let it analyze dependencies
4. Make sure all dependencies are selected
5. Proceed with install

### Install stuck at 0%

- The package may be large and the transfer hasn't started yet
- Check the log file for errors
- Cancel and retry
- If it keeps failing, check Xbox free disk space

### "Failed to upload package"

| Cause | Fix |
|-------|-----|
| **Disk space** | Free up space on your Xbox |
| **File too large** | Some packages may timeout during upload — try wired connection |
| **Network issues** | WiFi can be slow for large files; use Ethernet |
| **SFTP test** | Try uploading via File Explorer to verify the connection works |

### "Install completed but package manager reported failure"

The file was uploaded but the Xbox failed to register it. This is usually transient:

1. Wait a moment and check if the package appears in Installed view
2. Try installing again — sometimes a second attempt works
3. Check Dev Mode on the Xbox for any error messages

---

## App Crashes

### Crash on startup

1. Run `XBVault.exe --check` for diagnostics
2. Run `XBVault.exe --reset-data` to wipe corrupted data
3. If it still crashes, download a fresh copy

### Random crashes during use

1. Check log files for error details
2. Run `XBVault.exe --check` for a health report
3. The last 50 lines before the crash are in the log — include them when reporting

### How to report a crash

Include these in your bug report:

1. App version (`XBVault.exe --version`)
2. Your operating system (Windows 10/11, macOS version, Linux distro)
3. Xbox model and OS version (shown in System Info)
4. Steps that led to the crash
5. The log file from the logs folder

Open an issue: [github.com/marcelofrau/xb-homebrew-vault/issues](https://github.com/marcelofrau/xb-homebrew-vault/issues)

---

## USB / File Issues

### USB drive not detected

- USB detection is **Windows-only** — macOS/Linux users need to specify the drive path manually
- Make sure the drive is connected **before** launching the app
- Try a different USB port
- The drive must be formatted as **NTFS** (not FAT32 or exFAT)

### "Failed to grant USB permissions"

- Run XB Homebrew Vault as **Administrator** (right-click → Run as Administrator)
- The USB drive must be formatted as NTFS
- Make sure the drive letter is correct
- Some USB drives have hardware write-protection — disable it

### Can't see files in File Explorer

- The connection may be slow — wait for the file list to load
- Some Xbox directories are restricted and won't show
- File Explorer requires an active Dev Mode connection

---

## Other Issues

### Search not finding anything

- Search requires **at least 3 characters** — try a longer query
- Catalog may still be loading — wait for the spinner to finish
- Use the category filter to narrow results
- Click Refresh to reload the catalog

### Performance is slow

| Area | Explanation |
|------|-------------|
| **Catalog loading** | First launch is slower — it downloads the full catalog |
| **App startup** | Pre-flight checks run on every startup (typically < 1 second) |
| **Connection** | WiFi is slower than wired Ethernet for transfers |
| **Logging** | Debug log level can slow things down — use Info for normal use |

### "Settings file corrupted" message

The app auto-recovers by resetting to defaults. No action needed — unless you had custom settings, in which case you'll need to re-enter them.

### "Cache schema mismatch" message

The catalog cache format changed between versions. The app auto-clears it and re-fetches. No action needed.

---

## Running Diagnostics

### Health Check

```bash
XBVault.exe --check
```

Prints a diagnostic report covering:
- Settings validity
- Cache integrity
- Log directory accessibility
- System info (platform, architecture, .NET version)

### Viewing Logs

**Windows:**
```
%APPDATA%/XBVault/logs/
```

**Linux / macOS:**
```
~/.local/share/XBVault/logs/
```

Or click **Open Logs Folder** in Settings.

### Resetting Everything

```bash
XBVault.exe --reset-data
```

Wipes settings, cache, and logs. The app starts fresh.

---

## Still Stuck?

- **Run diagnostics:** `XBVault.exe --check`
- **Check logs:** See the logs folder above
- **Open an issue:** [github.com/marcelofrau/xb-homebrew-vault/issues](https://github.com/marcelofrau/xb-homebrew-vault/issues)
- **Ask the community:** Click the Discord icon in the app sidebar

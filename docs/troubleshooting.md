---
layout: default
title: Troubleshooting
---

# Troubleshooting

Quick fixes for common problems.

---

## App Won't Open

### Nothing happens when I double-click XBVault.exe

**Most likely:** A pre-flight check detected corruption or a fatal error occurred.

1. Open a terminal (Command Prompt / PowerShell) in the app folder
2. Run `XBVault.exe --check` to see the health report
3. Run `XBVault.exe --reset-data` to wipe settings, cache, and logs
4. Try launching again

Still won't open? Download a fresh copy from the [latest release](https://github.com/marcelofrau/xb-homebrew-vault/releases/latest).

### "The application failed to start" dialog

This is a fatal boot error. The dialog tells you which exception occurred. Common causes:

- **File access denied** — extract the ZIP to a writable folder (not Program Files, unless run as admin)
- **Missing DLL** — re-extract the ZIP, make sure all files are present
- **Corrupted settings** — run with `--reset-data` to wipe and restart

### App opens but window is blank / hangs

1. Wait 10-15 seconds — catalog may be loading for the first time
2. Check if your firewall is blocking the app
3. Run with `--console` to see log output in real-time
4. Check `%APPDATA%/XBVault/logs/` for error messages

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
- Try pinging the Xbox: `ping <xbox-ip>` — if it fails, network is the issue

### "Authentication failed"

Wrong username or password.

- Default username is `DevToolsUser` (unless you changed it in Dev Mode)
- Password is whatever you set in Dev Mode settings
- **Reset password** in Dev Mode: Settings → Remote Access → Reset Credentials
- Update the credentials in XBVault Settings and test again

### "Connection timed out"

Xbox is reachable but not responding to the Dev Mode API.

- **Firewall** — some networks block port `11443`. Try a different network or check router settings.
- **Same network** — PC and Xbox must be on the same local network
- **Restart Dev Mode** — on Xbox, quit Dev Mode and re-enter
- **Restart Xbox** — full power cycle (hold power button 10s)

### Connection works but "No packages found"

- Your Xbox may have no packages installed yet — install something from the catalog first
- Try clicking **Refresh** in the Installed view

---

## Install Problems

### "Package manager busy"

The Xbox is already installing something — wait 30-60 seconds and try again.

### "Dependency missing"

The custom install wizard should resolve this automatically:
1. Use **Custom Install** instead of one-click
2. Make sure all dependencies are selected in Step 3
3. If a dependency fails to install, try installing it individually from the catalog

### Install gets stuck at 0%

- The package may be large and the transfer hasn't started yet
- Check the log file for errors
- Cancel and retry
- If it keeps failing, the Xbox may not have enough free space

### "Failed to upload package"

- **Disk space** — check available space on your Xbox
- **File too large** — some very large packages may timeout during upload
- **Network** — WiFi can be slow for large packages; wired Ethernet is recommended
- Try uploading via **SFTP** (File Explorer) directly to verify the connection works

### "Install completed but package manager reported failure"

The file was uploaded but the Xbox failed to register it. This is usually transient:
1. Wait a moment and check if the package appears in Installed view
2. Try installing again — sometimes a second attempt works
3. Check Dev Mode on the Xbox for any error messages

---

## App Crashes

### Crash on startup

1. Run `XBVault.exe --check` to run diagnostics
2. Run `XBVault.exe --reset-data` to wipe corrupted data
3. If it still crashes, download a fresh copy

### Random crashes during use

- Check `%APPDATA%/XBVault/logs/` for error details
- Run `XBVault.exe --check` for a health report
- Logs show the last 50 lines before the crash — include them if reporting a bug

### How to report a crash

When reporting, include:
1. App version (`XBVault.exe --version`)
2. Your operating system (Windows 10, Windows 11, macOS version, etc.)
3. Xbox model and OS version (shown in System Info)
4. Steps that led to the crash
5. The log file from `%APPDATA%/XBVault/logs/`

Open an issue at: [github.com/marcelofrau/xb-homebrew-vault/issues](https://github.com/marcelofrau/xb-homebrew-vault/issues)

---

## USB / File Issues

### USB drive not detected

- USB detection is **Windows-only** — macOS and Linux users need to specify the drive path manually
- Make sure the drive is connected **before** launching the app
- Try a different USB port
- The drive must be formatted as **NTFS** (not FAT32 or exFAT)

### "Failed to grant USB permissions"

- Run XBVault as **Administrator** (right-click → Run as Administrator)
- The USB drive must be formatted as NTFS
- Make sure the drive letter is correct
- Some USB drives have hardware write-protection — disable it

### Can't see files in File Explorer

- The connection may be slow — wait for the file list to load
- Some Xbox directories are restricted and won't show
- File Explorer requires an active Dev Mode connection — check your connection settings

---

## Other Issues

### Search not finding anything

- Search requires **at least 3 characters** — try a longer query
- Catalog may still be loading — wait for the spinner to finish
- Use the category filter to narrow results
- Press Refresh to reload the catalog

### Performance is slow

- **Catalog loading** — first launch is slower because it downloads the full catalog
- **App startup** — pre-flight checks run on every startup (typically < 1 second)
- **Connection** — WiFi is slower than wired Ethernet for transfers
- **Logging** — setting log level to Debug can slow things down; use Info or Warn for normal use

### "Settings file corrupted" message

This is logged at startup when the settings file is unreadable. The app auto-recovers by resetting to defaults. No action needed — unless you had custom settings, in which case you'll need to re-enter them.

### "Cache schema mismatch" message

The catalog cache format changed between versions. The app auto-clears the cache and re-fetches the catalog. No action needed.

---

## Still Stuck?

- **Run diagnostics:** `XBVault.exe --check` prints a health report
- **Check logs:** Look in `%APPDATA%/XBVault/logs/` (Windows) or `~/.local/share/XBVault/logs/` (Linux/macOS)
- **Open an issue:** [github.com/marcelofrau/xb-homebrew-vault/issues](https://github.com/marcelofrau/xb-homebrew-vault/issues)
- **Ask the community:** Click the Discord icon in the app sidebar

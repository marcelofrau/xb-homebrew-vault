# Frequently Asked Questions

Quick answers to common questions about XB Homebrew Vault.

---

## General

### What is XB Homebrew Vault?

XB Homebrew Vault is a free, open-source desktop app that lets you manage homebrew apps, emulators, and tools on your Xbox One or Xbox Series S|X in Developer Mode. You can browse a catalog of packages, install them wirelessly, manage files, and use developer tools — all from your PC.

### Do I need to pay for it?

No. XB Homebrew Vault is completely free and open source (GPLv3).

### Does it work on all Xbox models?

It works on **Xbox One**, **Xbox One S**, **Xbox One X**, **Xbox Series S**, and **Xbox Series X** — as long as Developer Mode is enabled.

### What operating systems does it run on?

| Platform | Supported |
|----------|-----------|
| Windows 10/11 (x64) | ✅ |
| Windows on ARM (Surface Pro X) | ✅ |
| macOS (Apple Silicon) | ✅ |
| macOS (Intel) | ✅ |
| Linux (x64) | ✅ |
| Linux ARM (Raspberry Pi) | ✅ |

### Is there an Android version?

Android support is in development. The Android project exists but is not yet feature-complete.

### Does it require installation?

No. It's a **self-contained** app — just extract the ZIP and run. No installer, no dependencies, no admin rights needed (except for USB permission setup).

---

## Connection

### Can I connect to multiple Xboxes?

Not at the same time. XB Homebrew Vault connects to one Xbox at a time. You can change the connection in Settings to switch to a different Xbox.

### What port does it use?

Port **11443** (default for Xbox Dev Mode Device Portal). You can change this in Settings if your setup uses a different port.

### Can I connect over the internet?

No. The connection is local-network only — your PC and Xbox must be on the same Wi-Fi or Ethernet network.

### The app won't connect — what do I do?

See the [Troubleshooting — Connection Problems](Troubleshooting.md#cant-connect-to-xbox) section.

### Does it need the Xbox Device Portal to be open?

No. XB Homebrew Vault connects directly to the Dev Mode API. You don't need to open the Device Portal in a browser.

---

## Installing

### Is it safe to install packages?

Packages come from the **Emulation Revival** catalog, which curates and verifies homebrew apps. However, always exercise caution when installing software from any source.

### What file types can I install?

- `.appxbundle` — app bundle (multiple packages)
- `.msixbundle` — modern app bundle
- `.appx` — single app package
- `.msix` — modern single package
- `.zip` — compressed package (custom install)

### Can I install from a URL?

The Custom Install Wizard supports local files. For URL-based installs, download the file first, then use Custom Install.

### Do I need to install dependencies manually?

No. The app detects dependencies automatically and installs them in the correct order. You can uncheck specific dependencies in the Custom Install Wizard if needed.

### Can I uninstall packages?

Yes. Go to the Installed tab, select a package, and click Uninstall.

---

## Files & Storage

### Where does the app store data?

| Data | Windows | Linux / macOS |
|------|---------|---------------|
| Settings | `%APPDATA%/XBVault/` | `~/.local/share/XBVault/` |
| Logs | `%APPDATA%/XBVault/logs/` | `~/.local/share/XBVault/logs/` |
| Cache | `%LOCALAPPDATA%/XBVault/cache/` | `~/.cache/XBVault/` |

### How do I reset everything?

Run from the command line:
```bash
XBVault.exe --reset-data
```
This wipes settings, cache, and logs. The app starts fresh.

### Can I back up my settings?

Yes. Copy the `settings.json` file from the settings folder. You can restore it by placing it back in the same location.

---

## Developer Tools

### What is the Inspector?

The Inspector is a developer tool that connects to XRay agents running inside homebrew apps. It provides live log streaming and a Lua REPL for remote diagnostics. See [Inspector](Inspector.md) for details.

### Do all apps support the Inspector?

No. Only apps that include the XRay agent library (compiled with `XB_INSPECTOR_ENABLED`) will appear in the Inspector's agent scanner.

### What is the USB Permission Wizard?

It prepares a USB drive for use with Xbox Dev Mode by setting up NTFS file permissions. This is Windows-only. See [Dev Tools — USB Permission Wizard](Dev-Tools.md#usb-permission-wizard).

### What is X-Files Enablement?

A one-click wizard that sets up the X-Files homebrew file explorer app to work with the Xbox Device Portal REST API. See [Dev Tools — X-Files Enablement](Dev-Tools.md#x-files-enablement).

---

## Troubleshooting

### The app crashes on startup

1. Run `XBVault.exe --check` for diagnostics
2. Run `XBVault.exe --reset-data` to wipe corrupted data
3. If it still crashes, download a fresh copy

### Settings say "corrupted"

The app auto-resets to defaults. No action needed — just re-enter your settings.

### Catalog is slow to load

First launch downloads the full catalog. Subsequent loads use the cached version. If it's consistently slow, check your internet connection.

### How do I report a bug?

1. Get your version: `XBVault.exe --version`
2. Collect log files from the logs folder
3. Open an issue: [github.com/marcelofrau/xb-homebrew-vault/issues](https://github.com/marcelofrau/xb-homebrew-vault/issues)

---

## Privacy

### Does the app collect any data?

**No.** XB Homebrew Vault does not collect telemetry, analytics, or usage data. The only network request is fetching the catalog from Emulation Revival's CDN.

### Is my password stored securely?

Credentials are **obfuscated** (not stored in plain text) using XOR + salt. They never leave your PC.

### Does it require an account?

No. No account, no login, no cloud, no tracking.

See [Privacy & Data](Privacy-and-Data.md) for full details.

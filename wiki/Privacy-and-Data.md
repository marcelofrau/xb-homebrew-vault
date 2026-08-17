# Privacy & Data

XB Homebrew Vault is designed with privacy in mind. This page explains what data is stored, what is collected (nothing), and how to manage or reset your data.

---

## What We Collect

**Nothing.** XB Homebrew Vault does not collect any telemetry, analytics, or usage data. It has no "phone home" feature.

| What | Collected? |
|------|-----------|
| Usage analytics | ❌ No |
| Crash reports sent to us | ❌ No |
| Connection details sent anywhere | ❌ No |
| Package install history sent anywhere | ❌ No |
| Personal information | ❌ No |

The **only** outbound network request is fetching the package catalog from [Emulation Revival](https://emulationrevival.github.io)'s CDN. This is a public JSON file — no authentication, no tracking.

---

## Where Your Data Is Stored

All data stays **on your PC**. Nothing is sent to any server.

### Windows

| Data | Location |
|------|----------|
| Settings (connection, preferences) | `%APPDATA%/XBVault/settings.json` |
| Logs | `%APPDATA%/XBVault/logs/` |
| Package cache | `%LOCALAPPDATA%/XBVault/cache/` |
| Catalog cache | `%LOCALAPPDATA%/XBVault/cache/catalog-v1.json` |

### Linux / macOS

| Data | Location |
|------|----------|
| Settings | `~/.local/share/XBVault/settings.json` |
| Logs | `~/.local/share/XBVault/logs/` |
| Cache | `~/.cache/XBVault/` |

### To Find These Folders

- **Windows:** Open File Explorer and paste the path in the address bar
- **Linux / macOS:** Open a terminal and type `open ~/.local/share/XBVault` (macOS) or `xdg-open ~/.local/share/XBVault` (Linux)

Or use the **Open Settings Folder** and **Open Logs Folder** buttons in the Settings tab.

---

## Credential Storage

Your Xbox Dev Mode credentials (username and password) are stored in `settings.json` using **obfuscation** (XOR + salt). This means:

- Credentials are **not** stored in plain text
- They are **not** encrypted with a strong cipher (this is local-only obfuscation, not military-grade encryption)
- They **never leave your PC**
- They are **not** sent to any server

> **Note:** The obfuscation prevents casual reading of the settings file. It is not designed to resist a determined attacker with access to your PC. If you need stronger protection, use OS-level disk encryption.

---

## Resetting Your Data

### Via Command Line

```bash
XBVault.exe --reset-data
```

This shows a confirmation dialog, then deletes:
- All settings (connection details, preferences)
- All cached data (catalog, temp files)
- All logs

After reset, the app starts fresh as if it were the first launch.

### Manually

Delete the folders listed in the "Where Your Data Is Stored" section above. The app recreates them on next launch.

---

## Network Requests

The app makes these network requests:

| Request | Destination | Purpose |
|---------|-------------|---------|
| Catalog fetch | Emulation Revival CDN | Download the package catalog |
| Xbox connection | Your Xbox (local network) | Dev Mode API, SFTP, WebSocket |
| GitHub API | `api.github.com` | Check for app updates |

All Xbox communication is **local network only** — nothing goes to the internet.

---

## Open Source

XB Homebrew Vault is fully open source under the **GPLv3** license. You can inspect the code yourself to verify these privacy claims:

- Source code: [github.com/marcelofrau/xb-homebrew-vault](https://github.com/marcelofrau/xb-homebrew-vault)
- License: [GPLv3](https://github.com/marcelofrau/xb-homebrew-vault/blob/main/LICENSE)

---

## Summary

| Concern | Status |
|---------|--------|
| Telemetry | None |
| Analytics | None |
| Account required | No |
| Cloud storage | None |
| Password encryption | Obfuscated locally |
| Data leaves your PC | Only catalog fetch (public CDN) |
| Open source | Yes (GPLv3) |

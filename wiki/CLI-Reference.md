# CLI Reference

XB Homebrew Vault supports command-line options for advanced users, troubleshooting, and automation.

---

## Running from the Command Line

### Windows

Open Command Prompt or PowerShell and navigate to the folder where you extracted the app:

```powershell
cd C:\XBVault
.\XBVault.exe [options]
```

Or use the included helper scripts:
- `xbv-run.cmd` — launch normally
- `xbv-console.cmd` — launch with a visible console window (for debugging)

### macOS / Linux

Open a terminal and navigate to the app folder:

```bash
cd ~/XBVault
./XBVault [options]
```

Or use the included helper scripts:
- `xbv-run.sh` — launch normally
- `xbv-console.sh` — launch with a visible console window (for debugging)

---

## Available Options

| Flag | Short | Description |
|------|-------|-------------|
| `--help` | `-h`, `-?` | Show help message and exit |
| `--version` | `-v` | Show version number and exit |
| `--console` | `-c` | Open a console window for log output (Windows only) |
| `--reset-data` | `-r` | Reset all app data (settings, cache, logs) — requires confirmation |
| `--check` | — | Run health diagnostics and print report, then exit |

---

## Examples

### Show Help

```bash
XBVault.exe --help
```

Displays a summary of all available options.

### Show Version

```bash
XBVault.exe --version
```

Prints the current version number. Useful for bug reports.

### Launch with Console (Debugging)

```bash
XBVault.exe --console
```

Opens an additional console window that shows log output in real-time. This is very useful when:
- Troubleshooting connection issues
- Diagnosing crashes
- Seeing what the app is doing behind the scenes

### Reset All Data

```bash
XBVault.exe --reset-data
```

Wipes all application data:
- Settings (connection details, preferences)
- Cache (downloaded catalog, temp files)
- Logs

A confirmation dialog appears before anything is deleted. After reset, the app starts fresh as if it were the first launch.

**When to use this:**
- The app won't open due to corrupted data
- You want a completely fresh start
- You're troubleshooting persistent issues

### Run Health Check

```bash
XBVault.exe --check
```

Runs diagnostics and prints a report, then exits. The report checks:

| Check | What It Verifies |
|-------|-----------------|
| **Settings** | Are settings valid or corrupted |
| **Cache** | Is the catalog cache intact |
| **Log directory** | Is the log folder writable |
| **System** | Platform info, architecture, .NET version |

Results are also written to the log file.

---

## Helper Scripts

The release ZIP includes convenience scripts:

### Windows

| Script | Purpose |
|--------|---------|
| `xbv-run.cmd` | Launch the app normally (double-click or run from terminal) |
| `xbv-console.cmd` | Launch with console output visible |

### macOS / Linux

| Script | Purpose |
|--------|---------|
| `xbv-run.sh` | Launch the app normally |
| `xbv-console.sh` | Launch with console output visible |

Make sure the `.sh` scripts are executable:
```bash
chmod +x xbv-run.sh xbv-console.sh
```

---

## Tips

- **Start with `--console`** if something seems wrong — the console shows exactly what's happening
- **Use `--check`** before reporting a bug — it gives you a diagnostic snapshot
- **Use `--reset-data`** as a last resort — it wipes everything and gives you a clean slate
- **Combine with manual log inspection** — logs are at `%APPDATA%/XBVault/logs/` (Windows) or `~/.local/share/XBVault/logs/` (Linux/macOS)

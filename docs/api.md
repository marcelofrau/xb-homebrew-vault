---
layout: default
title: API Reference
---

# Xbox Device Portal API

Reference for the Xbox Developer Mode Device Portal endpoints used by XB Homebrew Vault.

## Base URL

```
https://<xbox-ip>:11443
```

## Authentication

HTTP Basic Authentication with the username and password configured in Xbox Developer Mode settings.

---

## Connection

### `GET /api/os/info`

Returns device information. Used for connection testing.

**Response**
```json
{
  "Name": "XboxOne",
  "Publisher": "Microsoft",
  "Version": "10.0.22621.1000"
}
```

---

## Package Management

### `GET /api/app/packagemanager/packages`

Lists all installed packages on the Xbox.

**Response**
```json
{
  "InstalledPackages": [
    {
      "PackageFullName": "RetroArch.20319A388A0CE8_px48cv62crvag",
      "PackageFamilyName": "RetroArch.20319A388A0CE8",
      "PackageOrigin": 0,
      "PackageSize": 124518400,
      "InstallDate": "2026-01-15T12:00:00Z"
    }
  ]
}
```

### `POST /api/app/packagemanager/package`

Installs a package. Accepts multipart/form-data with the package file.

Query parameters:
- `package` (optional): URL to download the package from

### `DELETE /api/app/packagemanager/package`

Uninstalls a package.

Query parameters:
- `package`: Full package name to uninstall

### `POST /api/taskmanager/app`

Launches an app by its PackageRelativeId.

Query parameters:
- `appid`: Base64-encoded + URL-escaped PackageRelativeId

### `POST /api/taskmanager/app/state`

Changes the state of a running package.

Query parameters:
- `package`: Base64-encoded + URL-escaped PackageFullName
- `state`: `suspend` \| `terminate`

### `GET /ext/app/runningtitle`

Returns the currently running foreground title.

**Response**
```json
{
  "PackageFullName": "RetroArch.20319A388A0CE8_px48cv62crvag"
}
```

---

## Processes

### `GET /api/resourcemanager/processes`

Lists all running processes. Also used for real-time performance data via WebSocket.

**Response**
```json
{
  "Processes": [
    {
      "ProcessId": 1234,
      "ImageName": "RetroArch.exe",
      "UserName": "XDK",
      "MemoryUsage": 52428800,
      "CpuUsage": 12.5,
      "PageFileUsage": 10240000
    }
  ]
}
```

### `DELETE /api/taskmanager/process`

Kills a process by PID.

Query parameters:
- `pid`: Process ID to kill

---

## Crash Dumps

### `GET /api/app/debug/crashdump`

Lists available crash dump files.

### `DELETE /api/app/debug/crashdump/{filename}`

Deletes a specific crash dump file.

### `GET /api/app/debug/crashcontrol`

Returns current crash dump settings.

**Response**
```json
{
  "CrashDumpEnabled": true
}
```

### `POST /api/app/debug/crashcontrol`

Enables or disables crash dump collection.

Form data:
- `CrashDumpEnabled`: `true` \| `false`

---

## Network

### `GET /api/networking/networkconfig`

Returns network configuration (interfaces, IP, DNS, MAC, link speed).

### `GET /api/wifi/interfaces`

Lists available WiFi interfaces.

### `GET /api/wifi/networks/{interfaceGuid}`

Lists WiFi networks visible to the specified interface.

---

## System

### `GET /api/system/info`

Returns detailed system information (OS version, console type, CPU, memory, serial).

### `GET /api/screenshot`

Captures a screenshot of the current Xbox screen. Returns raw image bytes.

### `POST /api/system/restart`

Restarts the Xbox console.

### `POST /api/system/shutdown`

Shuts down the Xbox console.

---

## Performance WebSocket

```
wss://<xbox-ip>:11443/api/resourcemanager/processes
```

Receives continuous JSON frames with performance snapshot data:

| Field | Description |
|-------|-------------|
| CPU usage | Per-core percentage |
| Memory | Working set / available |
| GPU clock | Current GPU frequency |
| Temperature | Per-core temperature readings |

Used by `PerformanceViewModel` to drive the real-time chart in the Dev Tools panel.

---

## References

- [Windows Device Portal API](https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/device-portal-api-core)
- [Xbox Developer Mode Guide](https://wiki.sternserv.xyz/docs/xbox-setup/xbox-developer-mode-setup)

---

[← Architecture](architecture) · [Roadmap →](roadmap)

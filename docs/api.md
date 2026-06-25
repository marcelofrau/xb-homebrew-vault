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

The same credentials are reused across all transports — HTTP Basic (this API), SFTP (SSH.NET), and SMB (USB folder access). See [Architecture → Design Decisions](architecture#design-decisions--rationale).

---

## Request Conventions

`XboxDeviceService` applies the same conventions to every call:

| Concern | Behavior | Rationale |
|---------|----------|-----------|
| **TLS certificate** | Validation is **bypassed** (`ServerCertificateCustomValidationCallback` always returns `true`). | The console serves a self-signed certificate. Dev-only tool on a trusted LAN. |
| **CSRF token** | A `CookieContainer` is shared across requests; the `CSRF-Token` cookie returned on the first call is sent automatically on subsequent state-changing requests. | WDP rejects unauthenticated `POST`/`DELETE` without the token. |
| **HttpClient lifetime** | A fresh `HttpClient` is created whenever `Configure(...)` is called. | `BaseAddress` is immutable once set; recreation allows switching consoles. |
| **Parameter encoding** | `appid` / `package` values are **Base64-encoded then URL-escaped**. | WDP expects opaque, URL-safe identifiers. |

### Common error responses

The Device Portal uses standard HTTP status codes. Typical failures:

| Status | Meaning | How the client reacts |
|--------|---------|-----------------------|
| `401 Unauthorized` | Wrong credentials or expired session | Surfaces a connection error; user re-enters credentials |
| `403 Forbidden` | Missing/expired CSRF token | A new request re-acquires the cookie |
| `404 Not Found` | Package/process/file no longer exists | Treated as already-removed in delete flows |
| `409 Conflict` | Package manager busy with another operation | Retried via readiness polling (see below) |
| `500 Internal Server Error` | Console-side failure (often during install) | Logged; operation reported as failed |

Error bodies, when present, follow:

```json
{
  "Reason": "The request could not be completed.",
  "ErrorCode": -2147023728
}
```

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

### Package manager readiness & polling

The package manager processes one operation at a time and is briefly unavailable after each upload/install. Before issuing the next operation (e.g. uploading a dependency), the client polls `GET /api/app/packagemanager/packages` and waits until the manager reports ready.

**Backoff strategy** (`WaitForPackageManagerReady`):

| Attempts | Delay each | Cumulative |
|---------:|-----------:|-----------:|
| 1–3 | 2s | up to 6s |
| 4–15 | 3s | up to 45s |

After 15 attempts (~45s) the wait gives up and the operation is reported as failed. Most installs become ready within 1–2 attempts. See the full [Package Installation Flow](integration-package-installation-flow) for the multipart upload pattern, dependency ordering, and failure recovery.

> **Multipart uploads:** install (`POST .../package`) accepts `multipart/form-data` with the package file. Dependencies are uploaded sequentially, each gated by the readiness poll above.

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

**Related guides:**
- [Package Installation Flow](integration-package-installation-flow) — multipart upload, dependency detection, polling & backoff
- [SSH/SFTP & Path Handling](integration-ssh-sftp-challenges) — file transfer outside WDP, `cmd.exe` quirks
- [Architecture](architecture) — where these endpoints map to services

---

[← Architecture](architecture) · [Roadmap →](roadmap)

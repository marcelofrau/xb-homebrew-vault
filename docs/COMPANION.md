# XBVault Companion API

## Overview

A companion UWP app that runs on the Xbox in Developer Mode and exposes file system, DVD/CD, USB, and sensor capabilities that the standard Windows Device Portal (WDP) does not provide.

**Port:** `11443` → `11444` (separate from WDP)

**Auth:** HTTP Basic Authentication (same credentials as WDP). HTTPS required.

## Path Convention

```
/drive/{letter}/{path}
```

Unix-style paths. Drive letter is a single uppercase character:

| Path | Resolves to |
|------|-------------|
| `/drive/C/Users/Public/` | `C:\Users\Public\` |
| `/drive/D/` | `D:\` (DVD drive) |
| `/drive/E/` | `E:\` (USB stick) |

## File System

### `GET /api/drives`

Lists all drives: internal storage, USB, DVD/CD-ROM, network.

```json
[
  {
    "letter": "C",
    "label": "Xbox Internal",
    "format": "NTFS",
    "type": "internal",
    "totalSize": 512000000000,
    "freeSpace": 256000000000,
    "isReady": true
  },
  {
    "letter": "E",
    "label": "USB_128GB",
    "format": "FAT32",
    "type": "usb",
    "totalSize": 128000000000,
    "freeSpace": 64000000000,
    "isReady": true
  }
]
```

### `GET /api/files/drive/{letter}/{path}`

List directory contents. Supports `?skip=N&take=N` pagination.

```json
{
  "parentPath": "/drive/E/Games",
  "items": [
    {
      "name": "RetroArch_1.16.0.0_x64.appx",
      "isDirectory": false,
      "extension": ".appx",
      "size": 52428800,
      "modifiedAt": "2026-06-20T14:30:00Z",
      "createdAt": "2026-06-15T10:00:00Z",
      "attributes": "Archive"
    }
  ],
  "total": 150
}
```

### `GET /api/files/drive/{letter}/{path}/info`

Metadata for a single file or directory.

### `GET /api/files/drive/{letter}/{path}/download`

Download file as byte stream. Supports `Range` header for resume.

### `POST /api/files/drive/{letter}/{path}/upload`

Upload single file via multipart/form-data. Max ~2GB.

Parameters:
- `file` — binary file data
- `overwrite` (boolean, default `true`)

### `POST /api/files/upload/batch`

Upload multiple files in one multipart request.

Parameters:
- `targetDir` — target directory on Xbox
- `files` — array of binary files
- `overwrite` (boolean, default `true`)

### `POST /api/files/drive/{letter}/{path}/extract-zip`

Upload a ZIP via multipart, extracts to target path, then deletes the ZIP. Existing files are overwritten.

```json
{
  "extracted": 24,
  "targetDir": "/drive/E/Games/MyGame",
  "totalSize": 524288000
}
```

### `DELETE /api/files/drive/{letter}/{path}`

Delete file or directory (recursive).

### `PATCH /api/files/drive/{letter}/{path}`

Rename or move:

```json
// Rename in place
{ "name": "new-name.appx" }

// Move to different directory
{ "targetPath": "/drive/D/Games/new-name.appx" }
```

### `POST /api/files/drive/{letter}/{path}/mkdir`

Create a directory.

### `GET /api/files/search?q=&dir=&skip=&take=`

Search files by name pattern.

```json
{
  "path": "/drive/E/Games",
  "items": [...],
  "totalResults": 42,
  "query": "retro"
}
```

### `POST /api/usb/eject/{letter}`

Safely eject a USB drive.

## Chunked Upload (2GB+ files)

For files larger than ~2GB, use the chunked upload workflow:

### 1. Create session: `POST /api/files/upload/session`

```json
{
  "filename": "large-game.pkg",
  "targetPath": "/drive/E/Games/",
  "totalSize": 3000000000,
  "chunkSize": 52428800
}
```

Returns:

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "filename": "large-game.pkg",
  "targetPath": "/drive/E/Games/",
  "totalSize": 3000000000,
  "chunkSize": 52428800,
  "totalChunks": 58,
  "receivedChunks": [],
  "status": "active"
}
```

### 2. Upload chunks: `POST /api/files/upload/session/{id}/chunk?index=N`

Send raw bytes as `application/octet-stream`. Chunks can be sent in any order.

### 3. Get progress: `GET /api/files/upload/session/{id}`

Returns session object with current `receivedChunks` array.

### 4. Complete: `POST /api/files/upload/session/{id}/complete`

Assembles all chunks. Fails if any chunk is missing.

### Cancel: `DELETE /api/files/upload/session/{id}`

Cancels session and cleans up partial data.

## DVD / CD-ROM

### `GET /api/dvd/info`

```json
{
  "isDiscInserted": true,
  "label": "XBOX_DISC",
  "format": "UDF",
  "totalSize": 4700000000,
  "isWritable": false
}
```

### `GET /api/dvd/files?path=`

List files on disc. Optional subdirectory filter.

### `POST /api/dvd/eject`

Open the disc tray.

## Sensors

### `GET /api/sensors`

```json
{
  "cpu": {
    "usagePercent": 23.5,
    "temperatureCelsius": null
  },
  "gpu": {
    "usagePercent": 45.2,
    "temperatureCelsius": null
  },
  "memory": {
    "totalBytes": 8589934592,
    "usedBytes": 4294967296
  },
  "power": {
    "state": "on",
    "batteryLevel": null,
    "isCharging": null
  },
  "system": {
    "uptimeMs": 86400000,
    "thermalThrottle": null
  },
  "fan": null
}
```

**Availability on Xbox:**

| Field | Available |
|-------|-----------|
| `cpu.usagePercent` | ✅ via `Windows.System.Diagnostics` |
| `cpu.temperatureCelsius` | ❌ always null |
| `gpu.usagePercent` | ⚠️ can be null |
| `gpu.temperatureCelsius` | ❌ always null |
| `memory.*` | ✅ via `MemoryManager` |
| `power.state` | ✅ via `PowerManager` |
| `power.batteryLevel` | ❌ always null (console) |
| `power.isCharging` | ❌ always null (console) |
| `system.uptimeMs` | ✅ |
| `system.thermalThrottle` | ⚠️ can be null |
| `fan.*` | ❌ always null |

## References

- OpenAPI spec: `docs/api/companion-openapi.yaml`
- WDP API (primary): `docs/API.md`

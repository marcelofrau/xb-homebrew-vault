# Companion UWP App — Specification

## 1. Overview

A UWP application that runs on Xbox One/Series consoles in **Developer
Mode**, acting as a companion service for **XB Homebrew Vault** (the
desktop app).

It exposes an HTTP REST API on a separate port (11444) for file system
operations, DVD/CD access, USB management, and system sensors — things
the standard Windows Device Portal (WDP) does not provide.

It also displays a full-screen read-only console on the TV showing
real-time interactions with the desktop client.

### Why not extend WDP?

WDP is a closed system by Microsoft — no custom endpoints possible. A
companion UWP sidesteps this limitation using UWP APIs for file access,
storage, and system management.

## 2. Architecture

```
┌─────────────────────────────────────────────────────┐
│  Xbox Console (Developer Mode)                      │
│                                                     │
│  ┌─────────────────────────────────────────────┐    │
│  │  Companion UWP Process                       │    │
│  │                                              │    │
│  │  ┌──────────┐   ┌───────────────────────┐   │    │
│  │  │ Console  │   │  HTTP Server (11444)   │   │    │
│  │  │ UI       │   │                       │   │    │
│  │  │ (screen) │   │  ┌─────────────────┐  │   │    │
│  │  │          │   │  │ FileController   │  │   │    │
│  │  └────┬─────┘   │  ├─────────────────┤  │   │    │
│  │       │         │  │ DvdController    │  │   │    │
│  │       │         │  ├─────────────────┤  │   │    │
│  │       │         │  │ SensorController │  │   │    │
│  │       │         │  ├─────────────────┤  │   │    │
│  │       │         │  │ DriveController  │  │   │    │
│  │       ▼         │  └─────────────────┘  │   │    │
│  │  ┌──────────┐   └──────────┬────────────┘   │    │
│  │  │ Log      │              │                │    │
│  │  │ Buffer   │◄─────────────┘                │    │
│  │  │ (5k)     │  Events pushed to buffer      │    │
│  │  └──────────┘                               │    │
│  └─────────────────────────────────────────────┘    │
│                                                     │
│  ┌─────────────────────────────────────────────┐    │
│  │  XBVault Desktop (PC)                       │    │
│  │  ────────────────────                       │    │
│  │  HTTP Basic Auth → https://xbox:11444       │    │
│  └─────────────────────────────────────────────┘    │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### Component responsibilities

| Component | Role |
|-----------|------|
| **HTTP Server** | `HttpListener`-like UWP listener on port 11444. Routes requests to controllers |
| **FileController** | Handles drive listing, file browsing, upload/download, rename, delete, mkdir, search |
| **DvdController** | Handles DVD info, file listing, eject |
| **SensorController** | Gathers system metrics via UWP APIs |
| **DriveController** | Manages drives, USB eject |
| **Log Buffer** | Circular in-memory buffer (5000 entries) consumed by console UI |
| **Console UI** | Full-screen XAML view bound to log buffer |

## 3. Project Structure

```
XboxCompanion/
├── Package.appxmanifest
├── XboxCompanion.csproj
├── App.xaml / .cs
├── MainPage.xaml / .cs          ← Full-screen console UI
│
├── Models/
│   ├── LogEntry.cs              ← Timestamp, Level, Message
│   ├── DriveInfo.cs             ← Letter, Label, Format, Type, Size, Free
│   ├── FileItem.cs              ← Name, IsDirectory, Size, ModifiedAt, etc.
│   ├── DvdInfo.cs               ← IsDiscInserted, Label, Format, Size
│   ├── UploadSession.cs         ← Id, Filename, TargetPath, TotalChunks, Status
│   └── SensorData.cs            ← Cpu, Gpu, Memory, Power, System, Fan
│
├── Services/
│   ├── HttpServer.cs            ← HTTP listener, routing, auth
│   ├── AuthService.cs           ← Basic auth validation
│   ├── LogBuffer.cs             ← Circular thread-safe buffer
│   └── SensorService.cs         ← UWP system sensor reads
│
├── Controllers/
│   ├── DrivesController.cs      ← GET /api/drives
│   ├── FilesController.cs       ← Browse, upload, download, delete, rename, mkdir, search
│   ├── ChunkedUploadController.cs ← Session create, chunk, complete, cancel
│   ├── DvdController.cs         ← Info, list, eject
│   └── SensorsController.cs     ← GET /api/sensors
│
├── Assets/
│   └── Fonts/                   ← Copy from XBVault (see §8)
│
└── Properties/
    └── Default.rd.xml
```

## 4. HTTP Server

### Requirements

- Listen on `https://0.0.0.0:11444`
- Support HTTPS with a self-signed cert (like WDP)
- HTTP Basic Authentication (reuse Xbox Dev Mode credentials)
- Route parsing: `{method} {path}` → controller
- Request body reading (multipart, octet-stream, JSON, form-urlencoded)
- Response writing (JSON, binary streams, status codes)
- CORS headers for browser-based testing
- X-CSRF-Token header passthrough (optional, for compatibility)

### Implementation approach

UWP supports `Windows.Networking.Sockets.StreamSocketListener` for
raw TCP. Build a lightweight HTTP parser on top:

```
StreamSocketListener
  └─> OnConnection
       └─> Read HTTP request line + headers
            └─> Parse method + path + auth header
                 └─> Validate credentials
                      └─> Route to controller
                           └─> Write HTTP response
```

Do NOT use ASP.NET Core or third-party HTTP libraries — they have
limited UWP/.NET Native support. A minimal hand-rolled parser is
~300 lines and more reliable.

### Port conflict prevention

Default port 11444. Must be configurable. If port is busy, log warning
and try next port (+1). Include actual port in startup log message.

## 5. Authentication

Use HTTP Basic Authentication with the Xbox Developer Mode credentials.
Same username/password the user configures in XBVault Settings.

```
Authorization: Basic base64(username:password)
```

All endpoints require auth. Return 401 on missing/invalid credentials.

The credentials should be:
- Hardcoded during initial development (for testing)
- Configurable via a local settings file or environment variable for release

## 6. API Endpoints

Full OpenAPI 3.0 spec: `docs/companion/openapi.yaml` (28 endpoints)

### Summary

| Group | Count | Endpoints |
|-------|-------|-----------|
| Drives | 1 | GET /api/drives |
| File browsing | 2 | GET/DELETE /api/files/drive/{letter}/{path} |
| File info | 1 | GET .../info |
| Download | 1 | GET .../download |
| Upload single | 1 | POST .../upload |
| Upload batch | 1 | POST /api/files/upload/batch |
| Upload chunked | 5 | session CRUD + chunk + complete |
| Extract ZIP | 1 | POST .../extract-zip |
| Mkdir | 1 | POST .../mkdir |
| Rename/move | 1 | PATCH .../{path} |
| Search | 1 | GET /api/files/search |
| USB eject | 1 | POST /api/usb/eject/{letter} |
| DVD | 3 | GET info, GET files, POST eject |
| Sensors | 1 | GET /api/sensors |

### Request flow for file upload (single)

```
POST /api/files/drive/E/path/to/dir/upload
Content-Type: multipart/form-data; boundary=...

--boundary
Content-Disposition: form-data; name="file"; filename="game.pkg"
Content-Type: application/octet-stream

<binary data>
--boundary--
```

Response:
```json
{ "name": "game.pkg", "size": 1048576, "path": "/drive/E/path/to/dir/game.pkg" }
```

### Request flow for chunked upload (2GB+)

```
1. POST /api/files/upload/session
   {"filename":"big.pkg", "targetPath":"/drive/E/Games/", "totalSize":3000000000}
   → 201 {"id":"uuid", "totalChunks":58, "status":"active"}

2. POST /api/files/upload/session/{id}/chunk?index=0
   Content-Type: application/octet-stream
   <raw 50MB chunk>
   → 200 {"index":0, "received":true}

3. (repeat for all chunk indices)

4. POST /api/files/upload/session/{id}/complete
   → 200 {"path":"/drive/E/Games/big.pkg", "size":3000000000}

Optional: GET /api/files/upload/session/{id}  → progress
Optional: DELETE /api/files/upload/session/{id} → cancel
```

Chunks may arrive out of order. File assembled only when all chunks
received. Default chunk size: 50MB.

## 7. Console UI

Full spec: `docs/companion/UIUX.md`

### Key requirements

- Full-screen, no window chrome
- Background: `#0D1117` (BladesBg)
- Header: status bar with companion version, uptime, active clients
- Log list: infinite-scroll, monospace (ProFontWindows Nerd Font 15px)
- Log format: `[HH:mm:ss]  ICON  LEVEL  Message`
- Auto-scroll to newest entry
- Circular buffer: 5000 lines max
- No user interaction — read-only display

### Log levels

| Level | Icon | Color |
|-------|------|-------|
| INFO | ● U+25CF | #9ACA3C |
| OK | ✓ U+2713 | #2ECC71 |
| WARN | ▲ U+25B2 | #F39C12 |
| ERROR | ✗ U+2717 | #E74C3C |
| DEBUG | … U+2026 | #5A5C60 |

## 8. Theme & Assets

### Fonts

Copy from XBVault (`XBVault/Assets/Fonts/`) into companion project
`Assets/Fonts/`:

| File | Use |
|------|-----|
| `Oxanium-700.ttf` | Header text |
| `Oxanium-400.ttf` | (reserve) |
| `ProFontWindowsNerdFont-Regular.ttf` | Log lines |

### Colors

Use the Xbox 360 Blades palette (from `docs/THEME.md`):

```xml
<!-- Resources.xaml -->
<Color x:Key="BladesBg">#0D1117</Color>
<Color x:Key="BladesSurfaceAlt">#252830</Color>
<Color x:Key="BladesAccent">#9ACA3C</Color>
<Color x:Key="BladesSuccess">#2ECC71</Color>
<Color x:Key="BladesDanger">#E74C3C</Color>
<Color x:Key="BladesWarning">#F39C12</Color>
<Color x:Key="BladesText">#F0F0F0</Color>
<Color x:Key="BladesTextMuted">#8B8D91</Color>
<Color x:Key="BladesTextDim">#5A5C60</Color>
<Color x:Key="BladesBorder">#2A2D33</Color>
```

### Icons

No icons needed for v1 (console is text-only with unicode symbols).
Future versions may import PNGs from the XBVault asset structure.

## 9. Build & Deployment

### Requirements

- Visual Studio 2022+ with UWP workload
- Target: Windows 10 Fall Creators Update (16299) or later
- Architecture: x64 (Xbox One/Series)
- .NET Native toolchain

### Package.appxmanifest capabilities

```xml
<Capabilities>
  <!-- Internet (client & server) for HTTP API -->
  <Capability Name="internetClient" />
  <Capability Name="internetClientServer" />

  <!-- File access for USB, DVD, internal drives -->
  <Capability Name="removableStorage" />
  <Capability Name="privateNetworkClientServer" />

  <!-- System management for sensors -->
  <DeviceCapability Name="systemManagement" />
</Capabilities>
```

### Deployment to Xbox

1. Build Release x64
2. Create .appxbundle or .msixbundle
3. Sideload via Windows Device Portal: `https://xbox:11443/` →
   App Explorer → Add
4. Port 11444 must be opened in Xbox network settings (usually auto)

### First run

- Companion auto-starts on boot (configure in manifest or via startup
  task)
- Console appears on the TV as the default full-screen view
- Log shows: `[--:--:--]  ●  INFO  Companion API started on port 11444`
- Desktop XBVault connects to port 11444 (configurable in Settings)

## 10. Threading Model

```
┌──────────────┐    ┌──────────────────┐    ┌──────────────┐
│ HTTP Server  │    │ SensorService    │    │ Console UI   │
│ (thread pool)│    │ (timer thread)   │    │ (UI thread)  │
└──────┬───────┘    └────────┬─────────┘    └──────┬───────┘
       │                     │                      │
       └─────────┬───────────┘                      │
                 ▼                                  │
        ┌────────────────┐                          │
        │  LogBuffer     │── ObservableCollection ──│
        │  (thread-safe) │    (via Dispatcher)      │
        └────────────────┘                          │
                                                   ▼
                                          ┌────────────────┐
                                          │  MainPage.xaml  │
                                          │  (ListBox)      │
                                          └────────────────┘
```

- HTTP server runs on background threads (one per connection)
- SensorService uses `ThreadPoolTimer` (every 5s)
- LogBuffer is a `ConcurrentQueue` with capacity limit
- Log entries dispatched to UI thread via `Dispatcher.RunAsync`
- No locks in LogBuffer — single-writer pattern per component

## 11. Configuration

Hardcoded defaults during initial development:

```csharp
public static class Config
{
    public const int DefaultPort = 11444;
    public const int MaxLogEntries = 5000;
    public const int DefaultChunkSize = 52428800; // 50 MB
    public const int SensorPollIntervalMs = 5000;

    // Override via environment variables or local settings.json
    public static string Username { get; set; } = "XBVault";
    public static string Password { get; set; } = "companion";
}
```

## 12. LogBuffer Implementation

```csharp
public class LogBuffer : INotifyCollectionChanged
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly int _capacity;
    private readonly Dispatcher _dispatcher;

    public void Write(LogLevel level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };

        _entries.Enqueue(entry);
        while (_entries.Count > _capacity && _entries.TryDequeue(out _)) { }

        // Notify UI on dispatcher thread
        _ = _dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            CollectionChanged?.Invoke(this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add, entry)));
    }
}
```

## 13. Sensor Reading (UWP APIs)

| Metric | API | Notes |
|--------|-----|-------|
| CPU usage | `Windows.System.Diagnostics` | `ProcessDiagnosticInfo.GetForCurrentProcess()` |
| Memory total | `Windows.System.MemoryManager` | `AppMemoryUsageLimit` |
| Memory used | `Windows.System.MemoryManager` | `AppMemoryUsage` |
| Power state | `Windows.System.Power.PowerManager` | `EnergySaverStatus`, `PowerSupplyStatus` |
| Uptime | `Environment.TickCount` or `KernelTransaction` | |

Temperature, fan speed, GPU data are NOT available via public UWP APIs
on Xbox — those fields always return `null`.

## 14. Error Handling

- All endpoints return JSON error bodies with `ErrorMessage`,
  `StatusCode`, `Reason`
- HTTP server catches all exceptions, logs them, returns 500
- File operations check path validity and return 404 for missing paths
- Disk full → 409 with descriptive message
- Auth failures → 401 with no body detail (security)

## 15. Testing

### Manual test scenarios

| Scenario | Steps |
|----------|-------|
| Auth OK | GET /api/drives with valid credentials → 200 |
| Auth fail | GET /api/drives with no/invalid credentials → 401 |
| List drives | GET /api/drives → array of DriveInfo |
| Browse dir | GET /api/files/drive/C/ → directory listing |
| Browse missing | GET /api/files/drive/Z/ → 404 |
| Download | GET /api/files/drive/C/test.txt/download → bytes |
| Upload small | POST .../upload (multipart, 1KB) → 200 |
| Upload >2GB | Chunked upload flow (session → chunks → complete) |
| USB eject | POST /api/usb/eject/E → 200 |
| DVD info | GET /api/dvd/info → disc info |
| DVD no disc | GET /api/dvd/info → isDiscInserted=false |
| Sensors | GET /api/sensors → SensorData |

### Automated testing

Not required for v1. Manual testing via:
- curl / Postman for API endpoints
- Visual observation of console UI on TV

## 16. Files reference

| File | Description |
|------|-------------|
| `docs/companion/SPEC.md` | This document — full architecture & implementation spec |
| `docs/companion/API.md` | Human-readable API reference with examples |
| `docs/companion/openapi.yaml` | OpenAPI 3.0 spec (28 endpoints, all schemas) |
| `docs/companion/UIUX.md` | Console UI design, colors, typography, layout |
| `docs/THEME.md` | XBVault theme reference (shared palette) |

## 17. Implementation Order (for the agent)

```
Phase 1 — Skeleton (1 session)
├── Create UWP project, csproj, manifest
├── Add fonts from XBVault Assets/Fonts/
├── Implement LogBuffer + LogEntry model
├── Implement MainPage.xaml (empty console UI)
└── Verify: builds, deploys, shows blank screen

Phase 2 — HTTP Server (1 session)
├── Implement HttpServer (StreamSocketListener + HTTP parse)
├── Implement AuthService (Basic auth)
├── Implement routing infrastructure
└── Verify: curl https://xbox:11444/api/drives → 401 or 200

Phase 3 — File System API (2 sessions)
├── Implement DriveController + FilesController
├── Implement UWP file I/O (KnownFolders, StorageFolder)
├── Implement single + batch upload
├── Implement chunked upload sessions
├── Implement extract-zip, rename, delete, mkdir, search
└── Verify: full file operations via curl

Phase 4 — DVD + Sensors (1 session)
├── Implement DvdController (info, list, eject)
├── Implement SensorController (CPU, memory, power)
└── Verify: endpoints return expected data

Phase 5 — Console UI polish (1 session)
├── Wire LogBuffer to MainPage via binding
├── Apply proper colors, fonts, spacing
├── Add header (version, uptime, clients)
├── Test auto-scroll and buffer limits
└── Verify: companion running on TV shows live log

Phase 6 — Integration test (1 session)
├── Port 11444 connectivity from PC
├── Auth flow with XBVault credentials
├── File upload from XBVault browse
└── Verify: end-to-end companion → desktop
```

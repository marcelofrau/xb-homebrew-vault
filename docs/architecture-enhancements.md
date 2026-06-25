## Services — Detailed Breakdown

> **Updated from Phase 1 code analysis:** Verified and expanded with decision rationale and integration patterns.

### XboxDeviceService (God Class)
**Metrics:** 1,207 lines, 35 public methods  
**Status:** ⚠️ **Split planned** — consolidating 8 unrelated domains

**Responsibilities (by domain):**

**1. Connection Management (6 methods)**
- `Configure(baseUrl, username, password)` — HTTP Basic auth setup, CSRF token initialization
- `IsConfigured`, `IsConnected` — Connection state

**2. Package Operations (9 methods)**
- `ListPackagesAsync()` — Enumerate installed packages
- `InstallPackageAsync(path, dependencies)` — Upload + install (multi-phase)
- `UninstallAsync(packageFullName)` — Remove package
- `LaunchAsync(packageFullName)` — Start app
- `SuspendAsync(packageFullName)` — Pause execution
- `TerminateAsync(packageFullName)` — Stop app
- `WaitForPackageManagerReady()` — Poll until ready (Xbox quirk)

**3. Process Management (2 methods)**
- `GetProcessesAsync()` — List running processes
- `KillProcessAsync(pid)` — Terminate by PID

**4. Crash Dumps (4 methods)**
- `ListCrashDumpsAsync()` — Enumerate dumps
- `DeleteCrashDumpAsync(fileName)` — Remove dump
- `GetCrashControlAsync()` — Check if crash dumps enabled
- `SetCrashControl(enabled)` — Enable/disable dumping

**5. Network Configuration (4 methods)**
- `GetNetworkConfigAsync()` — Network settings
- `GetWifiInterfacesAsync()` — List WiFi adapters
- `GetWifiNetworksAsync(interfaceGuid)` — Available networks

**6. System Operations (3 methods)**
- `GetSystemInfoAsync()` — Device specs, OS version
- `RestartXboxAsync()` — Reboot
- `ShutdownXboxAsync()` — Power off

**7. Miscellaneous**
- `GetScreenshotAsync()` → WebSocket connection (1 method)
- `FetchSmbPasswordAsync()`, `GetSshCredentials()` — SFTP auth

**Key Design Patterns:**
- **HTTP Client Recreation on Configure:** Fresh HttpClient per connection (BaseAddress immutability issue)
- **Certificate Validation Bypass:** Self-signed console certs, dev-only
- **CSRF Token via CookieContainer:** Automatic token attachment
- **WebSocket for Performance:** Real-time metrics streaming (separate from REST)

**Recommended Split (from TECH-DEBT.md):**
```
XboxPackageService     → install, uninstall, launch, suspend, terminate, list
XboxProcessService     → list, kill, running title
XboxCrashService       → list, delete, control
XboxNetworkService     → network config, WiFi
XboxSystemService      → info, restart, shutdown, screenshot
XboxPerformanceService → WebSocket connection
```

**Effort to Split:** 4-6 hours

---

### CatalogApiService
**Endpoint:** `https://emulationrevival.github.io/api/catalog.json`  
**Cache TTL:** 6 hours  
**Cache Location:** `%APPDATA%\XBVault\cache\catalog-api.json`

**Responsibilities:**
- Fetch catalog from JSON API
- Parse into CatalogItem models
- Cache with TTL + persistent disk storage
- Fallback to stale cache on API failure

**Decision Rationale:**
- **Why 6-hour TTL?** Catalog updates infrequently, 6h balances freshness + efficiency
- **Why persistent cache?** Allows offline browsing after first fetch, survives app restart
- **Why stale fallback?** Emulation Revival API can have brief downtime; stale data > no data

**Quirk: Self-Instantiation**
- `LoadFromCache()` static method creates instance to call `ClassifyDownloads()`
- Anti-pattern, should be extracted to static utility

---

### PackageInstallService
**Multi-phase installation pipeline:**

1. **Download** — Fetch all files (with cache check)
2. **Analyze** — Identify dependencies vs junk via regex
3. **Upload** — Sequential upload to Xbox
4. **Install** — Signal package manager
5. **Poll** — Wait for manager readiness between uploads

**Dependency Classification (Regex Patterns):**
```regex
DepPattern:   (?i)(microsoft\.|vclibs|net\.core|ui\.xaml|net\.native|vcruntime|dotnet|runtime\.)
JunkPattern:  (?i)(\.cer$|\.pfx$|add-appdevpackage|install\.ps1|\.appxsym$|\.psd1$|
              telemetrydependenc|logsideloading|diagnostics\.tracing|...)
InstallerExts: .appx, .msix, .appxbundle, .msixbundle
```

**Decision Rationale:**
- **Why multi-phase?** Xbox package manager requires all files before install request
- **Why dependency folder detection?** Different creators use `Dependencies/`, `deps/`, `dep/`
- **Why junk filter?** Certificates/scripts/diagnostics harmful if installed

**Xbox Package Manager Polling Quirk:**
```csharp
// Backoff pattern: 2s for first 3 attempts, 3s after
// Prevents hammering API during package installation
for (int i = 0; i < 15; i++)
{
    await Task.Delay(i < 3 ? 2000 : 3000);
    var info = await GetPackageManagerInfoAsync();
    if (info?.IsReady == true) return;
}
```

---

### SftpService
**Properly implements IDisposable** ✅

**Responsibilities:**
- Connect via SSH.NET wrapper
- SFTP file browsing + transfer
- Proper cleanup on disconnect

**Key Decision: Async Wrapper around Sync Library**
```csharp
// SSH.NET is synchronous, wrap in Task.Run to avoid blocking UI
public async Task ConnectAsync(string host, int port, string user, string pass)
{
    await Task.Run(() => {
        _ssh = new SshClient(connInfo);
        _ssh.Connect();
        _sftp = new SftpClient(connInfo);
        _sftp.OperationTimeout = TimeSpan.FromSeconds(15);
        _sftp.KeepAliveInterval = TimeSpan.FromSeconds(30);
        _sftp.Connect();
    });
}
```

**Why?** SSH.NET is synchronous; Task.Run prevents UI thread blocking

**Timeouts & Keep-Alive:**
- Operation timeout: 15 seconds (prevents hanging)
- Keep-alive: 30 seconds (prevents connection dropout during transfers)

---

### SettingsService
**Persists to:** `%APPDATA%\XBVault\settings.json`  
**Encryption:** XOR + Base64 obfuscation (NOT cryptographic)

**Why Obfuscation Instead of Encryption?**
- Goal: Hide credentials from casual inspection
- Threat model: Accidental file inspection (not targeted attack)
- If attacker has filesystem access, they have the key anyway (hardcoded in assembly)
- Trade-off: Simplicity vs cryptographic security (acceptable for dev tool)

---

### CacheService
**In-memory cache** with TTL for catalog items  
**Used by:** BrowseViewModel, RefreshViewModel

---

### CryptoService
**Pattern:** XOR each byte with key, then Base64 encode

**Example:**
```csharp
Input:  "MyPassword"
Key:    "SecretKey"
XOR:    Apply byte-wise XOR
Result: "VGVzdFBhc3N3b3Jk" (Base64)
```

---

### UsbDriveDetector (Windows-Only)
**Responsibilities:**
- Enumerate USB drives via WMI
- Grant permissions via icacls
- Allow Xbox Dev Mode to read external media

**Windows-Only Pattern:**
```csharp
#if WINDOWS
    // WMI code here
#endif
```

---

### AdminHelper
**Elevation helpers** for operations requiring admin rights

---

### Logger
**File + console logging**  
**Windows-specific:** `AttachConsole` via DllImport("kernel32.dll")

---

## MVVM Patterns & Conventions

### CommunityToolkit.Mvvm Source Generators

**Observable Property:**
```csharp
[ObservableProperty]
private string? selectedItem;
// Generates: public string? SelectedItem { get; set; } with INotifyPropertyChanged
```

**Relay Command:**
```csharp
[RelayCommand]
private async Task BrowseItemAsync()
{
    // Generates: public IAsyncRelayCommand BrowseItemCommand { get; }
}
```

### ViewModel Lifecycle Pattern

1. **Constructor** — Receive injected services
2. **Initialization** — Synchronous setup
3. **Async Setup** — Fire async commands during View load (via RelayCommand)

**Example (BrowseViewModel):**
```csharp
public BrowseViewModel(PackageInstallService installService, XboxDeviceService xbox)
{
    _installService = installService;  // Constructor injection
    _xbox = xbox;
}

[RelayCommand]
private async Task LoadCatalogAsync()
{
    // Async initialization, called from View.Loaded or other trigger
    var items = await _catalogService.FetchCatalogAsync();
    Items.Clear();
    Items.AddRange(items);
}
```

### Observable Collections

```csharp
[ObservableProperty]
private ObservableCollection<CatalogItem> items = new();

// Binding in AXAML:
// <ItemsControl ItemsSource="{Binding Items}" />
```

### Async/Await in ViewModels

**Current approach:** No `.ConfigureAwait(false)` used anywhere

**Rationale:** ViewModels need to stay on UI thread for ObservableProperty updates

**Recommended pattern:**
```csharp
// Service layer: use ConfigureAwait(false)
public async Task<Data> FetchDataAsync()
{
    var response = await _http.GetAsync(...).ConfigureAwait(false);
    return await Parse(response).ConfigureAwait(false);
}

// ViewModel layer: no ConfigureAwait (stays on UI thread)
private async Task LoadAsync()
{
    var data = await _service.FetchDataAsync();
    Items.Clear();
    Items.AddRange(data);  // Must update on UI thread
}
```

---

## Error Handling Issues & Recommendations

### Silent Exception Catches (12-14+ instances)

**CRITICAL:** Error handler has bare catches!
```csharp
// App.axaml.cs lines 107, 110
private void ShowErrorDialogSafe(Exception ex)
{
    try { /* show error dialog */ }
    catch { }  // ← Error in error handler!
}
```

**Recommendation:** Add minimal logging even in error handler

### async void Event Handlers (11 instances found)

**Fire-and-forget pattern → unhandled exceptions crash app**

**Current locations:**
- ConnectionWindow.axaml.cs:37 — Connection result handling
- ErrorDialog.axaml.cs:60 — Clipboard copy
- FileExplorerView.axaml.cs (3) — File operations
- LogsView.axaml.cs:41 — Clipboard copy
- NetworkInfoWindow.axaml.cs:15 — Window initialization
- SftpInfoWindow.axaml.cs (4) — Clipboard copies

**Recommendation:** Wrap in FireAndForget helper with exception logging

### Missing ConfigureAwait(false)

**Impact:** ~50+ await calls in XboxDeviceService capturing UI context unnecessarily

**Recommendation:** Add `.ConfigureAwait(false)` to all service-layer awaits

---

## Integration Decisions & Design Patterns

### 1. Package Installation Flow

**Why multi-phase?**
- Xbox package manager blocks during processing
- Pre-analysis avoids redundant downloads
- Progress reporting provides user feedback

**Quirk: Dependency Folder Names**
```csharp
// Case-insensitive detection
private static readonly HashSet<string> DepFolderNames = new(
    StringComparer.OrdinalIgnoreCase) { "Dependencies", "deps", "dep" };
```

### 2. SFTP Access Pattern

**Why Task.Run() wrapper?**
- SSH.NET is synchronous
- Prevents UI thread blocking during connection

**Credentials:**
- Fetch SMB password from `/ext/smb/developerfolder` endpoint
- Use same password for SSH auth (port 22, user "DevToolsUser")

### 3. WebSocket for Performance Metrics

**Why WebSocket instead of HTTP polling?**
- Real-time data (10+ samples/sec)
- Server push is more efficient
- Xbox Device Portal expects WebSocket

**Endpoint:** `wss://{xbox-ip}:11443/api/resourcemanager/processes`

### 4. Cache Strategy: 6-Hour TTL + Stale Fallback

**Why 6 hours?**
- Catalog updates infrequently
- Users unlikely to install twice in 6 hours
- Balances freshness vs network efficiency

**Why stale fallback?**
- Emulation Revival API can have brief downtime
- Stale data > no data (allows offline browsing)

### 5. Settings Persistence: XOR+Base64, NOT Encryption

**Why obfuscation instead of encryption?**
- Avoid plaintext passwords in JSON
- Crypto key would be hardcoded anyway
- Trade-off: simplicity vs security
- Acceptable for developer tool

### 6. Manual Service Composition (No DI Container)

**Current:** Explicit instantiation in App.axaml.cs

**Trade-offs:**
- **Pro:** Transparent, easy to follow for small projects
- **Con:** Tedious as services grow (currently 4+ services)

**Scalability:** Would benefit from `Microsoft.Extensions.DependencyInjection` if services double

### 7. Window Template Pattern

**All dialogs share:**
- `WindowDecorations="None"` (no OS chrome)
- Green border (#447F3E)
- Custom title bar with gradient
- Custom close button
- Draggable via BeginMoveDrag()

**Why?** Xbox 360 Blades theme styling, visual consistency

### 8. Credential Reuse Pattern

**Xbox credentials serve multiple purposes:**
- HTTP Basic auth (Device Portal)
- SFTP auth (SSH.NET)
- SMB folder access (USB drives)

**Single credential = simplified UX**

---

## Known Issues & Workarounds

### Avalonia 12 Border + Image Clipping

**Issue:** Border CornerRadius doesn't clip Image content  
**Workaround (pending):** RectangleGeometry clip or ImageBrush pattern

### Hardcoded Magic Delays (23 instances)

**Should be:** Named constants with descriptive identifiers

**Examples:**
- Splash duration: 2000ms
- Connection feedback: 1500-2000ms
- Package manager polling: 2000-3000ms (with backoff)
- Animation timing: 200-600ms (11 values in ConnectionViewModel)

### Gradient & Close Button Duplication

**Gradient:** Duplicated across 20+ windows  
**Close Button:** Duplicated styling across all windows

**Recommendation:** Extract as StaticResource + Style in BladesTheme.axaml

---

**Last updated:** 2026-06-25  
**Analyzed from:** Phase 1 code analysis (1,207 LOC XboxDeviceService + all services)  
**Next:** Enhance api.md, data-sources.md, add Mermaid diagrams

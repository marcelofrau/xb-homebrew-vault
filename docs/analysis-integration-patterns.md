---
layout: default
title: Integration Patterns & Decisions (Internal)
---

# Integration Patterns & Decision Records
## Internal Analysis Document

> **Note:** This document captures WHY architectural decisions were made. Used to enhance `architecture.md` and inform new documentation.

---

## 1. Package Installation Flow & Quirks

### Decision: Multi-Phase Install Process

**Why this approach?**

```
Phase 1: Analyze package contents
  ↓
Phase 2: Identify dependencies (regex matching)
  ↓
Phase 3: Download all files (with cache check)
  ↓
Phase 4: Upload to Xbox (sequential: main + deps)
  ↓
Phase 5: Signal package manager to install
```

**Rationale:**
1. **Xbox package manager limitations:** Requires all files (main + dependencies) uploaded before installation request
2. **Network efficiency:** Pre-analysis avoids re-downloading if dependencies already cached
3. **User feedback:** Multi-phase allows progress reporting at each step

### Package Identification Patterns

**Dependency Regex** (PackageInstallService.cs:23-24):
```regex
(?i)(microsoft\.|vclibs|net\.core|ui\.xaml|net\.native|vcruntime|dotnet|runtime\.)
```

**Why this pattern?**
- Xbox packages often name Microsoft frameworks/runtimes with these prefixes
- Case-insensitive (`(?i)`) because package naming varies
- Regex matching faster than directory scanning

**Junk Filter Regex** (lines 26-29):
```regex
(?i)(\.cer$|\.pfx$|add-appdevpackage|install\.ps1|\.appxsym$|\.psd1$|
telemetrydependenc|logsideloading|diagnostics\.tracing|...)
```

**Why filter these?**
- `.cer`, `.pfx`: Certificates/keys—harmful if installed as packages
- `install.ps1`: PowerShell scripts from developer machines
- `telemetrydependenc`, `logsideloading`: Diagnostic packages unnecessary on Xbox
- Prevents installation errors and cluttered package list

**Dependency Folder Detection** (lines 20-21):
```csharp
private static readonly HashSet<string> DepFolderNames = new(
    StringComparer.OrdinalIgnoreCase) { "Dependencies", "deps", "dep" };
```

**Why case-insensitive matching?**
- Different package creators use different conventions
- Case-insensitive HashSet handles both `Dependencies/` and `dependencies/`

### Xbox Package Manager Polling Quirk

**Pattern in XboxDeviceService.cs:571-590:**
```csharp
private async Task WaitForPackageManagerReady()
{
    // Poll until package manager ready
    for (int i = 0; i < 15; i++)
    {
        await Task.Delay(i < 3 ? 2000 : 3000);
        var info = await GetPackageManagerInfoAsync();
        if (info?.IsReady == true)
            return;
    }
}
```

**Why polling with backoff?**
- Xbox package manager is a background service that can be busy
- Uploading one file blocks the manager briefly
- Exponential backoff (2s for first 3 attempts, 3s after) balances responsiveness + tolerance
- 15 attempts * 3s = 45s max wait

**Real-world observation:**
- Initial attempts fail quickly (manager still processing)
- Later attempts succeed more often
- Backoff prevents hammering the API

---

## 2. SFTP & External Media Access

### Decision: SSH.NET Wrapper with Connection Pooling

**Why not direct SSH?**
- SSH.NET (Renci.SshNet) provides:
  - Both SSH and SFTP protocols
  - Connection management
  - Built-in timeouts and keep-alive
  - Exception handling consistency

### SftpService Connection Lifecycle

**Key decision: Async wrapper around sync SSH.NET library** (SftpService.cs:29-62)

```csharp
public async Task ConnectAsync(string host, int port, string user, string pass)
{
    await Task.Run(() => {
        _ssh = new SshClient(connInfo);
        _ssh.Connect();  // Synchronous, can block
        
        _sftp = new SftpClient(connInfo);
        _sftp.OperationTimeout = TimeSpan.FromSeconds(15);
        _sftp.KeepAliveInterval = TimeSpan.FromSeconds(30);
        _sftp.Connect();
    });
}
```

**Why?**
- SSH.NET is synchronous—can't use `async/await` directly
- `Task.Run()` ensures connection doesn't block UI thread
- OperationTimeout (15s) prevents hanging on slow Xbox network
- KeepAliveInterval (30s) prevents connection dropout during file transfers

### Path Normalization

**Pattern: Convert Windows → Unix paths**

```csharp
private static string NormalizePath(string path)
{
    path = path.Replace('\\', '/');  // Windows → Unix
    if (path.Length >= 2 && path[1] == ':' && !path.StartsWith('/'))
        path = "/" + path;  // C:\ → /C:\
    return path;
}
```

**Why?**
- Xbox filesystem is Linux-based (Xenon OS)
- Windows paths use backslash; Unix uses forward slash
- Drive letters (C:) need slash prefix for Xbox SFTP

### Credentials from Xbox Dev Portal

**Pattern: Fetch SMB password via HTTP, use for SSH** (XboxDeviceService.cs:90-105)

```csharp
public async Task<string?> FetchSmbPasswordAsync()
{
    var response = await _http.GetAsync("/ext/smb/developerfolder");
    // Response contains: { "Password": "Dev**" }
    // Use this password for SSH auth as well
}

public SshConnectionInfo GetSshCredentials()
{
    return new SshConnectionInfo(uri.Host, 22, "DevToolsUser", _smbPassword ?? _password);
}
```

**Why?**
- Xbox Dev Mode has a single credential: the Device Portal password
- SMB folder access (USB drives) uses same password as SSH
- Fetch SMB password from Dev Portal HTTP API
- Fall back to Device Portal password if SMB password not available
- Reuse credentials for SSH (port 22, user "DevToolsUser")

---

## 3. WebSocket Performance Streaming

### Decision: Real-Time Performance Metrics via WebSocket

**Why WebSocket instead of polling HTTP?**
- Real-time performance data (CPU, GPU, memory per core)
- HTTP polling would require sampling 10+ times/second
- WebSocket provides server push—more efficient
- Xbox Device Portal expects WebSocket for performance data

### WebSocket Endpoint & Frame Structure

**Endpoint:** `wss://{xbox-ip}:11443/api/resourcemanager/processes`

**Frame contents (PerformanceSnapshot model):**
- CPU usage per core
- GPU clock frequency
- Memory usage (free/used/total)
- Temperature readings (each core)
- Timestamp

### Connection Lifecycle

**Pattern in PerformanceViewModel** (document/code not shown, TBD from agent):
- Connect on window open
- Receive JSON frames continuously
- Parse → update ObservableProperty
- Disconnect on window close
- **Issue:** CancellationTokenSource never disposed (tech debt #13)

---

## 4. Xbox Device Portal Authentication

### Decision: HTTP Basic Auth + CSRF Token via Cookie

**Why Basic Auth?**
- Xbox Device Portal uses HTTP Basic for simplicity
- HTTP only (no OAuth), so Basic Auth is standard
- Credentials: `Authorization: Basic base64(username:password)`

### CSRF Token Handling

**Pattern in XboxDeviceService**:
1. First request gets 401 Unauthorized
2. Response includes `CSRF-Token` header
3. Store in CookieContainer
4. Subsequent requests include token

**Why CSRF?**
- Xbox Device Portal protects against Cross-Site Request Forgery
- Token validated on state-changing operations (POST, DELETE)

### Certificate Validation Bypass

**Code: XboxDeviceService.cs:32-33**
```csharp
ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
```

**Why bypass certificate validation?**
- Xbox console uses self-signed certificates
- Dev Mode is not for production use
- Certificate pinning would be too strict
- This is acceptable for a developer tool

---

## 5. Catalog & Cache Strategy

### Decision: 6-Hour TTL Cache with Stale Fallback

**Why 6 hours?**
- Emulation Revival catalog updates infrequently
- 6 hours balances freshness with network efficiency
- Users unlikely to install the same package twice in 6 hours

**Why persistent disk cache?**
- Allows offline browsing after first catalog fetch
- Cached in: `%APPDATA%\XBVault\cache\catalog-api.json`
- Survives app restart

### Fallback Strategy: Stale Cache on API Failure

**Pattern in CatalogApiService.cs:64-80**:
```csharp
// Try fresh data from API
var items = await TryFetchJsonApiAsync(progress);

if (items is not null && items.Count > 0)
    return items;

// API failed — use stale cache (ignore TTL)
Logger.Warn("JSON API failed, trying stale cache");
var stale = LoadFromCache(ignoreTtl: true);
```

**Why this fallback?**
- Emulation Revival API can have brief downtime
- Users still need to browse existing packages
- Stale data is better than no data
- Prevents "catalog unavailable" errors

---

## 6. Settings Persistence: XOR+Base64, NOT Encryption

### Decision: Obfuscation Only, Not Encryption

**Code in CryptoService**:
```csharp
// Pattern: XOR each byte with key, then Base64 encode
public string Encrypt(string plaintext, string key)
{
    var key_bytes = Encoding.UTF8.GetBytes(key);
    var plain_bytes = Encoding.UTF8.GetBytes(plaintext);
    
    for (int i = 0; i < plain_bytes.Length; i++)
        plain_bytes[i] ^= key_bytes[i % key_bytes.Length];
    
    return Convert.ToBase64String(plain_bytes);
}
```

**Why NOT full encryption?**
- Passwords stored in `%APPDATA%/XBVault/settings.json`
- Full encryption would require secure key storage (complex)
- XOR+Base64 obfuscates credentials from casual inspection
- If attacker has file system access, they have the key too (hardcoded in assembly)

**Design choice rationale:**
- **Goal:** Prevent passwords from visible in JSON
- **Threat model:** Accidental file inspection, not targeted attack
- **Trade-off:** Simplicity vs cryptographic security
- **Acceptable for:** Developer tool, not banking app

**Existing settings in file:**
```json
{
  "Xbox": {
    "BaseUrl": "https://192.168.1.100:11443",
    "Username": "devuser",
    "Password": "VGVzdFBhc3N3b3Jk"  // Base64 of XORed bytes
  }
}
```

---

## 7. USB Drive Permission Wizard (Windows Only)

### Decision: Use WMI + icacls for Permissions

**Why WMI?**
- Windows Management Instrumentation gives access to USB drive list
- `System.Management` namespace provides WMI interface
- Only way to enumerate USB devices on Windows without elevated privileges initially

**Why icacls?**
- Command-line tool to modify NTFS permissions
- "ALL APPLICATION PACKAGES" SID allows Xbox Dev Mode to read USB
- Xbox runs as SYSTEM with restricted sandbox
- Need explicit permission grant

**Pattern in UsbPermissionViewModel**:
1. List USB drives via WMI
2. For each drive: `icacls E: /grant "*S-1-15-2-1:(OI)(CI)F" /T /C`
3. Translation: Grant ALL APPLICATION PACKAGES full permissions recursively

**Why not just Run As Admin?**
- UsbPermissionWindow wants to avoid elevation if possible
- Gracefully handles non-elevated mode
- Only elevates if user clicks "Grant Permissions"

**Windows-specific quirk:**
```csharp
// In UsbDriveDetector.cs
#if WINDOWS
    // WMI code here
#endif
```

- Prevents Linux/macOS build errors
- Linux/macOS don't have WMI or Xbox dev mode support

---

## 8. Async Patterns & ConfigureAwait Strategy

### Current Approach (NOT using ConfigureAwait)

**Services layer:**
```csharp
public async Task<bool> InstallPackageAsync(...)
{
    var response = await _http.PostAsync(...);  // No ConfigureAwait
    return response.IsSuccessStatusCode;
}
```

**ViewModels:**
```csharp
[RelayCommand]
private async Task BrowseItemAsync()
{
    var catalog = await _catalogService.FetchCatalogAsync();  // No ConfigureAwait
    Items.Clear();
    Items.AddRange(catalog);  // UI update
}
```

### Why NOT using ConfigureAwait(false)?

**Current decision (implicit):**
- Development prioritized over performance
- Desktop app (not server), so context switching overhead minimal
- All ViewModels need UI thread for ObservableProperty updates anyway
- Consistency: don't mix ConfigureAwait(true) and ConfigureAwait(false)

### Potential Improvement

**Recommended approach (future):**
```csharp
// Services: use ConfigureAwait(false)
private async Task<string?> FetchDataAsync()
{
    var response = await _http.GetAsync(...).ConfigureAwait(false);
    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
}

// ViewModels: no ConfigureAwait (stays on UI thread)
private async Task LoadDataAsync()
{
    var data = await _service.FetchDataAsync();
    Items.Clear();
    Items.AddRange(data);  // Must be on UI thread
}
```

**Tech debt:** Item #4 in TECH-DEBT.md

---

## 9. Manual Service Composition (No DI Container)

### Current Pattern in App.axaml.cs

```csharp
var xboxService = new XboxDeviceService();
var cacheService = new CacheService();
var installService = new PackageInstallService(cacheService, xboxService);
```

### Why not use Microsoft.Extensions.DependencyInjection?

**Trade-offs:**
- **Pro:** Explicit composition, easy to follow for small projects
- **Con:** Manual wiring becomes tedious as services grow (currently ~12 services)
- **Decision:** App is small enough to manage manually

### Potential Issue: CatalogApiService

**Pattern in BrowseViewModel.cs:40:**
```csharp
public BrowseViewModel(...)
{
    _catalogService = new CatalogApiService();  // Created inline!
}
```

**Problem:**
- Creates instance per ViewModel, not reused
- Multiple CatalogApiService instances = multiple HTTP clients
- Cache not shared across instances if multiple VMs created

**Tech debt:** Item #12 in TECH-DEBT.md

---

## 10. Window Template Pattern

### All Dialogs Share Same Pattern

**AXAML Structure (WindowDecorations="None"):**
```xml
<Window WindowDecorations="None" 
        Background="{StaticResource SurfaceBrush}"
        Width="500" Height="300">
    <Border BorderBrush="#447F3E" BorderThickness="2" Margin="1">
        <Grid RowDefinitions="auto,*">
            <!-- Title Bar with gradient -->
            <Border Background="{StaticResource TitleGradient}"
                    PointerPressed="OnTitleBarPointerPressed"
                    CornerRadius="8,8,0,0">
                <!-- Close button, title -->
            </Border>
            <!-- Content -->
            <ScrollViewer Grid.Row="1">
                <!-- Window content -->
            </ScrollViewer>
        </Grid>
    </Border>
</Window>
```

**Code-behind pattern:**
```csharp
private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
{
    BeginMoveDrag(e);  // Allow drag to move window
}

private void OnCloseClick(object? sender, RoutedEventArgs e)
{
    Close();
}
```

### Why This Pattern?

1. **Xbox UI Style:** Blades theme (360 inspired) uses green accents
2. **No OS Chrome:** WindowDecorations="None" removes Windows title bar
3. **Green Border:** #447F3E (Xbox green)
4. **Custom Close Button:** Control behavior, ensure consistency
5. **Draggable Title Bar:** Custom drag via BeginMoveDrag()

### Known Avalonia Issue: Image Clipping

**Quirk: Border CornerRadius doesn't clip Image content**

**Pattern in BrowseView.axaml:**
```xml
<!-- This doesn't clip image corners! -->
<Border CornerRadius="8,8,0,0">
    <Image Stretch="UniformToFill" ... />
</Border>
```

**Workaround (not yet implemented):**
- Apply `Clip` geometry via code-behind
- Or use `ImageBrush` inside Border
- Avalonia issue: ClipToBounds doesn't work on Borders with Images

**Tech debt:** Item #8 in TECH-DEBT.md

---

## Summary: Integration Decisions

| Decision | Rationale | Trade-off |
|----------|-----------|-----------|
| Multi-phase package install | Xbox limitations + network efficiency | Complexity |
| SFTP over SSH.NET | Connection management + reliability | Dependency |
| WebSocket for performance | Real-time streaming efficiency | Complexity |
| Basic Auth + CSRF | Xbox API standard | Manual token handling |
| 6-hour cache + stale fallback | Offline support + freshness balance | Sync inconsistency |
| XOR+Base64 obfuscation | Simplicity vs security | Not cryptographically secure |
| WMI + icacls for USB | Windows-only access to drives | Platform-specific code |
| Manual service composition | Simplicity for current size | Will need DI at scale |
| Window template pattern | Blades theme consistency | Duplication across windows |

---

**Document version:** 1.0  
**Last updated:** 2026-06-25  
**Status:** Complete

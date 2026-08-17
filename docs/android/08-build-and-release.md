---
layout: default
title: Build and Release
---

# Build and Release — Android

## Prerequisites

### Development Environment

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0.x | Build runtime |
| Android SDK | API 36 | Android platform (`$env:LOCALAPPDATA\Android\Sdk\platforms;android-36`) |
| Android Build Tools | 36.0.0 | APK compilation (`$env:LOCALAPPDATA\Android\Sdk\build-tools;36.0.0`) |
| Java JDK | **21** (exactly) | Android toolchain — JDK 25+ fails with XA0030 |
| Android Emulator | Latest | Testing |

### JDK Setup (Windows)

```powershell
# JDK 21 is bundled with Android SDK
$env:JAVA_HOME = "$env:LOCALAPPDATA\Android\Sdk\jdk-21"

# Verify
& "$env:JAVA_HOME\bin\java" -version
# openjdk version "21.0.x" ...
```

**Do NOT use JDK 25+** — the Android SDK build tooling rejects it with `error XA0030: Building with JDK version 25.x is not supported`.

### Visual Studio

For running from Visual Studio (VS2022+):
1. Install workload: **.NET Multi-platform App UI development** (includes .NET for Android)
2. Ensure Android SDK and JDK 21 are detected by VS
3. Open `XBVault.sln`, set `XBVault.Android` as startup project, select emulator/device, F5

---

## Project Structure

```
XBVault.sln                         ← Solution with all 4 projects

XBVault/XBVault.csproj              ← Shared library (net10.0, Library)
XBVault.Desktop/XBVault.Desktop.csproj  ← Desktop host (net10.0, WinExe/Exe)
XBVault.Android/XBVault.Android.csproj  ← Android host (net10.0-android36.0, Exe)
tests/XBVault.Tests/XBVault.Tests.csproj ← xUnit tests (net10.0)
```

### Why 3 Projects?

The Avalonia canonical pattern uses a shared library + platform hosts:
- `XBVault/` is a **pure Library** (no OutputType, no RuntimeIdentifiers) — this is what makes the Android `ProjectReference` work without MSBuild outer-build hacks
- `XBVault.Desktop/` contains only `Program.cs` (entry point) and references `Avalonia.Desktop`
- `XBVault.Android/` contains only `MainActivity.cs` and `AndroidApp.cs`, references `Avalonia.Android`

---

## Build Commands

### Solution (everything)

```powershell
$env:JAVA_HOME = "$env:LOCALAPPDATA\Android\Sdk\jdk-21"
rtk dotnet build XBVault.sln -c Debug
```

### Desktop only

```powershell
powershell -File build/build.ps1
# or
rtk dotnet build XBVault.Desktop/XBVault.Desktop.csproj -c Debug
```

### Android only

```powershell
$env:JAVA_HOME = "$env:LOCALAPPDATA\Android\Sdk\jdk-21"
powershell -File build/build-android.ps1
# or
rtk dotnet build XBVault.Android/XBVault.Android.csproj -c Debug
```

### Run desktop

```powershell
powershell -File build/run.ps1
```

### Run Android (requires emulator or device)

```powershell
$env:JAVA_HOME = "$env:LOCALAPPDATA\Android\Sdk\jdk-21"
powershell -File build/run-android.ps1
```

### Release builds

```powershell
# Desktop (Windows x64)
powershell -File build/build-release.ps1 -Version 1.4.0 -Arch x64

# Desktop (Linux/macOS)
bash build/build-release.sh 1.4.0 x64

# Android (arm64)
$env:JAVA_HOME = "$env:LOCALAPPDATA\Android\Sdk\jdk-21"
powershell -File build/build-release-android.ps1 -Version 1.4.0
```

### Tests

```powershell
rtk dotnet test tests/XBVault.Tests/XBVault.Tests.csproj -c Release
# 240 tests, all pass
```

---

## CI/CD Pipeline

### GitHub Actions (`.github/workflows/build.yml`)

| Job | Trigger | Runner | What it does |
|-----|---------|--------|--------------|
| `build` | push/PR to main | windows-latest + ubuntu-latest | `dotnet build XBVault.Desktop` |
| `build-android` | push/PR to main | windows-latest | `dotnet build XBVault.Android` (JDK 21 + Android SDK) |
| `test` | push/PR to main | windows-latest + ubuntu-latest | `dotnet test` |
| `release` | tag `v*` | matrix | win-x64, win-arm64, linux-x64, osx-x64, osx-arm64, **android-arm64** |
| `publish` | tag `v*` | ubuntu-latest | GitHub Release with all ZIPs + checksums |

### Release Matrix

| Platform | RID | Script | Output |
|----------|-----|--------|--------|
| Windows x64 | win-x64 | `build-release.ps1` | `XBVault-v{V}-win-x64.zip` + optional installer |
| Windows ARM64 | win-arm64 | `build-release.ps1 -Arch arm64` | `XBVault-v{V}-win-arm64.zip` |
| Linux x64 | linux-x64 | `build-release.sh` | `XBVault-v{V}-linux-x64.zip` |
| Linux ARM64 | linux-arm64 | `build-release.sh` | `XBVault-v{V}-linux-arm64.zip` |
| macOS x64 | osx-x64 | `build-release.sh` | `XBVault-v{V}-osx-x64.zip` |
| macOS ARM64 | osx-arm64 | `build-release.sh` | `XBVault-v{V}-osx-arm64.zip` |
| Android ARM64 | android-arm64 | `build-release-android.ps1` | `XBVault-v{V}-android-arm64.zip` |

---

## Android Manifest

`XBVault.Android/AndroidManifest.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
  <uses-permission android:name="android.permission.INTERNET" />
  <uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
  <application
      android:label="XBVault"
      android:allowBackup="false"
      android:supportsRtl="true"
      android:theme="@style/MainTheme" />
</manifest>
```

Permissions: `INTERNET` + `ACCESS_NETWORK_STATE` (required for Xbox HTTP/SSH).

---

## Android Resources

`XBVault.Android/Resources/values/styles.xml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<resources>
  <style name="MainTheme" parent="Theme.AppCompat.DayNight.NoActionBar">
    <item name="android:windowActionBar">false</item>
    <item name="android:windowNoTitle">true</item>
  </style>
</resources>
```

A `values-v31` variant provides Material You splash support.

---

## Troubleshooting

### "Building with JDK version 25.x is not supported"

Use JDK 21: `set JAVA_HOME=%LOCALAPPDATA%\Android\Sdk\jdk-21`

### "minSdkVersion 21 cannot be smaller than version 23"

`SupportedOSPlatformVersion` in `XBVault.Android.csproj` must be `23` (required by `androidx.lifecycle.runtime` dependency from Avalonia.Android).

### "Ambiguous project name 'XBVault'"

The shared library and desktop host must not both have `AssemblyName=XBVault`. Currently, neither sets `AssemblyName` explicitly — defaults to `XBVault` (shared) and `XBVault.Desktop` (host).

### "resource style/MainTheme not found"

Ensure `XBVault.Android/Resources/values/styles.xml` exists with a `MainTheme` style definition.

### Android build hangs / OOM

Single RID (`android-arm64`) avoids MSBuild outer-multi-RID build issues. Do not add more RIDs to `RuntimeIdentifiers` in the Android csproj without understanding the outer-build propagation problem.

---

## Output Artifacts

| Artifact | Extension | Use Case |
|----------|-----------|----------|
| Debug APK | `.apk` | Development and testing |
| Release APK | `.apk` | Sideload distribution |
| AAB | `.aab` | Google Play Store |

### File Naming Convention

```
XBVault-v{Version}-android-arm64.zip
```

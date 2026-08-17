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
| Android SDK | API 34+ | Android platform |
| Android Build Tools | 34.0.0+ | APK/AAB compilation |
| Android Emulator | Latest | Testing |
| Java JDK | 17+ | Android toolchain |
| Visual Studio 2022 | 17.8+ | IDE (optional) |

### Android Workload

```bash
# Install Android workload for .NET
dotnet workload install android

# Verify
dotnet workload list
# Should show: android
```

### Environment Variables

```bash
# Windows
set ANDROID_HOME=%LOCALAPPDATA%\Android\Sdk
set JAVA_HOME=C:\Program Files\Microsoft\jdk-17.x.x

# Linux/macOS
export ANDROID_HOME=$HOME/Android/Sdk
export JAVA_HOME=/usr/lib/jvm/java-17-openjdk
```

---

## Project Configuration

### XBVault.Android.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-android36.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <RootNamespace>XBVault.Android</RootNamespace>
    <AssemblyName>XBVault.Android</AssemblyName>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- Android-specific -->
    <SupportedOSPlatformVersion>21</SupportedOSPlatformVersion>
    <AndroidApplication>true</AndroidApplication>
    <AndroidSigningKeyStore>debug.keystore</AndroidSigningKeyStore>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.0.0" />
    <PackageReference Include="Avalonia.Android" Version="12.0.0" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.0" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="12.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\XBVault\XBVault.csproj" />
  </ItemGroup>
</Project>
```

### Conditional Packages (if needed)

If certain NuGet packages don't support Android:

```xml
<!-- SSH.NET — should work, but add fallback if needed -->
<PackageReference Include="SSH.NET" Version="2026.0.0"
                  Condition="'$(TargetFramework)' != 'net10.0-android36.0'" />

<!-- AvaloniaEdit — may not support Android -->
<PackageReference Include="Avalonia.AvaloniaEdit" Version="12.0.0"
                  Condition="'$(TargetFramework)' != 'net10.0-android36.0'" />
```

### Desktop csproj Guards

The existing `XBVault.csproj` may need adjustments to avoid pulling desktop-only packages into Android builds. These are already mostly guarded:

```xml
<!-- Existing guards in XBVault.csproj — verify these work -->
<PackageReference Include="Avalonia.Desktop" Version="12.0.0" />
<!-- ^^^ This should NOT be referenced by XBVault.Android -->

<PackageReference Include="System.Management" Version="8.0.0" />
<!-- ^^^ Only needed on Windows — conditional reference may be needed -->
```

Since `XBVault.Android` references `XBVault` as a `<ProjectReference>`, transitive packages flow through. If `Avalonia.Desktop` causes issues on Android, make it conditional in the desktop csproj:

```xml
<PackageReference Include="Avalonia.Desktop" Version="12.0.0"
                  Condition="'$(TargetFramework)' != 'net10.0-android36.0'" />
```

---

## Build Commands

### Debug Build

```bash
dotnet build XBVault.Android -f net10.0-android36.0 -c Debug
```

### Release Build

```bash
dotnet build XBVault.Android -f net10.0-android36.0 -c Release
```

### Publish as APK (sideloading)

```bash
dotnet publish XBVault.Android -f net10.0-android36.0 -c Release -o ./publish-android
# Output: ./publish-android/xbvault.android.apk
```

### Publish as AAB (Google Play)

```bash
dotnet publish XBVault.Android -f net10.0-android36.0 -c Release -p:AndroidAppBundle=true -o ./publish-android-aab
# Output: ./publish-android-aab/xbvault.android.aab
```

---

## Signing

### Debug Signing

Debug builds use the default Android debug keystore:

```bash
# Default location
~/.android/debug.keystore
# Password: android
# Alias: androiddebugkey
```

### Release Signing

For production releases:

```bash
# Generate keystore
keytool -genkeypair -v -keystore release.keystore \
  -alias xbvault -keyalg RSA -keysize 2048 -validity 10000

# Configure in csproj
<PropertyGroup>
  <AndroidSigningKeyStore>release.keystore</AndroidSigningKeyStore>
  <AndroidSigningKeyAlias>xbvault</AndroidSigningKeyAlias>
  <AndroidSigningStorePass>$(KEYSTORE_PASSWORD)</AndroidSigningStorePass>
  <AndroidSigningKeyPass>$(KEY_PASSWORD)</AndroidSigningKeyPass>
</PropertyGroup>
```

**Never commit keystores or passwords to the repository.**

---

## Android Manifest

### AndroidManifest.xml

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
    package="com.marcelofrau.xbvault">

  <!-- Network access (required for Xbox connection) -->
  <uses-permission android:name="android.permission.INTERNET" />
  <uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />

  <!-- Optional: WiFi state for network detection -->
  <uses-permission android:name="android.permission.ACCESS_WIFI_STATE" />

  <application
      android:label="XBVault"
      android:icon="@mipmap/ic_launcher"
      android:allowBackup="false"
      android:supportsRtl="true"
      android:theme="@style/MainTheme">
  </application>
</manifest>
```

### Permissions Summary

| Permission | Required | Purpose |
|------------|----------|---------|
| `INTERNET` | Yes | HTTP/SSH communication with Xbox |
| `ACCESS_NETWORK_STATE` | Yes | Detect network availability |
| `ACCESS_WIFI_STATE` | Optional | WiFi-specific state detection |
| `WRITE_EXTERNAL_STORAGE` | No | Not needed — app uses private storage |
| `READ_EXTERNAL_STORAGE` | No | Not needed — file picker uses SAF |

---

## CI/CD Pipeline

### GitHub Actions Workflow

Add to `.github/workflows/build.yml`:

```yaml
  build-android:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Setup Java
        uses: actions/setup-java@v4
        with:
          distribution: 'microsoft'
          java-version: '17'

      - name: Install Android workload
        run: dotnet workload install android

      - name: Restore
        run: dotnet restore XBVault.Android

      - name: Build
        run: dotnet build XBVault.Android -f net10.0-android36.0 -c Release --no-restore

      - name: Upload APK artifact
        uses: actions/upload-artifact@v4
        with:
          name: xbvault-android
          path: XBVault.Android/bin/Release/net10.0-android36.0/*.apk
```

### Release Workflow (tag-triggered)

```yaml
  release-android:
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/v')
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET + Java + Android
        # ... same as above

      - name: Publish APK
        run: |
          dotnet publish XBVault.Android \
            -f net10.0-android36.0 \
            -c Release \
            -p:AndroidAppBundle=false \
            -o ./publish-android

      - name: Upload to GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: ./publish-android/*.apk
```

---

## Output Artifacts

| Artifact | Extension | Use Case |
|----------|-----------|----------|
| Debug APK | `.apk` | Development and testing |
| Release APK | `.apk` | Sideload distribution |
| AAB | `.aab` | Google Play Store |

### File Naming Convention

```
XBVault-v{Version}-android.apk
XBVault-v{Version}-android.aab
```

Example: `XBVault-v1.4.0-android.apk`

---

## Distribution

### Sideload (direct APK)

Users download the APK and install directly:
1. Enable "Install from unknown sources" on device
2. Transfer APK to device
3. Open APK file to install

### Google Play Store (future)

Requires:
1. Google Play Developer account ($25 one-time)
2. AAB format
3. Store listing (screenshots, description, privacy policy)
4. Content rating questionnaire
5. Data safety section (network usage disclosure)

Not required for initial release.

---

## Troubleshooting

### Build Fails: "Android workload not installed"

```bash
dotnet workload install android
dotnet workload restore
```

### Build Fails: "ANDROID_HOME not set"

```bash
# Find SDK location
dotnet workload search android
# Set ANDROID_HOME to the SDK path
```

### Build Fails: "Java SDK not found"

Install Java JDK 17+ and set `JAVA_HOME`.

### APK Installs But Crashes on Launch

1. Check logcat: `adb logcat -s "XBVault"`
2. Common causes:
   - Missing NuGet package for Android RID
   - P/Invoke without platform guard
   - `Avalonia.Desktop` package pulled into Android build

### SSH Connection Fails on Android

1. Verify INTERNET permission in manifest
2. Test with `adb shell curl` to Xbox IP
3. Check SSH.NET package supports Android RID

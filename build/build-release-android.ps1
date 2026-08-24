param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Arch = "arm64",
    [string]$OutputDir = "dist"
)

# Strip leading v prefix if present
$Version = $Version -replace '^v', ''

# Deterministic versionCode from semver: major*1000000 + minor*1000 + patch
$vparts = $Version -split '\.'
$major = [int]$vparts[0]
$minor = if ($vparts.Count -gt 1) { [int]$vparts[1] } else { 0 }
$patch = if ($vparts.Count -gt 2) { [int]($vparts[2] -replace '[^0-9].*$', '') } else { 0 }
$versionCode = $major * 1000000 + $minor * 1000 + $patch
Write-Host "versionCode: $versionCode (from v$Version)" -ForegroundColor DarkGray

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "XBVault.Android"
$dist = Join-Path $root $OutputDir
$rid = "android-$Arch"
$zipName = "XBVault-v$Version-$rid.zip"
$zipPath = Join-Path $dist $zipName
$publishDir = Join-Path $dist "publish-android"

# Set JAVA_HOME: prefer scoop temurin21-jdk, fallback to ANDROID_HOME/jdk-21
$scoopJdk = "$env:USERPROFILE\scoop\apps\temurin21-jdk\current"
if (Test-Path $scoopJdk) {
    $env:JAVA_HOME = $scoopJdk
    Write-Host "Set JAVA_HOME to scoop temurin21-jdk" -ForegroundColor DarkGray
} elseif (-not $env:JAVA_HOME -or -not (Test-Path $env:JAVA_HOME)) {
    $androidSdk = "$env:LOCALAPPDATA\Android\Sdk"
    $jdkPath = "$androidSdk\jdk-21"
    if (Test-Path $jdkPath) {
        $env:JAVA_HOME = $jdkPath
        Write-Host "Set JAVA_HOME to $jdkPath" -ForegroundColor DarkGray
    }
}

Write-Host "Building XBVault v$Version for $rid..." -ForegroundColor Green

# Prefer dotnet on PATH, fallback to default install path
$dotnet = (Get-Command "dotnet" -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrEmpty($dotnet)) { $dotnet = "C:\Program Files\dotnet\dotnet.exe" }

# Release signing when ANDROID_KEYSTORE_* env vars are set (CI); debug key otherwise
$signArgs = @()
if ($env:ANDROID_KEYSTORE_BASE64 -and $env:ANDROID_KEYSTORE_PASS -and $env:ANDROID_KEY_ALIAS -and $env:ANDROID_KEY_PASS) {
    $keystorePath = Join-Path $dist "xbvault-release.keystore"
    [IO.File]::WriteAllBytes($keystorePath, [Convert]::FromBase64String($env:ANDROID_KEYSTORE_BASE64))
    $signArgs = @(
        "-p:AndroidKeyStore=true",
        "-p:AndroidSigningKeyStore=$keystorePath",
        "-p:AndroidSigningKeyAlias=$($env:ANDROID_KEY_ALIAS)",
        "-p:AndroidSigningKeyPass=$($env:ANDROID_KEY_PASS)",
        "-p:AndroidSigningStorePass=$($env:ANDROID_KEYSTORE_PASS)",
        "-p:AndroidPackageFormat=apk"
    )
    Write-Host "Release signing enabled (alias $($env:ANDROID_KEY_ALIAS))" -ForegroundColor DarkGray
} else {
    Write-Host "No signing env vars found - APK will use debug key" -ForegroundColor DarkGray
}

# Publish
& $dotnet clean $project -c Release -r $rid

& $dotnet publish $project `
    -c Release `
    -r $rid `
    --self-contained true `
    @signArgs `
    -p:Version=$Version `
    -p:ApplicationVersion=$versionCode `
    -p:ApplicationDisplayVersion=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Copy standalone APK to dist/ (output name follows ApplicationId; pick any signed APK)
$apkName = "XBVault-v$Version-$rid.apk"
$apkPath = Join-Path $dist $apkName
$apkSource = (Get-ChildItem $publishDir -Filter "*-Signed.apk" | Select-Object -First 1).FullName

if ($apkSource -and (Test-Path $apkSource)) {
    Copy-Item $apkSource $apkPath -Force
    Write-Host "Standalone APK: $apkPath" -ForegroundColor Green
} else {
    Write-Warning "APK not found at $apkSource"
}

# Zip
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zipPath)

Write-Host "Release created: $zipPath" -ForegroundColor Green

# Cleanup
Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
if ($keystorePath -and (Test-Path $keystorePath)) { Remove-Item $keystorePath -Force }

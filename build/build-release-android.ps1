param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Arch = "arm64",
    [string]$OutputDir = "dist"
)

# Strip leading v prefix if present
$Version = $Version -replace '^v', ''

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

# Publish
& $dotnet publish $project `
    -c Release `
    -r $rid `
    --self-contained false `
    -p:Version=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
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

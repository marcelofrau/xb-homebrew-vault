param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Arch = "x64",
    [string]$Project = "XBVault",
    [string]$OutputDir = "dist",
    [switch]$Installer
)

$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root $Project
$dist = Join-Path $root $OutputDir
$rid = "win-$Arch"
$zipName = "XBVault-v$Version-$rid.zip"
$zipPath = Join-Path $dist $zipName
$publishDir = Join-Path $dist "publish"

if (-not (Test-Path $proj)) {
    Write-Error "Project not found: $proj"
    exit 1
}

# Strip leading v prefix if present
$Version = $Version -replace '^v', ''

Write-Host "Building XBVault v$Version for $rid..." -ForegroundColor Green

# Prefer dotnet on PATH, fallback to default install path
$dotnet = (Get-Command "dotnet" -ErrorAction SilentlyContinue)?.Source
if (-not $dotnet) { $dotnet = "C:\Program Files\dotnet\dotnet.exe" }

# Publish (skip PublishReadyToRun for arm64 cross-compile)
$r2r = if ($Arch -eq "arm64") { "false" } else { "true" }
& $dotnet publish $proj `
    -c Release `
    -r $rid `
    --self-contained true `
    -p:PublishReadyToRun=$r2r `
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

# Installer (Inno Setup)
if ($Installer) {
    $issPath = Join-Path $root "installer\XBVault.iss"
    if (-not (Test-Path $issPath)) {
        Write-Error "Inno Setup script not found: $issPath"
        exit 1
    }

    $iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if (-not $iscc) {
        # Try common paths
        $paths = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
            "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
            "${env:ProgramFiles}\Inno Setup 5\ISCC.exe"
        )
        foreach ($p in $paths) {
            if (Test-Path $p) { $iscc = $p; break }
        }
    }

    if (-not $iscc) {
        Write-Error "Inno Setup (ISCC.exe) not found. Install Inno Setup or add it to PATH."
        exit 1
    }

    Write-Host "Building installer..." -ForegroundColor Green
    & $iscc "/dMyAppVersion=$Version" $issPath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Installer build failed"
        exit 1
    }
}

# Cleanup publish dir
Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue

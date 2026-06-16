param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Arch = "x64",
    [string]$Project = "XBVault",
    [string]$OutputDir = "dist"
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

Write-Host "Building XBVault v$Version for $rid..." -ForegroundColor Green

$dotnet = "C:\Program Files\dotnet\dotnet.exe"

# Publish
& $dotnet publish $proj `
    -c Release `
    -r $rid `
    --self-contained true `
    -p:PublishReadyToRun=true `
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

# Cleanup publish dir
Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue

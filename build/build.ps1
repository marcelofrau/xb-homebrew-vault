param(
    [string]$Project = "XBVault"
)

$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root $Project

if (-not (Test-Path $proj)) {
    Write-Error "Project not found: $proj"
    exit 1
}

$csproj = Join-Path $proj "$Project.csproj"

if (-not (Test-Path $csproj)) {
    Write-Error "Project file not found: $csproj"
    exit 1
}

Write-Host "Building $Project..." -ForegroundColor Green
& "C:\Program Files\dotnet\dotnet.exe" build $csproj

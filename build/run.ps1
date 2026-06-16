param(
    [string]$Project = "XBVault"
)

$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root $Project

if (-not (Test-Path $proj)) {
    Write-Error "Project not found: $proj"
    exit 1
}

Write-Host "Running $Project..." -ForegroundColor Green
& "C:\Program Files\dotnet\dotnet.exe" run --project $proj

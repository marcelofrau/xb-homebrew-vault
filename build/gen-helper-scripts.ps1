param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

if (-not (Test-Path $PublishDir)) {
    Write-Error "Publish directory not found: $PublishDir"
    exit 1
}

$exeName = "XBVault.Desktop.exe"

function New-Script {
    param($Name, $Body)
    $path = Join-Path $PublishDir $Name
    $Body | Out-File -FilePath $path -Encoding ASCII -Force
    Write-Host "  Generated: $path" -ForegroundColor Green
}

New-Script "xbv-reset-data.cmd" @"
@echo off
"%~dp0$exeName" --reset-data
pause
"@

New-Script "xbv-console.cmd" @"
@echo off
"%~dp0$exeName" --console
pause
"@

New-Script "xbv-check.cmd" @"
@echo off
"%~dp0$exeName" --check
pause
"@

New-Script "xbv-run.cmd" @"
@echo off
"%~dp0$exeName"
pause
"@

Write-Host "Helper scripts generated in $PublishDir" -ForegroundColor Cyan

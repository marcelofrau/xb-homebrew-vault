param(
    [string]$Repo = "marcelofrau/xb-homebrew-vault",
    [string]$OutDir = (Get-Location).Path
)

$ErrorActionPreference = "Stop"
$releases = gh release list --repo $Repo --json tagName,name | ConvertFrom-Json

foreach ($rel in $releases) {
    Write-Host "[$($rel.tagName)] Downloading..." -ForegroundColor Cyan
    $assets = gh release view $rel.tagName --repo $Repo --json assets | ConvertFrom-Json | Select-Object -ExpandProperty assets
    if (-not $assets) { Write-Host "  No assets"; continue }

    foreach ($asset in $assets) {
        $dlUrl = $asset.browser_download_url
        $fileName = $asset.name
        $outPath = Join-Path $OutDir $fileName
        if (Test-Path $outPath) { Write-Host "  Already exists: $fileName"; continue }

        aria2c -x 4 -s 4 --continue=true --console-log-level=error -d $OutDir $dlUrl 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  Downloaded: $fileName" -ForegroundColor Green
        } else {
            Write-Host "  Failed: $fileName (download via browser?)" -ForegroundColor Yellow
        }
    }
}

Write-Host "Done. Files in: $OutDir" -ForegroundColor Cyan

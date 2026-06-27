param(
    [Parameter(Mandatory = $false)]
    [string]$Repo = "marcelofrau/xb-homebrew-vault",

    [Parameter(Mandatory = $true)]
    [string]$ApiKey
)

$ErrorActionPreference = "Stop"

# Verify gh
$ghPath = Get-Command gh -ErrorAction SilentlyContinue
if (-not $ghPath) {
    Write-Error "gh CLI not found. Install: winget install GitHub.cli"
    exit 1
}

gh auth status 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error "gh not authenticated. Run: gh auth login"
    exit 1
}

$workDir = Join-Path $env:TEMP "xbvault-vt-backfill"
Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $workDir -Force | Out-Null

Write-Host "Fetching releases..." -ForegroundColor Cyan
$releases = gh release list --repo $Repo --json tagName,isLatest --limit 100 | ConvertFrom-Json
[Array]::Reverse($releases)

$requestCount = 0
$total = $releases.Count
$done = 0

foreach ($release in $releases) {
    $tag = $release.tagName
    $done++
    Write-Host "[$done/$total] Processing $tag..." -ForegroundColor Yellow

    $info = gh release view $tag --repo $Repo --json body,assets 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Skip (no data)" -ForegroundColor DarkGray
        continue
    }
    $infoObj = $info | ConvertFrom-Json

    $assets = $infoObj.assets
    if (-not $assets -or $assets.Count -eq 0) {
        Write-Host "  Skip (no assets)" -ForegroundColor DarkGray
        continue
    }

    $relDir = Join-Path $workDir $tag
    New-Item -ItemType Directory -Path $relDir -Force | Out-Null
    gh release download $tag --repo $Repo --dir $relDir 2>&1 | Out-Null

    $checksums = [System.Text.StringBuilder]::new()
    $vtLines = [System.Text.StringBuilder]::new()

    foreach ($file in (Get-ChildItem -LiteralPath $relDir -File)) {
        $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLower()
        $name = $file.Name

        [void]$checksums.AppendLine("$hash  $name")

        $requestCount++
        if ($requestCount % 4 -eq 0) {
            Write-Host "  Rate limit pause (60s)..." -ForegroundColor DarkGray
            Start-Sleep -Seconds 62
        }

        try {
            $vtResp = Invoke-RestMethod -Uri "https://www.virustotal.com/api/v3/files/$hash" `
                -Headers @{"x-apikey" = $ApiKey} `
                -ContentType "application/json" `
                -ErrorAction Stop

            $stats = $vtResp.data.attributes.last_analysis_stats
            $malicious = $stats.malicious
            $totalEngines = $malicious + $stats.suspicious + $stats.undetected + $stats.harmless
            $url = "https://www.virustotal.com/gui/file/$hash/detection"

            if ($malicious -gt 0) {
                [void]$vtLines.AppendLine("- **$name** - View Scan <$url> | $malicious/$totalEngines engines flagged")
                Write-Host "  $name - $malicious/$totalEngines flagged" -ForegroundColor Red
            } else {
                [void]$vtLines.AppendLine("- **$name** - View Scan <$url> | 0/$totalEngines engines detected")
                Write-Host "  $name - 0/$totalEngines" -ForegroundColor Green
            }
        }
        catch {
            [void]$vtLines.AppendLine("- **$name** - SHA256: $hash (upload manually at https://virustotal.com)")
            Write-Host "  $name - not in VT database" -ForegroundColor DarkYellow
        }
    }

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm"
    $securitySection = @"

---

[Security Verification]

SHA256 Checksums:
```
$($checksums.ToString().TrimEnd())
```

VirusTotal Results:
$($vtLines.ToString().TrimEnd())

Verified $timestamp UTC
"@

    $currentBody = $infoObj.body
    $idx = $currentBody.IndexOf("`n[Security Verification]")
    if ($idx -ge 0) {
        $currentBody = $currentBody.Substring(0, $idx).TrimEnd()
    }

    $newBody = $currentBody + $securitySection
    $bodyFile = Join-Path $workDir "body.md"
    $newBody | Set-Content -LiteralPath $bodyFile -Encoding UTF8

    gh release edit $tag --repo $Repo --notes-file $bodyFile 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Release updated!" -ForegroundColor Green
    } else {
        Write-Host "  Failed to update release" -ForegroundColor Red
    }

    Remove-Item -LiteralPath $relDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Done. $done releases processed." -ForegroundColor Cyan

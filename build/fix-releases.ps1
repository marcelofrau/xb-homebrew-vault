param(
    [string]$Repo = "marcelofrau/xb-homebrew-vault"
)

$ErrorActionPreference = "Stop"

$cp437 = [System.Text.Encoding]::GetEncoding(437)
$utf8  = [System.Text.Encoding]::UTF8
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

function Fix-Mojibake($s) {
    if (-not $s -or $s.Length -lt 3) { return $s }
    $bytes = $cp437.GetBytes($s)
    return $utf8.GetString($bytes)
}

function Get-Changelog($tag, $prevTag) {
    if (-not $prevTag) {
        $log = git log --oneline --no-merges -10 $tag 2>$null
    } else {
        $log = git log --oneline --no-merges "$prevTag..$tag" 2>$null
    }
    if (-not $log) { return "  No changelog entries found." }
    $lines = $log -split "`n" | ForEach-Object {
        $_ -replace '^[0-9a-f]+\s+', '- '
    }
    return $lines -join "`n"
}

# Get all release tags sorted
$allTags = gh api "repos/$Repo/git/refs/tags" --jq '.[].ref' | ForEach-Object { $_ -replace 'refs/tags/', '' }
$versionTags = $allTags | Where-Object { $_ -match '^v\d+\.\d+\.\d+$' } | Sort-Object -Descending { [Version]($_.Substring(1)) }

$releases = gh release list --repo $Repo --json tagName | ConvertFrom-Json
[Array]::Reverse($releases)

$done = 0
$total = $releases.Count

cd (Split-Path $PSScriptRoot -Parent)  # repo root for git

foreach ($release in $releases) {
    $tag = $release.tagName
    $done++
    Write-Host "[$done/$total] $tag..." -ForegroundColor Yellow

    $info = gh release view $tag --repo $Repo --json body,assets 2>&1
    if ($LASTEXITCODE -ne 0) { Write-Host "  Skip"; continue }
    $infoObj = $info | ConvertFrom-Json
    if (-not $infoObj.assets -or $infoObj.assets.Count -eq 0) { Write-Host "  No assets"; continue }

    $releaseId = gh api "repos/$Repo/releases/tags/$tag" --jq '.id'

    # Current body (corrupted)
    $currentBody = if ($infoObj.body) { $infoObj.body } else { "" }

    # Strip old security section if exists
    $idx = $currentBody.IndexOf("`n[Security Verification]")
    if ($idx -ge 0) {
        $currentBody = $currentBody.Substring(0, $idx).TrimEnd()
    }

    # Fix mojibake
    Write-Host "  Fixing mojibake..." -ForegroundColor Gray
    $fixedBody = Fix-Mojibake $currentBody

    # If body is too short (<50 chars), likely nuked by debug — regenerate from git
    if ($fixedBody.Length -lt 50) {
        Write-Host "  Body too short, regenerating from git..." -ForegroundColor DarkYellow
        $tagIdx = [array]::IndexOf($versionTags, $tag)
        $prevTag = if ($tagIdx -ge 0 -and $tagIdx -lt $versionTags.Length - 1) { $versionTags[$tagIdx + 1] } else { $null }
        $changelog = Get-Changelog $tag $prevTag
        $fixedBody = "## $tag`n`n### What's New`n`n$changelog"
    }

    # Download + SHA256
    $tempDir = Join-Path $env:TEMP "xbvault-fix-$tag"
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    gh release download $tag --repo $Repo --dir $tempDir --clobber 2>&1 | Out-Null

    $checksums = [System.Text.StringBuilder]::new()
    $vtLines = [System.Text.StringBuilder]::new()
    foreach ($file in (Get-ChildItem -LiteralPath $tempDir -File)) {
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLower()
        $name = $file.Name
        [void]$checksums.AppendLine("$hash  $name")
        $permalink = "https://www.virustotal.com/gui/file/$hash/detection"
        [void]$vtLines.AppendLine("- **$name** - [View Scan]($permalink) | SHA256: $hash")
    }

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm"
    $securitySection = @"

[Security Verification]

SHA256 Checksums:
```
$($checksums.ToString().TrimEnd())
```

VirusTotal Results:
$($vtLines.ToString().TrimEnd())

Verified $timestamp UTC
"@

    $newBody = $fixedBody + $securitySection

    # Write JSON to file (UTF8 no BOM) - reliable JSON encoding
    $bodyObj = @{ body = $newBody }
    $jsonStr = $bodyObj | ConvertTo-Json -Depth 10
    $jsonFile = Join-Path $env:TEMP "xbvault-payload-$tag.json"
    [System.IO.File]::WriteAllText($jsonFile, $jsonStr, $utf8NoBom)

    # PATCH via gh api with file input
    $result = gh api "repos/$Repo/releases/$releaseId" --method PATCH --input $jsonFile 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Updated!" -ForegroundColor Green
    } else {
        Write-Host "  Failed: $result" -ForegroundColor Red
    }

    Remove-Item -LiteralPath $jsonFile -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Done. $done releases processed." -ForegroundColor Cyan

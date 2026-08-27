#requires -Version 7.4
<#
.SYNOPSIS
Analyze catalog-sweep results against the real manifest identities to decide
which catalog entries need a global override and which match reliably already.

Reads H:\xb-vault-temp\catalog-sweep\sweep-results.json
Writes H:\xb-vault-temp\catalog-sweep\match-analysis.txt  (human readable)
Writes H:\xb-vault-temp\catalog-sweep\needs-override.json (the actionable list)
#>
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression

$OutDir = "H:\xb-vault-temp\catalog-sweep"
$results = Get-Content -LiteralPath (Join-Path $OutDir "sweep-results.json") -Raw | ConvertFrom-Json

function Norm([string]$s) {
    if ([string]::IsNullOrEmpty($s)) { return "" }
    $s -replace "[^A-Za-z0-9]", ""
}
function StripSuffixes([string]$s) {
    if ([string]::IsNullOrEmpty($s)) { return $s }
    $t = $s
    foreach ($suf in @("UWP", "Uwp", "uwp", "Frontend", "frontend", "PC", "Emulator", "Launcher", "UWP Launcher")) {
        if ($t.EndsWith($suf) -and $t.Length -gt $suf.Length) { $t = $t.Substring(0, $t.Length - $suf.Length) }
    }
    return $t
}

$lines = [System.Collections.Generic.List[string]]::new()
$override = [System.Collections.Generic.List[object]]::new()

$needCount = 0
$okCount = 0
foreach ($r in $results) {
    if ($r.status -ne "ok") { continue }
    $catName = $r.title          # catalog.Name (matcher uses catalog.Name = api.Title)
    $catId   = $r.catalogId
    $mn = [string]$r.manifestName
    $dn = [string]$r.displayName

    $catNorm = Norm $catName
    $mnNorm  = Norm $mn
    $dnNorm  = Norm $dn
    $mnStrip = Norm (StripSuffixes $mn)
    $dnStrip = Norm (StripSuffixes $dn)

    $matched = $false
    $reason = ""

    # E0 exact
    if ($catName -eq $mn -or $catName -eq $dn) { $matched = $true; $reason = "E0 exact" }
    # E1 normalized exact
    elseif ($catNorm -and ($catNorm -eq $mnNorm -or $catNorm -eq $dnNorm)) { $matched = $true; $reason = "E1 norm" }
    # E1.2 suffix strip
    elseif ($mnStrip -and $catNorm -eq $mnStrip) { $matched = $true; $reason = "E1.2 suffix(name)" }
    elseif ($dnStrip -and $catNorm -eq $dnStrip) { $matched = $true; $reason = "E1.2 suffix(display)" }
    # E1.1 safe prefix: candidate startsWith catalog (both >=6)
    elseif ($catNorm.Length -ge 6 -and $mnNorm.Length -ge 6 -and $mnNorm.StartsWith($catNorm)) { $matched = $true; $reason = "E1.1 prefix(name)" }
    elseif ($catNorm.Length -ge 6 -and $dnNorm.Length -ge 6 -and ($dnNorm.StartsWith($catNorm) -or $dnNorm.Contains($catNorm))) { $matched = $true; $reason = "E1.1 prefix/contains(display)" }

    $tag = "OK"
    if (-not $matched) {
        $tag = "NEED"
        $needCount++
        $override.Add([pscustomobject]@{ catalogId = $catId; title = $catName; manifestName = $mn; displayName = $dn })
    } else { $okCount++ }

    $lines.Add(( "{0,-5} {1,-28} | cat='{2}'  name='{3}'  display='{4}'  ({5})" -f $tag, $catId, $catName, $mn, $dn, $reason ))
}

$lines | Set-Content -LiteralPath (Join-Path $OutDir "match-analysis.txt") -Encoding utf8
$override | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $OutDir "needs-override.json") -Encoding utf8

Write-Host "OK: $okCount   NEED-override: $needCount"
Write-Host "Analysis:  $OutDir\match-analysis.txt"
Write-Host "Overrides: $OutDir\needs-override.json"

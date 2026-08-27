#requires -Version 7.4
<#
.SYNOPSIS
Generate XBVault/Assets/package-overrides.json from the catalog-sweep results.
Maps manifestName (the real package identity <Name> that XDP reports as
pkg.Name and as the stripped PackageFamilyName) -> catalogId for every catalog
entry whose title does NOT reliably match its installed identity.

Blind spot in the matching heuristics is filled with an exact per-package
override, so installed<->catalog matching stops guessing.

Preserves the existing image-only PFN entries and versionOverrides.
#>
$ErrorActionPreference = "Stop"

$OutDir = "H:\xb-vault-temp\catalog-sweep"
$results = Get-Content -LiteralPath (Join-Path $OutDir "sweep-results.json") -Raw | ConvertFrom-Json
$needs = Get-Content -LiteralPath (Join-Path $OutDir "needs-override.json") -Raw | ConvertFrom-Json
$needIds = @{} 
foreach ($n in $needs) { $needIds[$n.catalogId] = $n.manifestName }

# Build the packageFamilyNameOverrides list.
# Every entry uses manifestName (the real <Name>) as the key.
$pfnOverrides = [System.Collections.Generic.List[object]]::new()

# --- 1) The 32 catalog entries whose title does not match their identity ---
foreach ($need in $needs) {
    $pfnOverrides.Add([pscustomobject]@{
        packageFamilyName = $need.manifestName
        catalogId         = $need.catalogId
    })
}

# --- 1b) Preserve previously-existing exact PFN->catalogId overrides whose
#          manifest identity the sweep CONFIRMED. The heuristic may already match
#          these, but a verified exact override is strictly safer than relying on
#          normalized-prefix heuristics.
$confirmedLegacy = @(
    @{ name = "SuperMarioBrosRemastered"; cat = "super-mario-bros-remastered" }
    @{ name = "Doom64EXClassicUWP";        cat = "doom64ex-classic" }
    @{ name = "uhexen2.UWP";               cat = "uhexen2" }
    @{ name = "SMWRP";                     cat = "super-mario-world-remastered-plus" }
)
$have = @{}
foreach ($p in $pfnOverrides) { if ($p.catalogId) { $have[$p.packageFamilyName.ToLower()] = $true } }
foreach ($legacy in $confirmedLegacy) {
    if (-not $have.ContainsKey($legacy.name.ToLower())) {
        $pfnOverrides.Add([pscustomobject]@{ packageFamilyName = $legacy.name; catalogId = $legacy.cat })
    }
}

# --- 2) Preserve existing image-only PFN entries (no catalogId) ---
$preserveImages = @(
    "57bcfd1f-31c1-4f8e-bf91-958732a81506",
    "20468Jellyfin.Jellyfin",
    "Revive.SpaceCadetPinballUWP"
)
$imageUrls = @{
    "57bcfd1f-31c1-4f8e-bf91-958732a81506" = "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Fwww.gamingonlinux.com%2Fuploads%2Farticles%2Ftagline_images%2F306770739id25264gol.jpg&f=1&nofb=1&ipt=f4f01ecffa4598684d4d7c0b2f65dfb0a96ac78e601e28e9c227100baa4883c4"
    "20468Jellyfin.Jellyfin"                = "https://m.media-amazon.com/images/I/51i0m01RSxL.png"
    "Revive.SpaceCadetPinballUWP"           = "https://emulationrevival.github.io/xbox-dev-mode/images/game-ports/spacecadetpinball.webp"
}
foreach ($pf in $preserveImages) {
    $pfnOverrides.Add([pscustomobject]@{
        packageFamilyName = $pf
        imageUrl          = $imageUrls[$pf]
    })
}

# --- packageNameOverrides ---
# All previous packageNameOverrides were display-name-keyed and unreliable
# (XDP pkg.Name == manifest <Name>, never the display name). They are superseded
# by the manifestName-keyed packageFamilyName entries above, so this list is now
# empty and kept for symmetry/future name-based mappings.
$nameOverrides = [System.Collections.Generic.List[object]]::new()

# --- versionOverrides (unchanged) ---
$versionOverrides = @(
    [pscustomobject]@{ catalogId = "safeexit";           catalogVersion = "1.0.0.1"; packageVersion = "1.0.0.0" }
    [pscustomobject]@{ catalogId = "sonic-2-sms-remake"; catalogVersion = "2.9.2";   packageVersion = "2.9.0.2" }
)

$result = [ordered]@{
    packageFamilyNameOverrides = $pfnOverrides
    packageNameOverrides       = $nameOverrides
    versionOverrides           = $versionOverrides
}

$json = $result | ConvertTo-Json -Depth 5
$target = "F:\workspace\xb-homebrew-vault\XBVault\Assets\package-overrides.json"
Set-Content -LiteralPath $target -Value $json -Encoding utf8
Write-Host "Wrote $target"
Write-Host "  packageFamilyNameOverrides: $($pfnOverrides.Count) entries"
Write-Host "  (incl $($needs.Count) manifestName->catalogId from sweep)"

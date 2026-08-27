#requires -Version 7.4
<#
.SYNOPSIS
Sweep the Emulation Revival catalog: downloads every installable package and
extracts the REAL package identity (Name, DisplayName, Version, Publisher) from
the embedded AppxManifest.xml. Outputs a JSON table used to build a mature
global override file.

.USAGE
pwsh -File build/catalog-sweep.ps1
  Downloads the live catalog, fetches every package (~large, use H:\xb-vault-temp),
  extracts manifests, writes H:\xb-vault-temp\catalog-sweep\sweep-results.json

pwsh -File build/catalog-sweep.ps1 -CatalogPath <catalog.json> -SkipDownload
  Re-analyze an existing sweep directory from cached downloads (no re-download).

.PARAMETER CatalogPath
  Local catalog.json to use instead of fetching https://emulationrevival.github.io/api/catalog.json

.PARAMETER OutDir
  Root working dir. Defaults to H:\xb-vault-temp\catalog-sweep (prefers H: for space).

.PARAMETER SkipDownload
  Do not download; only re-extract manifests from already-downloaded files.

.PARAMETER KeepPackages
  Keep downloaded package files on disk (default deletes them after manifest extraction
  to save space; only the manifest + out.json are preserved).
#>
param(
    [string]$CatalogPath = "",
    [string]$OutDir = "",
    [switch]$SkipDownload,
    [switch]$KeepPackages
)

$ErrorActionPreference = "Stop"

if (-not $OutDir) {
    $tempRoot = if (Test-Path -LiteralPath "H:\xb-vault-temp") { "H:\xb-vault-temp" } else {
        Join-Path ([System.IO.Path]::GetTempPath()) "xb-vault-temp"
    }
    $OutDir = Join-Path $tempRoot "catalog-sweep"
}
$downloadsDir = Join-Path $OutDir "packages"
$resultsPath = Join-Path $OutDir "sweep-results.json"
$catalogUrl = "https://emulationrevival.github.io/api/catalog.json"

New-Item -ItemType Directory -Path $downloadsDir -Force | Out-Null

# ---------- Load catalog ----------
if ($CatalogPath -and (Test-Path -LiteralPath $CatalogPath)) {
    Write-Host "Using local catalog: $CatalogPath" -ForegroundColor DarkGray
    $catalog = Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json
}
else {
    Write-Host "Fetching catalog: $catalogUrl" -ForegroundColor DarkGray
    $catalog = Invoke-RestMethod -Uri $catalogUrl -Headers @{ "User-Agent" = "XB Homebrew Vault" }
}
$items = @($catalog.items)
Write-Host "Catalog has $($items.Count) items" -ForegroundColor Cyan

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$results = [System.Collections.Generic.List[object]]::new()

# ---------- Helpers ----------
function Get-EntryName($archive, $suffix) {
    foreach ($e in $archive.Entries) {
        if ($e.Name -like $suffix) { return $e.FullName }
    }
    return $null
}

function Read-ManifestFromZipArchive($zipArchive, [string]$packageId) {
    # Returns a [xml] AppxManifest.xml or $null
    $manifests = @($zipArchive.Entries | Where-Object { $_.FullName -eq "AppxManifest.xml" })
    if ($manifests.Count -gt 0) {
        $entry = $manifests[0]
        try {
            $reader = New-Object System.IO.StreamReader($entry.Open())
            try {
                $xmlText = $reader.ReadToEnd()
                $doc = New-Object System.Xml.XmlDocument
                $doc.LoadXml($xmlText)
                return $doc
            }
            finally { $reader.Dispose() }
        }
        catch { Write-Verbose "[$packageId] AppxManifest parse error: $($_.Exception.Message)" }
    }
    return $null
}

function Read-ManifestFromBundleStream([System.IO.Stream]$stream, [string]$packageId) {
    # A bundle (.msixbundle/.appxbundle) contains inner .msix/.appx entries.
    # Open the bundle as a zip, find an x64 inner package, read ITS manifest.
    try {
        $bundle = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Read)
        try {
            # Prefer an x64/neutral inner package over arm
            $inner = $null
            foreach ($cand in @($bundle.Entries | Where-Object { $_.Name -like "*.msix" -or $_.Name -like "*.appx" })) {
                if ($inner -eq $null) { $inner = $cand }
                if ($cand.Name -match "x64|-x64|_x64|neutral" -or $cand.FullName -match "x64|-x64") {
                    $inner = $cand
                    break
                }
            }
            if ($inner -eq $null) { return $null }

            $innerStream = $inner.Open()
            try {
                $innerZip = New-Object System.IO.Compression.ZipArchive($innerStream, [System.IO.Compression.ZipArchiveMode]::Read)
                try { return Read-ManifestFromZipArchive $innerZip $packageId }
                finally { $innerZip.Dispose() }
            }
            finally { $innerStream.Dispose() }
        }
        finally { $bundle.Dispose() }
    }
    catch { Write-Verbose "[$packageId] bundle error: $($_.Exception.Message)" }
    return $null
}

function Read-ManifestFromZipFile([string]$packageFile, [string]$packageId) {
    # .zip may be:
    #   a) a loose-files package with AppxManifest.xml at root (extracted-install style)
    #   b) an outer zip containing an inner .msix/.appx/.msixbundle
    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($packageFile)
        try {
            # (a) direct manifest at root
            $doc = Read-ManifestFromZipArchive $zip $packageId
            if ($doc) { return $doc }

            # (b) inner single package
            # Skip Microsoft.* dependency packages and anything under a Dependencies/
            # folder — those are deps, not the main app (e.g. safeexit zip).
            $innerEntry = $null
            $candidates = @($zip.Entries | Where-Object {
                $_.Name -like "*.msix" -or $_.Name -like "*.appx" -or
                $_.Name -like "*.msixbundle" -or $_.Name -like "*.appxbundle"
            })
            foreach ($cand in $candidates) {
                $isDep = $cand.FullName -match "Dependencies|Dependency" -or
                          $cand.Name -like "Microsoft.*" -or $cand.Name -like "Microsoft.NET.*"
                if ($isDep) { continue }
                if ($innerEntry -eq $null) { $innerEntry = $cand }
                if ($cand.FullName -match "x64|-x64|_x64|neutral|main|app") { $innerEntry = $cand }
            }
            # Fallback: no non-dependency candidate -> take first dep (better than nothing)
            if ($innerEntry -eq $null -and $candidates.Count -gt 0) { $innerEntry = $candidates[0] }
            if ($innerEntry -ne $null) {
                $innerStream = $innerEntry.Open()
                try {
                    if ($innerEntry.Name -like "*.msixbundle" -or $innerEntry.Name -like "*.appxbundle") {
                        return Read-ManifestFromBundleStream $innerStream $packageId
                    }
                    $innerZip = New-Object System.IO.Compression.ZipArchive($innerStream, [System.IO.Compression.ZipArchiveMode]::Read)
                    try { return Read-ManifestFromZipArchive $innerZip $packageId }
                    finally { $innerZip.Dispose() }
                }
                finally { $innerStream.Dispose() }
            }

            # (c) manifest nested deeper (walk all AppxManifest.xml entries)
            $nested = @($zip.Entries | Where-Object { $_.Name -eq "AppxManifest.xml" })
            if ($nested.Count -gt 0) {
                $entry = $nested | Sort-Object { $_.FullName.Length } | Select-Object -First 1
                try {
                    $reader = New-Object System.IO.StreamReader($entry.Open())
                    try {
                        $doc = New-Object System.Xml.XmlDocument
                        $doc.LoadXml($reader.ReadToEnd())
                        return $doc
                    }
                    finally { $reader.Dispose() }
                }
                catch { }
            }
        }
        finally { $zip.Dispose() }
    }
    catch { Write-Verbose "[$packageId] zip error: $($_.Exception.Message)" }
    return $null
}

# ---------- Per-item processing ----------
$idx = 0
foreach ($item in $items) {
    $idx++
    $id = $item.id
    $url = $item.downloadUrl
    $isWindowsTool = $false
    if (-not $url) {
        # fallback: check downloads[]
        foreach ($d in @($item.downloads)) {
            if ($d.url -and $d.url -match "\.(msix|msixbundle|appx|appxbundle|zip)(\?|$)") {
                $url = $d.url; break
            }
            if ($d.url -and $d.url -match "\.exe(\?|$)") { $url = $d.url; $isWindowsTool = $true; break }
        }
    }
    if (-not $url) {
        $results.Add([pscustomobject]@{
            catalogId = $id; title = $item.title; category = $item.category
            downloadUrl = ""; extension = ""; status = "no-download-url"
            manifestName = ""; displayName = ""; version = ""; publisher = ""; pfn = ""
        })
        continue
    }
    if ($url -match "\.exe(\?|$)") { $isWindowsTool = $true }

    if ($isWindowsTool) {
        Write-Host "[$idx/$($items.Count)] $id : WINDOWS TOOL (.exe) - skip" -ForegroundColor DarkYellow
        $results.Add([pscustomobject]@{
            catalogId = $id; title = $item.title; category = $item.category
            downloadUrl = $url; extension = "exe"; status = "windows-tool"
            manifestName = ""; displayName = ""; version = ""; publisher = ""; pfn = ""
        })
        continue
    }

    $extension = ""
    if ($url -match "\.msixbundle(\?|$)") { $extension = "msixbundle" }
    elseif ($url -match "\.appxbundle(\?|$)") { $extension = "appxbundle" }
    elseif ($url -match "\.msix(\?|$)") { $extension = "msix" }
    elseif ($url -match "\.appx(\?|$)") { $extension = "appx" }
    elseif ($url -match "\.zip(\?|$)") { $extension = "zip" }

    $manifest = $null
    $pkgFile = ""

    if ($extension -eq "zip") {
        # the download itself is a zip
        $pkgFile = Join-Path $downloadsDir "$id.$extension"
        if (Test-Path -LiteralPath $pkgFile) {
            Write-Host "[$idx/$($items.Count)] $id : cached zip" -ForegroundColor DarkGray
        }
        elseif ($SkipDownload) {
            Write-Host "[$idx/$($items.Count)] $id : SKIP (no cache)" -ForegroundColor DarkGray
            $status = "no-cache"
        }
        else {
            Write-Host "[$idx/$($items.Count)] $id : downloading $extension..." -ForegroundColor DarkGray
            try {
                Invoke-WebRequest -Uri $url -OutFile $pkgFile -Headers @{ "User-Agent" = "XB Homebrew Vault" }
            }
            catch {
                Write-Host "[$idx/$($items.Count)] $id : DOWNLOAD FAILED: $($_.Exception.Message)" -ForegroundColor Red
                $results.Add([pscustomobject]@{
                    catalogId = $id; title = $item.title; category = $item.category
                    downloadUrl = $url; extension = $extension; status = "download-failed"
                    manifestName = ""; displayName = ""; version = ""; publisher = ""; pfn = ""
                })
                continue
            }
        }
        if (Test-Path -LiteralPath $pkgFile) {
            $manifest = Read-ManifestFromZipFile $pkgFile $id
        }
    }
    else {
        # single package or bundle
        $pkgFile = Join-Path $downloadsDir "$id.$extension"
        if (Test-Path -LiteralPath $pkgFile) {
            Write-Host "[$idx/$($items.Count)] $id : cached $extension" -ForegroundColor DarkGray
        }
        elseif ($SkipDownload) {
            Write-Host "[$idx/$($items.Count)] $id : SKIP (no cache)" -ForegroundColor DarkGray
            $status = "no-cache"
        }
        else {
            Write-Host "[$idx/$($items.Count)] $id : downloading $extension..." -ForegroundColor DarkGray
            try {
                Invoke-WebRequest -Uri $url -OutFile $pkgFile -Headers @{ "User-Agent" = "XB Homebrew Vault" }
            }
            catch {
                Write-Host "[$idx/$($items.Count)] $id : DOWNLOAD FAILED: $($_.Exception.Message)" -ForegroundColor Red
                $results.Add([pscustomobject]@{
                    catalogId = $id; title = $item.title; category = $item.category
                    downloadUrl = $url; extension = $extension; status = "download-failed"
                    manifestName = ""; displayName = ""; version = ""; publisher = ""; pfn = ""
                })
                continue
            }
        }

        if ($extension -eq "msixbundle" -or $extension -eq "appxbundle") {
            if (Test-Path -LiteralPath $pkgFile) {
                $fs = [System.IO.File]::OpenRead($pkgFile)
                try { $manifest = Read-ManifestFromBundleStream $fs $id }
                finally { $fs.Dispose() }
            }
        }
        else {
            if (Test-Path -LiteralPath $pkgFile) {
                $zip = [System.IO.Compression.ZipFile]::OpenRead($pkgFile)
                try { $manifest = Read-ManifestFromZipArchive $zip $id }
                finally { $zip.Dispose() }
            }
        }
    }

    # Cleanup package files unless KeepPackages (save space)
    if (-not $KeepPackages -and $pkgFile -and (Test-Path -LiteralPath $pkgFile)) {
        Remove-Item -LiteralPath $pkgFile -Force
    }

    if ($manifest -eq $null) {
        Write-Host "[$idx/$($items.Count)] $id ($extension) : NO MANIFEST FOUND" -ForegroundColor Yellow
        $results.Add([pscustomobject]@{
            catalogId = $id; title = $item.title; category = $item.category
            downloadUrl = $url; extension = $extension; status = "no-manifest"
            manifestName = ""; displayName = ""; version = ""; publisher = ""; pfn = ""
        })
        continue
    }

    # Parse identity
    $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace("d", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
    $ns.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10")
    $ns.AddNamespace("uap3", "http://schemas.microsoft.com/appx/manifest/uap/windows10/3")

    $identity = $manifest.Package.Identity
    $pkgName = if ($identity) { [string]$identity.Name } else { "" }
    $pkgVersion = if ($identity) { [string]$identity.Version } else { "" }
    $publisher = if ($identity) { [string]$identity.Publisher } else { "" }

    $displayName = ""
    $df = $manifest.SelectSingleNode("//d:Properties/d:DisplayName", $ns)
    if ($df) { $displayName = $df.InnerText }

    # Publisher hash heuristic: last 8 hex of the Publisher CN. Dev-mode packages
    # use the publisher from the signing cert, so we record publisher for reference
    # but the matching key (stripped PFN == Name) does not need the hash.
    $pfn = $pkgName

    Write-Host "[$idx/$($items.Count)] $id ($extension) -> Name='$pkgName' Display='$displayName' v$pkgVersion" -ForegroundColor Green
    $results.Add([pscustomobject]@{
        catalogId = $id; title = $item.title; category = $item.category
        downloadUrl = $url; extension = $extension; status = "ok"
        manifestName = $pkgName; displayName = $displayName; version = $pkgVersion; publisher = $publisher; pfn = $pfn
    })
}

$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $resultsPath -Encoding utf8
Write-Host ""
Write-Host "Done. Results written to: $resultsPath" -ForegroundColor Cyan

# Summary
$ok = @($results | Where-Object { $_.status -eq "ok" }).Count
Write-Host "  ok:           $ok"
Write-Host "  no-manifest:  $(@($results | Where-Object { $_.status -eq 'no-manifest' }).Count)"
Write-Host "  download-fail:$(@($results | Where-Object { $_.status -eq 'download-failed' }).Count)"
Write-Host "  windows-tool: $(@($results | Where-Object { $_.status -eq 'windows-tool' }).Count)"
Write-Host "  no-url:       $(@($results | Where-Object { $_.status -eq 'no-download-url' }).Count)"

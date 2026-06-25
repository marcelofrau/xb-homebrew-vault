param(
    [int]$Port = 4000,
    [string]$BaseUrl = "/xb-homebrew-vault",
    [switch]$NoBaseUrl,
    [switch]$Live  # Simplified: just -Live
)

# Shortcut: -Live implies LiveReload
$LiveReload = $Live

$root = Split-Path -Parent $PSScriptRoot
$docs = Join-Path $root "docs"

if (-not (Test-Path $docs)) {
    Write-Error "Docs directory not found: $docs"
    exit 1
}

$docker = Get-Command "docker.exe" -ErrorAction SilentlyContinue
if (-not $docker) {
    Write-Error "Docker not found in PATH. Install/start Docker and try again."
    exit 1
}

& docker.exe info *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker daemon is not running. Start Rancher Desktop/Docker Desktop and try again."
    exit 1
}

$docsPath = (Resolve-Path $docs).Path
$volume = "$docsPath`:/srv/jekyll"

$args = @(
    "run", "--rm",
    "-p", "$Port`:4000",
    "-v", $volume,
    "jekyll/jekyll:pages"
)

if ($NoBaseUrl) {
    $baseUrlArg = "--baseurl `"`""
    Write-Host "Starting docs at http://localhost:$Port/" -ForegroundColor Green
}
else {
    $baseUrlArg = "--baseurl $BaseUrl"
    Write-Host "Starting docs at http://localhost:$Port$BaseUrl/" -ForegroundColor Green
}

$liveReloadArg = if ($LiveReload) { "--livereload" } else { "" }

if ($LiveReload) {
    Write-Host "LiveReload enabled." -ForegroundColor Green
}

if ([string]::IsNullOrWhiteSpace($liveReloadArg)) {
    $command = "gem list -i webrick > /dev/null || gem install webrick --no-document; jekyll serve --host 0.0.0.0 $baseUrlArg"
}
else {
    $command = "gem list -i webrick > /dev/null || gem install webrick --no-document; jekyll serve --host 0.0.0.0 $baseUrlArg $liveReloadArg"
}

$args += @("sh", "-lc", $command)

& docker.exe @args

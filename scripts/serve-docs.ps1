param(
  [string]$Port = "4000",
  [switch]$Open
)

$docsDir = Join-Path $PSScriptRoot ".." "docs" | Resolve-Path
$siteDir = Join-Path $docsDir "_site"

# Try Docker first (best preview with Jekyll rendering)
$dockerOk = $null -ne (Get-Command docker -ErrorAction SilentlyContinue)
if ($dockerOk) {
  $dockerOk = docker info 2>$null | Select-String "Server Version" -Quiet
}

if ($dockerOk) {
  Write-Host "Starting Jekyll via Docker at http://localhost:$Port ..." -ForegroundColor Green
  Write-Host "Ctrl+C to stop" -ForegroundColor Gray
  docker run --rm -v "${docsDir}:/srv/jekyll" -p "${Port}:4000" jekyll/jekyll:3.8 jekyll serve --port 4000 --watch --force_polling
  return
}

# Fallback: serve static _site/ if it exists
if (Test-Path $siteDir) {
  Write-Host "Serving pre-built site from _site/ ..." -ForegroundColor Green
  Write-Host "To rebuild: cd docs && bundle exec jekyll build (requires Ruby)" -ForegroundColor Gray
  & {
    $server = $null
    if (Get-Command python -ErrorAction SilentlyContinue) {
      python -m http.server $Port -d $siteDir
    } elseif (Get-Command npx -ErrorAction SilentlyContinue) {
      npx serve $siteDir -p $Port
    } else {
      throw "No HTTP server (Python/Node) found"
    }
  }
  return
}

# Nothing works
Write-Host "=== No preview available ===" -ForegroundColor Red
Write-Host "Start Docker Desktop and re-run this script for full Jekyll preview." -ForegroundColor Yellow
Write-Host "Or check deployed site at https://xbvault.pages.dev" -ForegroundColor Yellow

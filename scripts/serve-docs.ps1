param(
  [string]$Port = "4000",
  [switch]$Open
)

$docsDir = Join-Path $PSScriptRoot ".." "docs" | Resolve-Path

Write-Host "Installing Jekyll gems..." -ForegroundColor Green
Push-Location $docsDir
try {
  bundle install --quiet 2>$null
  if ($LASTEXITCODE -ne 0) {
    bundle install
  }

  $jekyllArgs = "exec", "jekyll", "serve", "--port", $Port
  if ($Open) {
    $jekyllArgs += "--open-url"
  }

  Write-Host "" -ForegroundColor Green
  Write-Host "Starting Jekyll at http://localhost:$Port ..." -ForegroundColor Green
  Write-Host "Ctrl+C to stop" -ForegroundColor Gray
  Write-Host "" -ForegroundColor Green

  & bundle $jekyllArgs
}
finally {
  Pop-Location
}

param(
    [string]$Configuration = "Debug"
)

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "XBVault.Android"

# Set JAVA_HOME to Android SDK JDK if not already set
if (-not $env:JAVA_HOME -or -not (Test-Path $env:JAVA_HOME)) {
    $androidSdk = "$env:LOCALAPPDATA\Android\Sdk"
    $jdkPath = "$androidSdk\jdk-21"
    if (Test-Path $jdkPath) {
        $env:JAVA_HOME = $jdkPath
        Write-Host "Set JAVA_HOME to $jdkPath" -ForegroundColor DarkGray
    }
}

Write-Host "Running XBVault.Android on connected device/emulator..." -ForegroundColor Green
& "C:\Program Files\dotnet\dotnet.exe" run --project $project -c $Configuration

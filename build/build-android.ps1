param(
    [string]$Arch = "x64"
)

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "XBVault.Android"

# Set JAVA_HOME: prefer scoop temurin21-jdk, fallback to ANDROID_HOME/jdk-21
$scoopJdk = "$env:USERPROFILE\scoop\apps\temurin21-jdk\current"
if (Test-Path $scoopJdk) {
    $env:JAVA_HOME = $scoopJdk
    Write-Host "Set JAVA_HOME to scoop temurin21-jdk" -ForegroundColor DarkGray
} elseif (-not $env:JAVA_HOME -or -not (Test-Path $env:JAVA_HOME)) {
    $androidSdk = "$env:LOCALAPPDATA\Android\Sdk"
    $jdkPath = "$androidSdk\jdk-21"
    if (Test-Path $jdkPath) {
        $env:JAVA_HOME = $jdkPath
        Write-Host "Set JAVA_HOME to $jdkPath" -ForegroundColor DarkGray
    }
}

$rid = "android-$Arch"
Write-Host "Publishing XBVault.Android ($rid)..." -ForegroundColor Green
& "C:\Program Files\dotnet\dotnet.exe" publish $project -c Release -r $rid -f net10.0-android36.0

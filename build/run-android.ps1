param(
    [string]$Arch = ""
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

$sdkRoot = "$env:LOCALAPPDATA\Android\Sdk"
$env:ANDROID_SDK_ROOT = $sdkRoot

$emulator = "$sdkRoot\emulator\emulator.exe"
$adb = "$env:USERPROFILE\scoop\apps\android-clt\current\platform-tools\adb.exe"

# Detect connected devices
$devices = & $adb devices 2>&1
$deviceLines = $devices | Where-Object { $_ -match "^\S+\s+device$" }
$deviceCount = ($deviceLines | Measure-Object).Count

if ($deviceCount -eq 0) {
    Write-Host "No device/emulator detected. Starting emulator..." -ForegroundColor Yellow
    Start-Process -FilePath $emulator -ArgumentList "-avd", "Medium_Phone" -WindowStyle Minimized | Out-Null
    Write-Host "Waiting for emulator to boot..." -ForegroundColor Yellow
    & $adb wait-for-device
    do {
        Start-Sleep -Seconds 2
        $boot = & $adb shell getprop sys.boot_completed 2>&1
    } while ($boot -ne "1")
    Write-Host "Emulator ready." -ForegroundColor Green
    $targetDevice = "emulator-5554"
} elseif ($deviceCount -eq 1) {
    $targetDevice = ($deviceLines -split "\s+")[0]
    Write-Host "Using device: $targetDevice" -ForegroundColor Green
} else {
    $emulatorLine = $deviceLines | Where-Object { $_ -match "^emulator-" } | Select-Object -First 1
    if ($emulatorLine) {
        $targetDevice = ($emulatorLine -split "\s+")[0]
    } else {
        $targetDevice = ($deviceLines | Select-Object -First 1) -split "\s+"
        $targetDevice = $targetDevice[0]
    }
    Write-Host "Multiple devices found. Using: $targetDevice" -ForegroundColor Yellow
}

$deviceSerial = $targetDevice
Write-Host "Target: $deviceSerial" -ForegroundColor Cyan

# Auto-detect ABI and map to RID, unless -Arch was explicitly passed
if (-not $Arch) {
    $abi = & $adb -s $deviceSerial shell getprop ro.product.cpu.abi 2>&1
    $abi = $abi.Trim()
    switch ($abi) {
        "arm64-v8a"   { $Arch = "arm64" }
        "armeabi-v7a" { $Arch = "arm" }
        "x86_64"      { $Arch = "x64" }
        "x86"         { $Arch = "x86" }
        default {
            Write-Host "Unknown ABI '$abi', defaulting to arm64" -ForegroundColor Yellow
            $Arch = "arm64"
        }
    }
    Write-Host "Detected ABI: $abi → RID: android-$Arch" -ForegroundColor Cyan
}

$rid = "android-$Arch"

# Full uninstall first — adb install -r corrupts assemblies on emulator
Write-Host "Uninstalling previous build..." -ForegroundColor DarkGray
& $adb -s $deviceSerial uninstall XBVault.Android 2>&1 | Out-Null

Write-Host "Publishing XBVault.Android ($rid)..." -ForegroundColor Green
& "C:\Program Files\dotnet\dotnet.exe" publish $project -c Release -r $rid -f net10.0-android36.0
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed!" -ForegroundColor Red; exit $LASTEXITCODE }

$apkDir = Join-Path $project "bin\Release\net10.0-android36.0\$rid\publish"
$apk = Get-ChildItem -Path $apkDir -Filter "*-Signed.apk" | Select-Object -First 1
if (-not $apk) { $apkDir = Join-Path $project "bin\Release\net10.0-android36.0\$rid"; $apk = Get-ChildItem -Path $apkDir -Filter "*-Signed.apk" | Select-Object -First 1 }
if (-not $apk) { Write-Host "APK not found in $apkDir" -ForegroundColor Red; exit 1 }

Write-Host "Installing $($apk.Name) on $deviceSerial..." -ForegroundColor Green
& $adb -s $deviceSerial install $apk.FullName
if ($LASTEXITCODE -ne 0) { Write-Host "Install failed!" -ForegroundColor Red; exit $LASTEXITCODE }

Write-Host "Starting app..." -ForegroundColor Green
& $adb -s $deviceSerial shell am start -n "XBVault.Android/crc647da56a516f0b6d42.MainActivity"

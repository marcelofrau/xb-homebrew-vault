# log-android.ps1 — Tail XBVault logs from Android emulator via logcat
# Usage:
#   .\log-android.ps1           — tail XBVault logs (tag filter)
#   .\log-android.ps1 -All      — show ALL logcat (no filter)
#   .\log-android.ps1 -Clear    — clear logcat buffer
#   .\log-android.ps1 -Recent 30 — show last N lines only
param(
    [switch]$All,
    [switch]$Clear,
    [int]$Recent = 0
)

$adb = "adb"

if ($Clear) {
    & $adb logcat -c
    Write-Host "Logcat cleared." -ForegroundColor Green
    return
}

if ($Recent -gt 0) {
    if ($All) {
        & $adb logcat -d -v time 2>&1 | Select-Object -Last $Recent
    } else {
        & $adb logcat -d -v time -s "XBVault:D" 2>&1 | Select-Object -Last $Recent
    }
    return
}

if ($All) {
    Write-Host "Tailing ALL logcat (Ctrl+C to stop)..." -ForegroundColor Cyan
    & $adb logcat -v time 2>&1
} else {
    Write-Host "Tailing XBVault logs (Ctrl+C to stop)..." -ForegroundColor Cyan
    Write-Host "Tag filter: XBVault" -ForegroundColor DarkGray
    Write-Host "---" -ForegroundColor DarkGray
    & $adb logcat -v time -s "XBVault:D" 2>&1
}

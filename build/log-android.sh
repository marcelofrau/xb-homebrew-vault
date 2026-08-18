#!/usr/bin/env bash
# log-android.sh — Tail XBVault logs from Android emulator via logcat
# Usage: ./log-android.sh [filter] [--clear]

FILTER="${1:-XBVault}"

if [[ "$1" == "--clear" || "$2" == "--clear" ]]; then
    adb logcat -c
    echo "Logcat cleared."
    exit 0
fi

echo "Tailing XBVault logs from emulator (Ctrl+C to stop)..."
echo "Filter: '$FILTER'"
echo "---"

adb logcat -v time --regex="$FILTER" 2>&1

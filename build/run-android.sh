#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${1:-Debug}"
ARCH="${2:-x64}"

# Set JAVA_HOME if not set
if [ -z "${JAVA_HOME:-}" ]; then
    if [ -d "$HOME/.jdks/temurin-21" ]; then
        export JAVA_HOME="$HOME/.jdks/temurin-21"
        echo "Set JAVA_HOME to $JAVA_HOME"
    elif [ -d "$ANDROID_HOME/jdk-21" ]; then
        export JAVA_HOME="$ANDROID_HOME/jdk-21"
        echo "Set JAVA_HOME to $JAVA_HOME"
    fi
fi

EMULATOR="${ANDROID_HOME:-$HOME/Android/Sdk}/emulator/emulator"
ADB="${ANDROID_HOME:-$HOME/Android/Sdk}/platform-tools/adb"

# Auto-start emulator if no device connected
if ! "$ADB" devices 2>/dev/null | grep -q 'device$'; then
    echo "No device/emulator detected. Starting emulator..."
    "$EMULATOR" -avd Medium_Phone &
    echo "Waiting for emulator to boot..."
    "$ADB" wait-for-device
    while [ "$("$ADB" shell getprop sys.boot_completed 2>/dev/null)" != "1" ]; do
        sleep 2
    done
    echo "Emulator ready."
fi

RID="android-$ARCH"
echo "Running XBVault.Android ($RID)..."
dotnet run --project "$ROOT/XBVault.Android" -c "$CONFIG" -r "$RID"

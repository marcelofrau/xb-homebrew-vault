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

RID="android-$ARCH"
echo "Building XBVault.Android ($CONFIG, $RID)..."
dotnet build "$ROOT/XBVault.Android" -c "$CONFIG" -r "$RID"

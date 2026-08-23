#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${1:?Usage: $0 <version> [arch]}"
ARCH="${2:-arm64}"
OUTPUT_DIR="${3:-dist}"

# Strip leading v prefix if present
VERSION="${VERSION#v}"

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
DIST_DIR="$ROOT/$OUTPUT_DIR"
PROJ_DIR="$ROOT/XBVault.Android"
ZIP_NAME="XBVault-v$VERSION-$RID.zip"
ZIP_PATH="$DIST_DIR/$ZIP_NAME"
PUBLISH_DIR="$DIST_DIR/publish-android"

echo "Building XBVault v$VERSION for $RID..."
mkdir -p "$PUBLISH_DIR"

dotnet clean "$PROJ_DIR" -c Release -r "$RID" || true

dotnet publish "$PROJ_DIR" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:Version="$VERSION" \
    -o "$PUBLISH_DIR"

echo "Packaging $ZIP_NAME..."
cd "$PUBLISH_DIR" && zip -r "$ZIP_PATH" . && cd "$ROOT"

echo "Release created: $ZIP_PATH"
rm -rf "$PUBLISH_DIR"

#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${1:?Usage: $0 <version> [arch]}"
ARCH="${2:-x64}"
PROJECT="${3:-XBVault.Desktop}"
OUTPUT_DIR="${4:-dist}"

# Strip leading v prefix if present
VERSION="${VERSION#v}"

# Detect OS for RID
case "$(uname -s)" in
  Darwin) OS="osx" ;;
  Linux)  OS="linux" ;;
  *)      echo "Unsupported OS: $(uname -s)" >&2; exit 1 ;;
esac
RID="$OS-$ARCH"

DIST_DIR="$ROOT/$OUTPUT_DIR"
PROJ_DIR="$ROOT/$PROJECT"
ZIP_NAME="XBVault-v$VERSION-$RID.zip"
ZIP_PATH="$DIST_DIR/$ZIP_NAME"
PUBLISH_DIR="$DIST_DIR/publish"

if [ ! -d "$PROJ_DIR" ]; then
    echo "Project not found: $PROJ_DIR" >&2
    exit 1
fi

echo "Building XBVault v$VERSION for $RID..."
mkdir -p "$PUBLISH_DIR"

dotnet publish "$PROJ_DIR" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishReadyToRun=true \
    -p:Version="$VERSION" \
    -o "$PUBLISH_DIR"

rm -f "$ZIP_PATH"

if [ "$OS" = "osx" ]; then
    # macOS: ship a double-clickable XBVault.app with the helper scripts beside it,
    # instead of the raw publish folder.
    APP_NAME="XBVault.app"
    STAGE_DIR="$DIST_DIR/stage"
    rm -rf "$STAGE_DIR"
    mkdir -p "$STAGE_DIR"

    echo "Creating $APP_NAME..."
    bash "$ROOT/build/package-macos-app.sh" "$PUBLISH_DIR" "$VERSION" "$STAGE_DIR/$APP_NAME"

    echo "Generating helper scripts..."
    bash "$ROOT/build/gen-helper-scripts.sh" "$STAGE_DIR" "$APP_NAME/Contents/MacOS/XBVault.Desktop"

    echo "Packaging $ZIP_NAME..."
    # ditto (not zip) so the bundle's code signature survives the round trip
    ditto -c -k --sequesterRsrc "$STAGE_DIR" "$ZIP_PATH"
    rm -rf "$STAGE_DIR"
else
    echo "Generating helper scripts..."
    bash "$ROOT/build/gen-helper-scripts.sh" "$PUBLISH_DIR"

    echo "Packaging $ZIP_NAME..."
    cd "$PUBLISH_DIR" && zip -r "$ZIP_PATH" . && cd "$ROOT"
fi

echo "Release created: $ZIP_PATH"
rm -rf "$PUBLISH_DIR"

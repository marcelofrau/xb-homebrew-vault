#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${1:?Usage: $0 <version> [arch]}"
ARCH="${2:-x64}"
PROJECT="${3:-XBVault}"
OUTPUT_DIR="${4:-dist}"

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
    -p:Version="$VERSION" \
    -o "$PUBLISH_DIR"

echo "Packaging $ZIP_NAME..."
cd "$PUBLISH_DIR" && zip -r "$ZIP_PATH" . && cd "$ROOT"

echo "Release created: $ZIP_PATH"
rm -rf "$PUBLISH_DIR"

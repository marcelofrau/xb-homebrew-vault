#!/usr/bin/env bash
# Wraps a `dotnet publish` output directory into a macOS application bundle:
#
#   XBVault.app/Contents/
#     Info.plist
#     MacOS/       <- the whole publish output (apphost + assemblies + native libs)
#     Resources/   <- XBVault.icns
#
# The bundle is ad-hoc signed. That is not notarization — a downloaded copy still
# has to be approved once in System Settings > Privacy & Security — but it gives
# the bundle a valid signature, which Apple Silicon requires to run it at all.
set -euo pipefail

PUBLISH_DIR="${1:?Usage: $0 <publish-dir> <version> <output.app>}"
VERSION="${2:?Usage: $0 <publish-dir> <version> <output.app>}"
APP_PATH="${3:?Usage: $0 <publish-dir> <version> <output.app>}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PLIST_TEMPLATE="$ROOT/build/macos/Info.plist"
ICON_SRC="$ROOT/fastlane/metadata/android/en-US/icon.png"   # largest app icon in the repo (512px)
EXE="XBVault.Desktop"
ICON_NAME="XBVault"

if [ "$(uname -s)" != "Darwin" ]; then
    echo "package-macos-app.sh needs macOS (sips, iconutil, codesign)" >&2
    exit 1
fi
for required in "$PUBLISH_DIR/$EXE" "$PLIST_TEMPLATE" "$ICON_SRC"; do
    if [ ! -f "$required" ]; then
        echo "Missing: $required" >&2
        exit 1
    fi
done

CONTENTS="$APP_PATH/Contents"
rm -rf "$APP_PATH"
mkdir -p "$CONTENTS/MacOS" "$CONTENTS/Resources"

cp -R "$PUBLISH_DIR/." "$CONTENTS/MacOS/"
chmod +x "$CONTENTS/MacOS/$EXE"

# Info.plist — bundle versions must be purely numeric, so drop any -suffix/+metadata.
# The minimum macOS version is whatever the .NET apphost itself was built for.
BUNDLE_VERSION="${VERSION%%[-+]*}"
MIN_MACOS="$(otool -l "$CONTENTS/MacOS/$EXE" | awk '/LC_BUILD_VERSION/ {found=1} found && $1 == "minos" {print $2; exit}')"
MIN_MACOS="${MIN_MACOS:-12.0}"
sed -e "s/__VERSION__/$BUNDLE_VERSION/g" \
    -e "s/__MIN_MACOS__/$MIN_MACOS/g" \
    "$PLIST_TEMPLATE" > "$CONTENTS/Info.plist"
plutil -lint "$CONTENTS/Info.plist" > /dev/null

# App icon — build the .icns from the PNG so no extra binary lives in the repo.
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT
ICONSET="$WORK_DIR/$ICON_NAME.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256; do
    sips -z "$size" "$size" "$ICON_SRC" --out "$ICONSET/icon_${size}x${size}.png" > /dev/null
    sips -z "$((size * 2))" "$((size * 2))" "$ICON_SRC" --out "$ICONSET/icon_${size}x${size}@2x.png" > /dev/null
done
sips -z 512 512 "$ICON_SRC" --out "$ICONSET/icon_512x512.png" > /dev/null
iconutil -c icns -o "$CONTENTS/Resources/$ICON_NAME.icns" "$ICONSET"

# Ad-hoc sign every file inside the bundle, then seal the bundle itself.
codesign --force --deep --sign - "$APP_PATH"
codesign --verify --deep --strict "$APP_PATH"

echo "  Bundled: $APP_PATH (v$BUNDLE_VERSION, macOS $MIN_MACOS+)"

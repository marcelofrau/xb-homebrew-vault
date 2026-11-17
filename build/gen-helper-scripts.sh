#!/usr/bin/env bash
set -euo pipefail

# Usage: gen-helper-scripts.sh <target-dir> [exe-path]
#   exe-path is the executable relative to <target-dir>. It defaults to the bare
#   apphost; macOS releases pass the path inside the .app bundle so the scripts
#   can sit next to XBVault.app.
PUBLISH_DIR="${1:?Usage: $0 <target-dir> [exe-path]}"

if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Publish directory not found: $PUBLISH_DIR" >&2
    exit 1
fi

EXE="${2:-XBVault.Desktop}"

# What to tell the user to launch once Gatekeeper is out of the way
case "$EXE" in
    *.app/*) LAUNCH_HINT="open ${EXE%%.app/*}.app" ;;
    *)       LAUNCH_HINT="run ./$EXE" ;;
esac

gen() {
    local name="$1" flag="$2"
    local path="$PUBLISH_DIR/$name"
    cat > "$path" << SCRIPT
#!/bin/sh
if [ "\$(uname)" = "Darwin" ]; then
    xattr -dr com.apple.quarantine "\$(dirname "\$0")"
fi
"\$(dirname "\$0")/$EXE" $flag
echo "Press Enter to close..."
read _
SCRIPT
    chmod +x "$path"
    echo "  Generated: $path"
}

gen "xbv-reset-data.sh" "--reset-data"
gen "xbv-console.sh" "--console"
gen "xbv-check.sh" "--check"
gen "xbv-run.sh" ""

# Primary launcher (named after the app; exec keeps any console attached to the app)
cat > "$PUBLISH_DIR/XBVault.sh" << SCRIPT
#!/bin/sh
cd "\$(dirname "\$0")"
if [ "\$(uname)" = "Darwin" ]; then
    xattr -dr com.apple.quarantine "\$(pwd)"
fi
exec "./$EXE" "\$@"
SCRIPT
chmod +x "$PUBLISH_DIR/XBVault.sh"
echo "  Generated: $PUBLISH_DIR/XBVault.sh"

# macOS Gatekeeper fix script
FIX_PATH="$PUBLISH_DIR/xbv-fix-macos.sh"
cat > "$FIX_PATH" << SCRIPT
#!/usr/bin/env bash
# Removes macOS quarantine attributes that prevent Avalonia native libs from loading.
# Run this once after extracting the ZIP on macOS.
# Only the quarantine flag is removed: other extended attributes hold the app
# bundle's code signature and must stay.
set -euo pipefail

SCRIPT_DIR="\$(cd "\$(dirname "\$0")" && pwd)"

echo "Removing quarantine attributes from XBVault..."
echo "Location: \$SCRIPT_DIR"
echo ""

xattr -dr com.apple.quarantine "\$SCRIPT_DIR"

echo ""
echo "Done! You can now $LAUNCH_HINT normally."
echo "If it still doesn't work, try: sudo spctl --master-disable (re-enable after)"
SCRIPT
chmod +x "$FIX_PATH"
echo "  Generated: $FIX_PATH"

echo "Helper scripts generated in $PUBLISH_DIR"

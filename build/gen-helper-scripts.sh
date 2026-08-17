#!/usr/bin/env bash
set -euo pipefail

PUBLISH_DIR="${1:?Usage: $0 <publish-dir>}"

if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Publish directory not found: $PUBLISH_DIR" >&2
    exit 1
fi

EXE="XBVault.Desktop"

gen() {
    local name="$1" flag="$2"
    local path="$PUBLISH_DIR/$name"
    cat > "$path" << SCRIPT
#!/bin/sh
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

# macOS Gatekeeper fix script
FIX_PATH="$PUBLISH_DIR/xbv-fix-macos.sh"
cat > "$FIX_PATH" << 'SCRIPT'
#!/usr/bin/env bash
# Removes macOS quarantine attributes that prevent Avalonia native libs from loading.
# Run this once after extracting the ZIP on macOS.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "Removing quarantine attributes from XBVault..."
echo "Location: $SCRIPT_DIR"
echo ""

xattr -cr "$SCRIPT_DIR"

echo ""
echo "Done! You can now run ./XBVault normally."
echo "If it still doesn't work, try: sudo spctl --master-disable (re-enable after)"
SCRIPT
chmod +x "$FIX_PATH"
echo "  Generated: $FIX_PATH"

echo "Helper scripts generated in $PUBLISH_DIR"

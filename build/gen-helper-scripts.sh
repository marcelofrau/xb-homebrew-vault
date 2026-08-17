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

echo "Helper scripts generated in $PUBLISH_DIR"

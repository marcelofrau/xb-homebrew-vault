#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="${1:-XBVault}"
PROJ_DIR="$ROOT/$PROJECT"

if [ ! -d "$PROJ_DIR" ]; then
    echo "Project not found: $PROJ_DIR" >&2
    exit 1
fi

echo "Running $PROJECT..."
dotnet run --project "$PROJ_DIR"

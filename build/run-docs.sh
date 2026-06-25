#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DOCS_DIR="$ROOT/docs"

PORT="4000"
BASEURL="/xb-homebrew-vault"
NO_BASEURL="false"
LIVERELOAD="false"

# Parse arguments
while [[ $# -gt 0 ]]; do
  case "$1" in
    --port)
      PORT="${2:?Missing value for --port}"
      shift 2
      ;;
    --baseurl)
      BASEURL="${2:?Missing value for --baseurl}"
      shift 2
      ;;
    --no-baseurl)
      NO_BASEURL="true"
      shift
      ;;
    --live|-l)
      LIVERELOAD="true"
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      echo "Usage: $0 [--port <port>] [--baseurl <path>] [--no-baseurl] [--live]" >&2
      exit 1
      ;;
  esac
done

if [[ ! -d "$DOCS_DIR" ]]; then
  echo "Docs directory not found: $DOCS_DIR" >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker not found in PATH. Install/start Docker and try again." >&2
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "Docker daemon is not running. Start Rancher Desktop/Docker Desktop and try again." >&2
  exit 1
fi

ARGS=(
  run --rm
  -p "$PORT:4000"
  -v "$DOCS_DIR:/srv/jekyll"
  jekyll/jekyll:pages
)

if [[ "$NO_BASEURL" == "true" ]]; then
  BASEURL_ARG='--baseurl ""'
  echo "Starting docs at http://localhost:$PORT/"
else
  BASEURL_ARG="--baseurl $BASEURL"
  echo "Starting docs at http://localhost:$PORT$BASEURL/"
fi

if [[ "$LIVERELOAD" == "true" ]]; then
  LIVERELOAD_ARG="--livereload"
  echo "LiveReload enabled."
else
  LIVERELOAD_ARG=""
fi

COMMAND="gem list -i webrick >/dev/null || gem install webrick --no-document; jekyll serve --host 0.0.0.0 $BASEURL_ARG $LIVERELOAD_ARG"
ARGS+=(sh -lc "$COMMAND")

docker "${ARGS[@]}"

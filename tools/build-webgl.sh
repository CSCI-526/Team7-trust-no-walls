#!/usr/bin/env bash
# Builds the WebGL player to docs/ via BuildScript.BuildWebGL.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1-arm64/Unity.app/Contents/MacOS/Unity"
PROJECT_PATH="$ROOT/Game"
LOG_DIR="$ROOT/build-logs"
LOG_FILE="$LOG_DIR/webgl.log"
DOCS_DIR="$ROOT/docs"

mkdir -p "$LOG_DIR"

"$UNITY" -batchmode -nographics -quit \
    -projectPath "$PROJECT_PATH" \
    -executeMethod TrustNoWall.EditorTools.BuildScript.BuildWebGL \
    -logFile "$LOG_FILE"
UNITY_EXIT=$?

if [ -f "$DOCS_DIR/index.html" ] && [ -d "$DOCS_DIR/Build" ]; then
    echo "WebGL build OK: docs/index.html"
    exit "$UNITY_EXIT"
else
    echo "WebGL build failed."
    echo "--- last 40 log lines ---"
    tail -n 40 "$LOG_FILE" 2>/dev/null
    exit 1
fi

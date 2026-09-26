#!/usr/bin/env bash
# Runs Unity EditMode tests and prints a one-line pass/fail summary.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1-arm64/Unity.app/Contents/MacOS/Unity"
PROJECT_PATH="$ROOT/Game"
RESULTS_DIR="$ROOT/test-results"
LOG_DIR="$ROOT/build-logs"
RESULTS_XML="$RESULTS_DIR/editmode.xml"
LOG_FILE="$LOG_DIR/tests.log"

mkdir -p "$RESULTS_DIR" "$LOG_DIR"
rm -f "$RESULTS_XML"

"$UNITY" -batchmode -nographics \
    -projectPath "$PROJECT_PATH" \
    -runTests -testPlatform EditMode \
    -testResults "$RESULTS_XML" \
    -logFile "$LOG_FILE"
UNITY_EXIT=$?

if [ "$UNITY_EXIT" -ne 0 ]; then
    echo "Unity exited with code $UNITY_EXIT"
    echo "--- last 40 log lines ---"
    tail -n 40 "$LOG_FILE" 2>/dev/null
    exit 1
fi

if [ ! -f "$RESULTS_XML" ]; then
    echo "Test results file not found: $RESULTS_XML"
    echo "--- last 40 log lines ---"
    tail -n 40 "$LOG_FILE" 2>/dev/null
    exit 1
fi

if grep -q "error CS" "$LOG_FILE"; then
    echo "Compiler errors found in log:"
    grep "error CS" "$LOG_FILE"
    exit 1
fi

SUMMARY=$(python3 - "$RESULTS_XML" <<'PYEOF'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
root = ET.parse(path).getroot()
total = root.attrib.get("total")
passed = root.attrib.get("passed")
failed = root.attrib.get("failed")

print(f"EditMode: {passed}/{total} passed, {failed} failed")
sys.exit(1 if int(failed) != 0 else 0)
PYEOF
)
PY_EXIT=$?

echo "$SUMMARY"
exit "$PY_EXIT"

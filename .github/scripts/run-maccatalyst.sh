#!/usr/bin/env bash
# Runs the Mac Catalyst app headlessly on the macOS runner and checks the result.
set -uo pipefail

mkdir -p mobile-logs

APP=$(find Tests/Mobile -type d -path "*bin*maccatalyst*/*.app" -not -path "*obj*" | head -n 1)
if [ -z "$APP" ]; then APP=$(find Tests/Mobile -type d -path "*maccatalyst*/*.app" -not -path "*obj*" | head -n 1); fi
echo "App bundle: $APP"
if [ -z "$APP" ]; then echo "No .app bundle found"; exit 1; fi

EXE_NAME=$(ls "$APP/Contents/MacOS/" | head -n 1)
EXE="$APP/Contents/MacOS/$EXE_NAME"
echo "Executable: $EXE"
chmod +x "$EXE" 2>/dev/null || true

# Note: macOS has no `timeout`; the app exits itself in CI mode. The workflow step
# provides a hard time limit.
set +e
SNOWBALL_CI=1 "$EXE" > mobile-logs/maccatalyst-output.log 2>&1
CODE=$?
set -e
echo "Process exit code: $CODE"
tail -n 30 mobile-logs/maccatalyst-output.log || true

if grep -q "result=PASS" mobile-logs/maccatalyst-output.log; then
  echo "Mac Catalyst tests PASSED"
  exit 0
fi
echo "Mac Catalyst tests FAILED or timed out (exit code $CODE)"
exit 1

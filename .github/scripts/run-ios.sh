#!/usr/bin/env bash
# Boots an iOS Simulator, installs the app, runs the suite, and checks the result.
set -uo pipefail

BUNDLE=com.sqlitefts.snowball.tests
mkdir -p mobile-logs

APP=$(find Tests/Mobile -type d -path "*bin*iossimulator*/*.app" -not -path "*obj*" | head -n 1)
if [ -z "$APP" ]; then APP=$(find Tests/Mobile -type d -path "*iossimulator*/*.app" -not -path "*obj*" | head -n 1); fi
echo "App bundle: $APP"
if [ -z "$APP" ]; then echo "No .app bundle found"; exit 1; fi

RUNTIME=$(xcrun simctl list runtimes available | grep -Eo 'com.apple.CoreSimulator.SimRuntime.iOS[-0-9]+' | tail -n 1)
echo "Runtime: $RUNTIME"

# Prefer an already-available iPhone simulator (any iOS runtime); the app targets a
# lower iOS SDK and runs fine on newer runtimes. Fall back to creating one.
UDID=$(xcrun simctl list devices available --json | python3 -c "import json,sys; d=json.load(sys.stdin)['devices']; print(next((dev['udid'] for rt in sorted(d) if 'iOS' in rt for dev in d[rt] if 'iPhone' in dev.get('name','')), ''))")
if [ -z "$UDID" ]; then
  DEVTYPE=$(xcrun simctl list devicetypes --json | python3 -c "import json,sys; d=json.load(sys.stdin)['devicetypes']; ps=[t['identifier'] for t in d if 'iPhone' in t.get('name','')]; print(ps[-1] if ps else '')")
  echo "Creating simulator: $DEVTYPE on $RUNTIME"
  UDID=$(xcrun simctl create snowball-sim "$DEVTYPE" "$RUNTIME")
fi
echo "Simulator UDID: $UDID"
if [ -z "$UDID" ]; then echo "Could not obtain a simulator"; exit 1; fi
xcrun simctl boot "$UDID" 2>/dev/null || true
for _ in $(seq 1 45); do
  if xcrun simctl list devices | grep "$UDID" | grep -q Booted; then break; fi
  sleep 2
done
echo "Boot state: $(xcrun simctl list devices | grep "$UDID" || true)"

xcrun simctl install "$UDID" "$APP"

# `simctl launch` can block indefinitely on some runners, so run it in the background and
# poll the app's result file concurrently (the app writes it a few seconds into the run).
echo "Launching app..."
( SIMCTL_CHILD_SNOWBALL_CI=1 xcrun simctl launch --terminate-running-process "$UDID" "$BUNDLE" ) >/dev/null 2>&1 &
LPID=$!

CONTAINER=""
RESULT=""
for _ in $(seq 1 75); do
  if [ -z "$CONTAINER" ]; then CONTAINER=$(xcrun simctl get_app_container "$UDID" "$BUNDLE" data 2>/dev/null || true); fi
  if [ -n "$CONTAINER" ]; then
    FILE=$(find "$CONTAINER" -name snowball-result.txt 2>/dev/null | head -n 1)
    if [ -n "$FILE" ]; then RESULT=$(cat "$FILE"); break; fi
  fi
  sleep 4
done
kill "$LPID" 2>/dev/null || true
echo "Data container: $CONTAINER"

# Collect the app's own log + result + any crash report for the artifact.
if [ -n "$CONTAINER" ]; then
  find "$CONTAINER" \( -name "mobile-*.log" -o -name "snowball-result.txt" \) -exec cp {} mobile-logs/ \; 2>/dev/null || true
fi
cp "$HOME"/Library/Logs/DiagnosticReports/*SnowballTests* mobile-logs/ 2>/dev/null || true

echo "----- app log tail -----"
tail -n 25 mobile-logs/mobile-*.log 2>/dev/null || true

xcrun simctl shutdown "$UDID" 2>/dev/null || true

echo "----- RESULT -----"
echo "$RESULT"
if echo "$RESULT" | grep -q "result=PASS"; then
  echo "iOS tests PASSED"
  exit 0
fi
echo "iOS tests FAILED or timed out"
exit 1

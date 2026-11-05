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
xcrun simctl boot "$UDID" || true
xcrun simctl bootstatus "$UDID" -b || true

xcrun simctl install "$UDID" "$APP"

# Capture the simulator system log for our process in the background.
( xcrun simctl spawn "$UDID" log stream --level debug --predicate 'processImagePath CONTAINS "SnowballTests"' > mobile-logs/ios-syslog.txt 2>&1 ) &
LOGPID=$!

echo "Launching app (console)..."
set +e
SIMCTL_CHILD_SNOWBALL_CI=1 xcrun simctl launch --console-pty "$UDID" "$BUNDLE" > mobile-logs/ios-console.log 2>&1
echo "launch returned $?"
set -e

sleep 3
kill $LOGPID 2>/dev/null || true

echo "----- console tail -----"
tail -n 60 mobile-logs/ios-console.log 2>/dev/null || true
echo "----- syslog tail -----"
tail -n 40 mobile-logs/ios-syslog.txt 2>/dev/null || true

CONTAINER=$(xcrun simctl get_app_container "$UDID" "$BUNDLE" data 2>/dev/null || true)
echo "Data container: $CONTAINER"

RESULT=$(grep "SNOWBALL_RESULT" mobile-logs/ios-console.log 2>/dev/null | tail -n 1)
if [ -z "$RESULT" ]; then RESULT=$(grep "SNOWBALL_RESULT" mobile-logs/ios-syslog.txt 2>/dev/null | tail -n 1); fi
if [ -z "$RESULT" ] && [ -n "$CONTAINER" ]; then
  FILE=$(find "$CONTAINER" -name snowball-result.txt 2>/dev/null | head -n 1)
  if [ -n "$FILE" ]; then RESULT=$(cat "$FILE"); cp "$FILE" mobile-logs/ 2>/dev/null || true; fi
fi

# Collect app logs and any crash reports for the artifact.
[ -n "$CONTAINER" ] && find "$CONTAINER" -name "mobile-*.log" -exec cp {} mobile-logs/ \; 2>/dev/null || true
cp "$HOME"/Library/Logs/DiagnosticReports/*SnowballTests* mobile-logs/ 2>/dev/null || true

xcrun simctl shutdown "$UDID" 2>/dev/null || true
xcrun simctl delete "$UDID" 2>/dev/null || true

echo "----- RESULT -----"
echo "$RESULT"
if echo "$RESULT" | grep -q "result=PASS"; then
  echo "iOS tests PASSED"
  exit 0
fi
echo "iOS tests FAILED or timed out"
exit 1

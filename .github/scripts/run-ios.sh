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
DEVTYPE=$(xcrun simctl list devicetypes | grep -Eo 'com.apple.CoreSimulator.SimDeviceType.iPhone-[0-9]+' | tail -n 1)
echo "Runtime: $RUNTIME  DeviceType: $DEVTYPE"

UDID=$(xcrun simctl create snowball-sim "$DEVTYPE" "$RUNTIME")
echo "Simulator UDID: $UDID"
xcrun simctl boot "$UDID"
xcrun simctl bootstatus "$UDID" -b

xcrun simctl install "$UDID" "$APP"
SIMCTL_CHILD_SNOWBALL_CI=1 xcrun simctl launch "$UDID" "$BUNDLE" || true

CONTAINER=$(xcrun simctl get_app_container "$UDID" "$BUNDLE" data)
echo "Data container: $CONTAINER"

RESULT=""
FILE=""
for _ in $(seq 1 90); do
  FILE=$(find "$CONTAINER" -name snowball-result.txt 2>/dev/null | head -n 1)
  if [ -n "$FILE" ]; then RESULT=$(cat "$FILE"); break; fi
  sleep 4
done

echo "----- RESULT -----"
echo "$RESULT"
find "$CONTAINER" -name "mobile-*.log" -exec cp {} mobile-logs/ \; 2>/dev/null || true
[ -n "$FILE" ] && cp "$FILE" mobile-logs/ 2>/dev/null || true

xcrun simctl shutdown "$UDID" 2>/dev/null || true
xcrun simctl delete "$UDID" 2>/dev/null || true

if echo "$RESULT" | grep -q "result=PASS"; then
  echo "iOS tests PASSED"
  exit 0
fi
echo "iOS tests FAILED or timed out"
exit 1

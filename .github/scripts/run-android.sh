#!/usr/bin/env bash
# Runs inside reactivecircus/android-emulator-runner after the emulator has booted.
set -uo pipefail

PKG=com.sqlitefts.snowball.tests
mkdir -p mobile-logs

APK=$(find Tests/Mobile -name "*-Signed.apk" | head -n 1)
echo "APK: $APK"
if [ -z "$APK" ]; then echo "No signed APK found"; exit 1; fi

adb wait-for-device
adb install -r -g "$APK"

adb logcat -c || true
ACT=$(adb shell cmd package resolve-activity --brief "$PKG" | tr -d '\r' | tail -n 1)
echo "Launcher activity: $ACT"
adb shell am start -n "$ACT" --es SNOWBALL_CI 1

echo "Waiting for the test result..."
RESULT=""
for _ in $(seq 1 90); do
  FILE=$(adb shell "cat /sdcard/Android/data/$PKG/files/snowball-result.txt" 2>/dev/null | tr -d '\r')
  if echo "$FILE" | grep -q "SNOWBALL_RESULT"; then RESULT="$FILE"; break; fi
  LOG=$(adb logcat -d -s SNOWBALL:I 2>/dev/null | grep "SNOWBALL_RESULT" | tail -n 1)
  if [ -n "$LOG" ]; then RESULT="$LOG"; break; fi
  sleep 4
done

echo "----- RESULT -----"
echo "$RESULT"
adb pull "/sdcard/Android/data/$PKG/files" mobile-logs 2>/dev/null || true
adb logcat -d -s SNOWBALL:I > mobile-logs/android-logcat-snowball.txt 2>/dev/null || true

if echo "$RESULT" | grep -q "result=PASS"; then
  echo "Android tests PASSED"
  exit 0
fi
echo "Android tests FAILED or timed out"
exit 1

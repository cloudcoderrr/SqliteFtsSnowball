#!/usr/bin/env bash
# Runs inside reactivecircus/android-emulator-runner after the emulator has booted.
set -uo pipefail

PKG=com.sqlitefts.snowball.tests
mkdir -p mobile-logs

adb wait-for-device
adb root >/dev/null 2>&1 || true
adb wait-for-device

APK=$(find Tests/Mobile -path "*android-x64*-Signed.apk" | head -n 1)
if [ -z "$APK" ]; then APK=$(find Tests/Mobile -name "*-Signed.apk" | head -n 1); fi
echo "APK: $APK"
if [ -z "$APK" ]; then echo "No signed APK found"; exit 1; fi
adb install -r -g "$APK"

adb logcat -c || true
adb logcat > mobile-logs/android-logcat-full.txt 2>&1 &
LOGPID=$!

ACT=$(adb shell cmd package resolve-activity --brief "$PKG" | tr -d '\r' | tail -n 1)
echo "Launcher activity: $ACT"
adb shell am start -n "$ACT" --es SNOWBALL_CI 1

echo "Waiting for the test result..."
RESULT=""
for _ in $(seq 1 75); do
  LOG=$(adb logcat -d -s SNOWBALL:I 2>/dev/null | grep "SNOWBALL_RESULT" | tail -n 1)
  if [ -n "$LOG" ]; then RESULT="$LOG"; break; fi
  FILE=$(adb shell "run-as $PKG cat files/snowball-result.txt" 2>/dev/null | tr -d '\r')
  if echo "$FILE" | grep -q "SNOWBALL_RESULT"; then RESULT=$(echo "$FILE" | grep "SNOWBALL_RESULT" | tail -n 1); break; fi
  if adb logcat -b crash -d 2>/dev/null | grep -q "$PKG"; then echo "Crash detected in crash buffer"; break; fi
  sleep 4
done

kill $LOGPID 2>/dev/null || true
echo "----- RESULT -----"
echo "$RESULT"

# Collect diagnostics for the artifact.
adb logcat -b crash -d > mobile-logs/android-crash.txt 2>/dev/null || true
adb logcat -d -s SNOWBALL:I SNOWBALL:E DOTNET:I AndroidRuntime:E "*:F" > mobile-logs/android-logcat-app.txt 2>/dev/null || true
adb exec-out run-as "$PKG" sh -c 'cd files && tar c .' 2>/dev/null | tar x -C mobile-logs 2>/dev/null || true

if echo "$RESULT" | grep -q "result=PASS"; then
  echo "Android tests PASSED"
  exit 0
fi
echo "Android tests FAILED or timed out"
exit 1


#!/usr/bin/env bash
# Installs an APK on the emulator, lets it run, captures logcat + screenshots, fails on any crash marker.
#   tools/emulator_smoke.sh [apk] [seconds] [avd]
# Default apk: build/out/UrbanRunner-<ver>-standalone-emulator.apk (Unity autoplays on its own).
#   tools/emulator_smoke.sh combined [avd]
# Combined app (Flutter + exported Unity): 3 runs Home → Game → Results → Home via integration_test,
# with logcat captured and scanned. Export Unity first (tools/build_all.sh emulator).
set -u
source "$(dirname "$0")/env.sh"
source "$ROOT/tools/lib/adb.sh"
cd "$ROOT"

if [ "${1:-}" = combined ]; then
  AVD="${2:-ur_api36}"
  STAMP="$(date +%Y%m%d-%H%M%S)"
  LOGCAT="build/logs/combined_logcat_$STAMP.txt"
  mkdir -p build/logs
  ur_ensure_emulator "$AVD"
  echo "== combined smoke on $ADB_SERIAL"
  ur_logcat_start "$LOGCAT"
  (cd app && flutter test integration_test/combined_test.dart -d "$ADB_SERIAL") > "build/logs/combined_test_$STAMP.log" 2>&1
  RC=$?
  ur_screenshot "build/logs/combined_end_$STAMP.png" || true
  ur_logcat_stop
  tail -5 "build/logs/combined_test_$STAMP.log"
  grep -E "UR_PERF" "$LOGCAT" | tail -8
  FAIL=$RC
  if ! CR="$(ur_scan_crashes "$LOGCAT")"; then echo "$CR" | head -20; FAIL=1; fi
  echo "logcat: $LOGCAT  test log: build/logs/combined_test_$STAMP.log"
  [ "$FAIL" = 0 ] && echo "SMOKE CLEAN" || echo "SMOKE FAILED"
  exit "$FAIL"
fi

VERSION="$(grep -E '^version:' app/pubspec.yaml | sed -E 's/version: *([^+]+).*/\1/')"
APK="${1:-build/out/UrbanRunner-$VERSION-standalone-emulator.apk}"
SECONDS_TO_RUN="${2:-60}"
AVD="${3:-ur_api36}"
STAMP="$(date +%Y%m%d-%H%M%S)"
LOGCAT="build/logs/smoke_logcat_$STAMP.txt"
SHOTS="build/smoke/$STAMP"
[ -f "$APK" ] || { echo "UR_ERROR apk not found: $APK"; exit 2; }
mkdir -p "$SHOTS"

ur_ensure_emulator "$AVD"
AAPT2="$(ls -1d "$ANDROID_SDK_ROOT"/build-tools/*/aapt2 | sort -V | tail -1)"
PKG="$("$AAPT2" dump packagename "$APK")"
echo "== smoke $APK ($PKG) on $ADB_SERIAL for ${SECONDS_TO_RUN}s"
adbx install -r "$APK" >/dev/null || { echo "UR_ERROR install failed"; exit 1; }
ur_logcat_start "$LOGCAT"
adbx shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1

FAIL=0
step=$(( SECONDS_TO_RUN / 4 )); [ "$step" -lt 1 ] && step=1
for i in 1 2 3 4; do
  sleep "$step"
  ur_screenshot "$SHOTS/shot_$i.png"
  [ -n "$(adbx shell pidof "$PKG" | tr -d '\r')" ] || { echo "UR_ERROR process died"; FAIL=1; break; }
done
adbx shell dumpsys meminfo "$PKG" | grep -E "TOTAL PSS|TOTAL:" | head -1
ur_logcat_stop

grep -E "UR_PERF" "$LOGCAT" | tail -5
if ! CR="$(ur_scan_crashes "$LOGCAT")"; then echo "$CR" | head -20; FAIL=1; fi
echo "logcat: $LOGCAT  screenshots: $SHOTS"
[ "$FAIL" = 0 ] && echo "SMOKE CLEAN" || echo "SMOKE FAILED"
exit "$FAIL"

#!/usr/bin/env bash
# M0 bridge spike (HANDOVER §9 M0): runs the 7 checks against the combined Flutter+Unity APK on the emulator.
#   tools/spike_m0.sh <apk>
# Writes build/logs/spike_results.txt, build/logs/spike_logcat.txt and release/screenshots/m0/*.png.
set -u
source "$(dirname "$0")/env.sh"
source "$ROOT/tools/lib/adb.sh"
APK="${1:?usage: spike_m0.sh <apk>}"
SHOTS="$ROOT/release/screenshots/m0"
RES="$ROOT/build/logs/spike_results.txt"
LOGCAT="$ROOT/build/logs/spike_logcat.txt"
mkdir -p "$SHOTS"; : > "$RES"

result() { echo "$1 | $2 | $3" | tee -a "$RES"; }
pid() { adbx shell pidof "$APP_ID" | tr -d '\r'; }
wait_text() { # wait_text <needle> <seconds> -> prints node text
  local i t
  for i in $(seq 1 "$2"); do t="$(ur_ui_text "$1")"; [ -n "$t" ] && { echo "$t"; return 0; }; sleep 1; done
  return 1
}
rtt_check() { # taps Ping x20 and checks median < 100 ms
  ur_tap_text "Ping x20" 5 || return 1
  local t; t="$(wait_text "rtt: n=" 30)" || return 1
  echo "$t"
  python3 - "$t" <<'EOF'
import re, sys
m = re.search(r"n=(\d+)/(\d+) median=([\d.]+)ms max=([\d.]+)ms", sys.argv[1])
sys.exit(0 if m and m.group(1) == m.group(2) and float(m.group(3)) < 100 else 1)
EOF
}

ur_ensure_emulator ur_api36
echo "device: $ADB_SERIAL  sdk=$(adbx shell getprop ro.build.version.sdk | tr -d '\r')  abi=$(adbx shell getprop ro.product.cpu.abi | tr -d '\r')" | tee -a "$RES"
adbx uninstall "$APP_ID" >/dev/null 2>&1
adbx install -r "$APK" >/dev/null || { result 0 install FAIL; exit 1; }
adbx shell settings put system user_rotation 0
ur_logcat_start "$LOGCAT"
adbx shell am start -W -n "$APP_ID/.MainActivity" | grep -E "TotalTime|WaitTime" | tee -a "$RES"
sleep 2
ur_screenshot "$SHOTS/01_home.png"

# 1. Unity renders
T0=$(date +%s.%N)
ur_tap_text "Open Unity spike" 15
if STATUS="$(wait_text "status: ready" 40)"; then
  T1=$(date +%s.%N)
  sleep 2; ur_screenshot "$SHOTS/02_unity_rendered.png"
  result 1 "unity renders" "PASS ($STATUS; ready after $(python3 -c "print(round($T1-$T0,2))") s)"
else
  ur_screenshot "$SHOTS/02_unity_failed.png"
  result 1 "unity renders" "FAIL ($(ur_ui_text 'status:'))"
  ur_logcat_stop; ur_scan_crashes "$LOGCAT" | head -20; exit 1
fi
PID0="$(pid)"

# 2. Touch and swipe input (Unity view fills the top of the screen)
read -r W H < <(adbx shell wm size | awk -F'[ x]' '/Physical/{print $3, $4}' | tr -d '\r')
CX=$((W/2)); CY=$((H*35/100)); D=$((W/4))
adbx shell input swipe $CX $CY $((CX-D)) $CY 120; sleep 0.6
adbx shell input swipe $CX $CY $((CX+D)) $CY 120; sleep 0.6
adbx shell input swipe $CX $CY $CX $((CY-D)) 120; sleep 0.6
adbx shell input swipe $CX $CY $CX $((CY+D)) 120; sleep 1
SW="$(ur_ui_text 'swipes:')"
ur_screenshot "$SHOTS/03_after_swipes.png"
[[ "$SW" == "swipes: 4 last: down" ]] && result 2 "touch + swipe" "PASS ($SW; left,right,up,down sent)" || result 2 "touch + swipe" "FAIL ($SW)"

# 3. ping -> pong < 100 ms
OUT="$(rtt_check)" && result 3 "ping-pong < 100 ms" "PASS ($(echo "$OUT" | tail -1))" || result 3 "ping-pong < 100 ms" "FAIL ($OUT)"

# 4. Pause and resume
ur_tap_text "Pause+Resume" 5
P="$(wait_text "pause/resume: " 10 | grep -v ': -$' )"; sleep 3; P="$(ur_ui_text 'pause/resume:')"
[[ "$P" == "pause/resume: ok" ]] && result 4 "pause + resume" "PASS" || result 4 "pause + resume" "FAIL ($P)"

# 5. Leave the screen and return
ur_tap_text "Back" 5; sleep 2
ur_screenshot "$SHOTS/04_left_screen.png"
ur_tap_text "Open Unity spike" 10
if wait_text "status: ready" 15 >/dev/null && OUT="$(rtt_check)"; then
  sleep 1; ur_screenshot "$SHOTS/05_returned.png"
  [ "$(pid)" = "$PID0" ] && result 5 "leave + return" "PASS (same process, bridge alive: $(echo "$OUT" | tail -1))" \
                         || result 5 "leave + return" "FAIL (process restarted)"
else
  result 5 "leave + return" "FAIL ($OUT)"
fi

# 6. Background / foreground
adbx shell input keyevent KEYCODE_HOME; sleep 4
adbx shell am start -n "$APP_ID/.MainActivity" >/dev/null; sleep 3
if [ "$(pid)" = "$PID0" ] && OUT="$(rtt_check)"; then
  ur_screenshot "$SHOTS/06_after_background.png"
  result 6 "background + foreground" "PASS (same process, $(echo "$OUT" | tail -1))"
else
  ur_screenshot "$SHOTS/06_after_background_fail.png"
  result 6 "background + foreground" "FAIL (pid $(pid) vs $PID0; $OUT)"
fi

# 7. Rotation: force landscape at the system level, then emulator rotate
adbx shell settings put system user_rotation 1; sleep 2
"$ADB" -s "$ADB_SERIAL" emu rotate >/dev/null 2>&1; sleep 2
ur_screenshot "$SHOTS/07_rotated.png"
if [ "$(pid)" = "$PID0" ] && OUT="$(rtt_check)"; then
  result 7 "rotation" "PASS (no restart, still portrait, $(echo "$OUT" | tail -1))"
else
  result 7 "rotation" "FAIL (pid $(pid) vs $PID0; $OUT)"
fi
"$ADB" -s "$ADB_SERIAL" emu rotate >/dev/null 2>&1
adbx shell settings put system user_rotation 0
sleep 2

ur_logcat_stop
adbx shell dumpsys meminfo "$APP_ID" | grep -E "TOTAL PSS|TOTAL:" | head -1 | tee -a "$RES"
if CR="$(ur_scan_crashes "$LOGCAT")"; then
  result 8 "logcat crash scan" "PASS (no crash markers)"
else
  result 8 "logcat crash scan" "FAIL"; echo "$CR" | head -20 | tee -a "$RES"
fi
grep -c "UR_BRIDGE" "$LOGCAT" | xargs -I{} echo "UR_BRIDGE lines in logcat: {}" | tee -a "$RES"
! grep -q "| FAIL" "$RES"

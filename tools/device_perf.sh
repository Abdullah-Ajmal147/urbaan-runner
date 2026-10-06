#!/usr/bin/env bash
# Measures in-game performance on a real phone connected by USB (USB debugging on).
#   tools/device_perf.sh [apk] [seconds]
# Installs the phone APK, opens the app, and records the game's UR_PERF logs while you play for [seconds]
# (default 180). Prints the device, the frame-rate windows and the adaptive-quality steps it settled on.
set -u
source "$(dirname "$0")/env.sh"
cd "$ROOT"
APK="${1:-release/test-builds/UrbanRunner-0.1.0-phone-arm64.apk}"
SECS="${2:-180}"
PKG=com.gamefactory.urbanrunner
ADB="${ADB:-adb}"

SERIAL="$("$ADB" devices | awk 'NR>1 && $2=="device" && $1 !~ /^emulator-/ {print $1; exit}')"
if [ -z "$SERIAL" ]; then
  echo "No phone found. On the phone: Settings → About phone → tap Build number 7 times;"
  echo "then Settings → Developer options → USB debugging ON; connect USB and tap Allow."
  "$ADB" devices
  exit 2
fi
a() { "$ADB" -s "$SERIAL" "$@"; }
STAMP="$(date +%Y%m%d-%H%M%S)"
LOG="build/logs/device_perf_$STAMP.txt"
mkdir -p build/logs

MODEL="$(a shell getprop ro.product.model | tr -d '\r')"
SOC="$(a shell getprop ro.soc.model | tr -d '\r')"
ANDROID="$(a shell getprop ro.build.version.release | tr -d '\r')"
echo "== phone: $MODEL (SoC ${SOC:-?}, Android $ANDROID), serial $SERIAL"
echo "== installing $APK"
a install -r "$APK" >/dev/null || { echo "UR_ERROR install failed (if it says signatures differ: uninstall the old app first)"; exit 1; }
a logcat -c
a shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
echo "== app opened. Play normally now (several runs). Recording for ${SECS}s..."
a logcat -v time > "$LOG" 2>&1 &
LC=$!
sleep "$SECS"
kill "$LC" 2>/dev/null; wait "$LC" 2>/dev/null
a shell dumpsys gfxinfo "$PKG" > "build/logs/device_gfxinfo_$STAMP.txt" 2>/dev/null
a shell dumpsys meminfo "$PKG" | grep -E "TOTAL PSS|TOTAL:" | head -1

echo "== device:"; grep -h "UR_PERF device" "$LOG" | tail -1 | sed 's/.*UR_PERF //'
echo "== shader warm-up:"; grep -h "shader_warmup_ms" "$LOG" | tail -1 | sed 's/.*UR_PERF //'
echo "== quality steps:"; grep -h "quality_level=" "$LOG" | sed 's/.*UR_PERF //' | uniq | tail -6
echo "== frame-rate windows (10 s each):"
grep -h "UR_PERF window" "$LOG" | sed -E 's/.*fps_avg=([0-9.]+) fps_min=([0-9.]+).*draw_calls_avg=([0-9]+).*/\1 \2 \3/' |
  awk '{n++; s+=$1; if (min==""||$2<min) min=$2; printf "  avg %5.1f  min %5.1f  draws %s\n", $1, $2, $3} END {if (n) printf "== overall avg %.1f fps over %d windows, worst frame-rate %s\n", s/n, n, min; else print "  (no in-game windows: did a run start?)"}'
CR="$(grep -cE 'FATAL EXCEPTION|ANR in|Fatal signal' "$LOG")"
echo "== crashes/ANRs: $CR   full log: $LOG"

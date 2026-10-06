#!/usr/bin/env bash
# Walks through every screen of the combined app on the emulator and saves a screenshot of each.
#   tools/screen_tour.sh [apk] [out_dir]
# Fresh install: splash → onboarding (3 cards) → first run (the tutorial, played with adb swipes) →
# crash → "Keep running?" (Unity) → Results → Home → Missions → Locker → Store → Profile → Settings.
set -u
source "$(dirname "$0")/env.sh"
source "$ROOT/tools/lib/adb.sh"
cd "$ROOT"

VERSION="$(grep -E '^version:' app/pubspec.yaml | sed -E 's/version: *([^+]+).*/\1/')"
APK="${1:-build/out/UrbanRunner-$VERSION-emulator.apk}"
OUT="${2:-build/tour/$(date +%Y%m%d-%H%M%S)}"
PKG=com.gamefactory.urbanrunner
[ -f "$APK" ] || { echo "UR_ERROR apk not found: $APK"; exit 2; }
mkdir -p "$OUT"

ur_ensure_emulator ur_api36
adbx uninstall "$PKG" >/dev/null 2>&1 || true
adbx install -r "$APK" >/dev/null || { echo "UR_ERROR install failed"; exit 1; }
LOGCAT="$OUT/logcat.txt"
ur_logcat_start "$LOGCAT"

FAIL=0
shot() { ur_screenshot "$OUT/$1.png"; echo "  shot $1"; }
step() { "$@" || FAIL=1; } # a missed tap fails the tour instead of drifting into other screens
# The game screen is Unity: Flutter's bottom tabs disappear while it's up.
in_game() { [ -z "$(ur_ui_find "Missions")" ]; }
swipe() { # left|right|up|down
  case "$1" in
    left)  adbx shell input swipe 760 1500 260 1500 120 ;;
    right) adbx shell input swipe 320 1500 820 1500 120 ;;
    up)    adbx shell input swipe 540 1700 540 1100 120 ;;
    down)  adbx shell input swipe 540 1100 540 1700 120 ;;
  esac
}

adbx shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
sleep 0.8; shot 01_splash
for i in $(seq 1 20); do [ -n "$(ur_ui_find "Next")" ] && break; sleep 1; done
shot 02_onboarding_1
step ur_tap_text "Next" 10; sleep 1; shot 03_onboarding_2
step ur_tap_text "Next" 10; sleep 1; shot 04_onboarding_3
step ur_tap_text "Let's go" 10; sleep 2; shot 05_home_first_run

# First run = tutorial. Prompts freeze the run until the right swipe; tutorial obstacles can't hurt.
for i in $(seq 1 15); do [ -n "$(ur_ui_find "Start your first run")" ] && break; sleep 1; done
sleep 1.5 # let the page transition settle before tapping
step ur_tap_text "Start your first run" 10
for i in $(seq 1 20); do in_game && break; sleep 1; done
if ! in_game; then echo "UR_ERROR the game screen never opened"; ur_logcat_stop; exit 1; fi
sleep 6; shot 06_game_tutorial
for cycle in 1 2 3; do
  for d in left right up down; do swipe "$d"; sleep 2.2; done
  [ "$cycle" = 1 ] && shot 07_tutorial
done
sleep 4; shot 08_run_after_tutorial
# No more input: the courier crashes → "Keep running?" card for 5 s → Results.
for i in $(seq 1 40); do
  sleep 1
  if [ -n "$(ur_ui_find "metres")" ]; then break; fi
  [ "$i" = 12 ] && shot 09_run_or_continue
done
shot 10_results
step ur_tap_text "Home" 10; sleep 2; shot 11_home
step ur_tap_text "Missions" 10; sleep 1.5; shot 12_missions
step ur_tap_text "Locker" 10; sleep 1.5; shot 13_locker
step ur_tap_text "Store" 10; sleep 1.5; shot 14_store
step ur_tap_text "Home" 10; sleep 1.5
step ur_tap_text "Profile" 10; sleep 1.5; shot 15_profile
adbx shell input keyevent KEYCODE_BACK; sleep 1.2
step ur_tap_text "Settings" 10; sleep 1.5; shot 16_settings
adbx shell input keyevent KEYCODE_BACK; sleep 1

ur_logcat_stop
if ! CR="$(ur_scan_crashes "$LOGCAT")"; then echo "$CR" | head -20; echo "TOUR FAILED (crash markers)"; exit 1; fi
[ "$FAIL" = 0 ] || { echo "TOUR FAILED (a step didn't find its target) → $OUT"; exit 1; }
echo "TOUR CLEAN → $OUT"

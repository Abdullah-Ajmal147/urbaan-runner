#!/usr/bin/env bash
# adb helpers shared by emulator_smoke and the spike (HANDOVER §10.2). Source after env.sh.

ADB_SERIAL="${ADB_SERIAL:-}"
adbx() { if [ -n "$ADB_SERIAL" ]; then "$ADB" -s "$ADB_SERIAL" "$@"; else "$ADB" "$@"; fi; }

# First running emulator, or empty.
ur_emulator_serial() { "$ADB" devices | awk '/^emulator-[0-9]+\tdevice/{print $1; exit}'; }

# Boot an AVD if no emulator is running. ur_ensure_emulator [avd]
ur_ensure_emulator() {
  local avd="${1:-ur_api36}"
  ADB_SERIAL="$(ur_emulator_serial)"
  if [ -z "$ADB_SERIAL" ]; then
    echo "== booting $avd"
    nohup "$EMULATOR" -avd "$avd" -no-snapshot-save -no-boot-anim -gpu host >"$ROOT/build/logs/emulator_$avd.log" 2>&1 &
    "$ADB" wait-for-device
    ADB_SERIAL="$(ur_emulator_serial)"
  fi
  local i
  for i in $(seq 1 120); do
    [ "$(adbx shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = 1 ] && break
    sleep 2
  done
  export ADB_SERIAL
  adbx shell settings put system accelerometer_rotation 0 >/dev/null 2>&1 || true
}

# Dump the UI hierarchy (Flutter exposes semantics to uiautomator).
ur_ui_dump() { adbx shell uiautomator dump /sdcard/ui.xml >/dev/null 2>&1; adbx shell cat /sdcard/ui.xml; }

# Centre of the first node whose text or content-desc contains $1. Prints "x y".
ur_ui_find() {
  ur_ui_dump | python3 -c '
import re, sys
needle = sys.argv[1]
for node in re.findall(r"<node [^>]*>", sys.stdin.read()):
    attrs = dict(re.findall(r"([\w-]+)=\"([^\"]*)\"", node))
    if needle in attrs.get("text", "") or needle in attrs.get("content-desc", ""):
        x1, y1, x2, y2 = map(int, re.findall(r"\d+", attrs["bounds"]))
        print((x1 + x2) // 2, (y1 + y2) // 2)
        break
' "$1"
}

# Full text of the first node containing $1 (to read on-screen results).
ur_ui_text() {
  ur_ui_dump | python3 -c '
import re, sys, html
needle = sys.argv[1]
for node in re.findall(r"<node [^>]*>", sys.stdin.read()):
    attrs = dict(re.findall(r"([\w-]+)=\"([^\"]*)\"", node))
    for k in ("text", "content-desc"):
        if needle in attrs.get(k, ""):
            print(html.unescape(attrs[k])); sys.exit()
' "$1"
}

# Tap a node by text, waiting up to $2 seconds for it to appear.
ur_tap_text() {
  local text="$1" wait="${2:-10}" xy i
  for i in $(seq 1 "$wait"); do
    xy="$(ur_ui_find "$text")"
    [ -n "$xy" ] && { adbx shell input tap $xy; return 0; }
    sleep 1
  done
  echo "UR_ERROR could not find '$text' on screen" >&2
  return 1
}

ur_screenshot() { # ur_screenshot <dest.png>
  mkdir -p "$(dirname "$1")"
  adbx exec-out screencap -p > "$1"
}

ur_logcat_start() { # ur_logcat_start <file>
  adbx logcat -c
  adbx logcat -v time > "$1" 2>&1 &
  UR_LOGCAT_PID=$!
}

ur_logcat_stop() { [ -n "${UR_LOGCAT_PID:-}" ] && kill "$UR_LOGCAT_PID" 2>/dev/null; UR_LOGCAT_PID=; }

# Crash markers from HANDOVER §10.2. Prints matches; returns 1 if any were found.
ur_scan_crashes() {
  local f="$1"
  if grep -E "FATAL EXCEPTION|ANR in|Fatal signal|\*\*\* \*\*\* \*\*\*|backtrace:|UR_ERROR" "$f" | grep -v "ur_scan_ignore"; then
    return 1
  fi
  return 0
}

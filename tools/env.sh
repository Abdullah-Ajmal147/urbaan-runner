#!/usr/bin/env bash
# Detects the toolchain and exports UNITY, FLUTTER, ADB, EMULATOR, JAVA_HOME, ANDROID_SDK_ROOT, FIREBASE.
# Source it:  source tools/env.sh
# The detected Unity path is cached in tools/env.local (gitignored). Delete that file to re-detect.

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export ROOT
LOCAL="$ROOT/tools/env.local"
# An explicit UNITY in the environment wins over the cached value.
if [ -z "${UNITY:-}" ] && [ -f "$LOCAL" ]; then source "$LOCAL"; fi

ur_find_unity() {
  local hub="$HOME/Unity/Hub/Editor" v
  # Prefer 6000.3 LTS, then 6000.0 (>= 58f2 per HANDOVER §8.1).
  for v in $(ls -1 "$hub" 2>/dev/null | grep -E '^6000\.3\.' | sort -V -r) \
           $(ls -1 "$hub" 2>/dev/null | grep -E '^6000\.0\.' | sort -V -r); do
    [ -x "$hub/$v/Editor/Unity" ] && { echo "$hub/$v/Editor/Unity"; return; }
  done
  command -v unity-editor || true
}

if [ -z "${UNITY:-}" ] || [ ! -x "$UNITY" ]; then
  UNITY="$(ur_find_unity)"
  [ -n "$UNITY" ] && echo "UNITY=\"$UNITY\"" > "$LOCAL"
fi
export UNITY

UNITY_ANDROID="$(dirname "$UNITY")/Data/PlaybackEngines/AndroidPlayer"
export JAVA_HOME="${JAVA_HOME_17:-$UNITY_ANDROID/OpenJDK}"
export ANDROID_SDK_ROOT="${ANDROID_SDK_ROOT:-$HOME/Android/Sdk}"
export ANDROID_HOME="$ANDROID_SDK_ROOT"
export PATH="$HOME/development/flutter/bin:$ANDROID_SDK_ROOT/platform-tools:$ANDROID_SDK_ROOT/emulator:$HOME/.local/bin:$PATH"
export FLUTTER="$(command -v flutter || true)"
export ADB="$(command -v adb || true)"
export EMULATOR="$(command -v emulator || true)"
export FIREBASE="$(command -v firebase || true)"
export ZIPALIGN="$(ls -1d "$ANDROID_SDK_ROOT"/build-tools/*/zipalign 2>/dev/null | sort -V | tail -1)"
export APP_ID="com.gamefactory.urbanrunner"

# Close-the-Editor rule (CLAUDE.md #8): refuse to run batchmode while an interactive Editor has the project open.
ur_assert_editor_closed() {
  if pgrep -af "Editor/Unity" | grep -v -- "-batchmode" | grep -qiF -- "$ROOT/unity"; then
    echo "UR_ERROR Unity Editor has this project open. Close it, then retry." >&2
    return 1
  fi
  if pgrep -af "Editor/Unity .*-batchmode" | grep -qiF -- "$ROOT/unity"; then
    echo "UR_ERROR another Unity batchmode run is using this project:" >&2
    pgrep -af "Editor/Unity .*-batchmode" | grep -iF -- "$ROOT/unity" | cut -c1-200 >&2
    return 1
  fi
}

# Run Unity batchmode: ur_unity <logname> <args...>
# Unity 6000.3 on this machine sometimes hangs during shutdown *after* it has finished and reported its result
# (KNOWN_ISSUES.md). So we watch the log for Unity's own verdict, and if the process is still alive 90 s later
# we stop it and return that verdict. A run that never reports a verdict still fails via the overall timeout.
ur_unity() {
  local log="$1"; shift
  local logfile="$ROOT/build/logs/$log.log"
  mkdir -p "$ROOT/build/logs" "$ROOT/build/test-results"
  ur_assert_editor_closed || return 1
  rm -f "$logfile"
  # Unity's bundled adb (36.x) and the SDK's adb (37.x) fight over the default server port 5037 when an
  # emulator is running (docs/decisions.md D11), so Unity's adb gets its own port.
  ANDROID_ADB_SERVER_PORT="${UR_UNITY_ADB_PORT:-5099}" \
    "$UNITY" -batchmode -projectPath "$ROOT/unity" -logFile "$logfile" "$@" &
  local pid=$! start=$SECONDS verdict="" verdict_at=0 rc=0
  local limit="${UR_BATCHMODE_TIMEOUT:-2700}"
  while kill -0 "$pid" 2>/dev/null; do
    sleep 2
    if [ -z "$verdict" ] && [ -f "$logfile" ]; then
      # `|| true`: callers may run with `set -euo pipefail`, and "no verdict yet" must not abort them.
      verdict="$(grep -aoE 'Exiting with code [0-9]+|UR_BATCH_EXIT [0-9]+' "$logfile" 2>/dev/null | head -1 | grep -oE '[0-9]+$' || true)"
      # A step can name its own final success line (UR_SUCCESS_MARKER): Unity has hung right after a
      # finished Flutter export, before our UR_BATCH_EXIT line was ever written.
      if [ -z "$verdict" ] && [ -n "${UR_SUCCESS_MARKER:-}" ] && grep -aqF "$UR_SUCCESS_MARKER" "$logfile" 2>/dev/null; then verdict=0; fi
      [ -n "$verdict" ] && verdict_at=$SECONDS
    fi
    if [ -n "$verdict" ] && [ $((SECONDS - verdict_at)) -ge "${UR_HANG_GRACE:-90}" ]; then
      echo "UR_WARN unity '$log' reported exit $verdict but hung on shutdown; stopping it" >&2
      ur_kill_tree "$pid"
      wait "$pid" 2>/dev/null || true
      return "$verdict"
    fi
    if [ $((SECONDS - start)) -ge "$limit" ]; then
      echo "UR_ERROR unity batchmode '$log' timed out after ${limit}s" >&2
      ur_kill_tree "$pid"
      wait "$pid" 2>/dev/null || true
      return 124
    fi
  done
  wait "$pid" || rc=$?
  return "$rc"
}

# Stop a process and all its descendants (Unity leaves helper processes behind when killed).
ur_kill_tree() {
  local p
  for p in $(pgrep -P "$1" || true); do ur_kill_tree "$p"; done
  kill "$1" 2>/dev/null || true
  sleep 1
  kill -9 "$1" 2>/dev/null || true
}

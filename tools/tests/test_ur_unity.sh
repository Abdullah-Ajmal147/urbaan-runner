#!/usr/bin/env bash
# Regression tests for ur_unity's shutdown watchdog (KNOWN_ISSUES K1).
# Bug: under `set -euo pipefail` (build_all.sh) the first poll's empty grep aborted the caller and orphaned Unity.
set -euo pipefail
cd "$(dirname "$0")/../.."
FAKE="$(mktemp -d)"
trap 'rm -rf "$FAKE"' EXIT

# A fake Unity: writes $FAKE_VERDICT to -logFile after 3 s, then hangs (or exits if FAKE_EXIT is set).
cat > "$FAKE/Unity" <<'FAKEUNITY'
#!/usr/bin/env bash
while [ $# -gt 0 ]; do [ "$1" = -logFile ] && LOG="$2"; shift; done
echo "starting" > "$LOG"
sleep 3
echo "$FAKE_VERDICT" >> "$LOG"
[ -n "${FAKE_EXIT:-}" ] && exit "$FAKE_EXIT"
sleep 600
FAKEUNITY
chmod +x "$FAKE/Unity"

run() { # run <expected rc> <description> -- env...
  local expected="$1" desc="$2"; shift 2
  local rc=0 start=$SECONDS
  env "$@" UNITY="$FAKE/Unity" UR_HANG_GRACE=3 bash -c 'set -euo pipefail; source tools/env.sh; ur_assert_editor_closed() { :; }; ur_unity watchdog_test' || rc=$?
  if [ "$rc" = "$expected" ]; then echo "PASS $desc (rc=$rc, $((SECONDS - start))s)"; else echo "FAIL $desc: rc=$rc expected $expected"; exit 1; fi
}

run 0 "hang after success verdict returns 0" FAKE_VERDICT="UR_BATCH_EXIT 0"
run 2 "hang after test failure returns the test's code" FAKE_VERDICT="Test run completed. Exiting with code 2 (Failed)."
run 3 "normal exit keeps the process exit code" FAKE_VERDICT="UR_BATCH_EXIT 3" FAKE_EXIT=3
run 124 "no verdict times out" FAKE_VERDICT="still working" UR_BATCHMODE_TIMEOUT=8
run 0 "hang right after the step's own success line returns 0" FAKE_VERDICT="Building project for Flutter succeeded" UR_SUCCESS_MARKER="Building project for Flutter succeeded"
echo "ALL PASS"

#!/usr/bin/env bash
# Runs every automated test layer that is available and exits non-zero on any failure (HANDOVER §14).
#   tools/test_all.sh [--skip-unity] [--skip-flutter] [--skip-firebase]
set -u
source "$(dirname "$0")/env.sh"
cd "$ROOT"

SKIP_UNITY=0; SKIP_FLUTTER=0; SKIP_FIREBASE=0
for a in "$@"; do
  case "$a" in
    --skip-unity) SKIP_UNITY=1 ;;
    --skip-flutter) SKIP_FLUTTER=1 ;;
    --skip-firebase) SKIP_FIREBASE=1 ;;
  esac
done

declare -a RESULTS=()
FAIL=0
record() { RESULTS+=("$1: $2"); [ "$2" = PASS ] || FAIL=1; }

# NUnit XML: pass only if the run completed and nothing failed.
unity_xml_ok() {
  local xml="$1"
  [ -f "$xml" ] || return 1
  local run; run="$(grep -o '<test-run [^>]*' "$xml")"
  echo "$run" | grep -q 'failed="0"' && ! echo "$run" | grep -q 'total="0"'
}

# Tooling regression tests (fast; no Unity needed).
{ bash tools/tests/test_ur_unity.sh && bash tools/tests/test_store_check.sh; } > build/logs/tools_test.log 2>&1 && record "tools" PASS || record "tools" FAIL

if [ "$SKIP_UNITY" = 0 ]; then
  if [ -z "$UNITY" ]; then
    record "unity" "MISSING (no Unity 6000.3/6000.0 found)"
  else
    rm -f build/test-results/editmode.xml build/test-results/playmode.xml
    ur_unity editmode -nographics -runTests -testPlatform EditMode -testResults "$ROOT/build/test-results/editmode.xml"
    unity_xml_ok build/test-results/editmode.xml && record "unity editmode" PASS || record "unity editmode" FAIL
    ur_unity playmode -runTests -testPlatform PlayMode -testResults "$ROOT/build/test-results/playmode.xml"
    unity_xml_ok build/test-results/playmode.xml && record "unity playmode" PASS || record "unity playmode" FAIL
  fi
fi

if [ "$SKIP_FLUTTER" = 0 ]; then
  if [ -z "$FLUTTER" ]; then
    record "flutter" "MISSING"
  else
    ( cd app && flutter pub get >/dev/null && flutter analyze ) > build/logs/flutter_analyze.log 2>&1 \
      && record "flutter analyze" PASS || record "flutter analyze" FAIL
    ( cd app && flutter test --reporter expanded ) > build/logs/flutter_test.log 2>&1 \
      && record "flutter test" PASS || record "flutter test" FAIL
  fi
fi

if [ "$SKIP_FIREBASE" = 0 ] && [ -f firebase/package.json ]; then
  if [ -z "$FIREBASE" ]; then
    record "firebase rules" "MISSING"
  else
    ( cd firebase && npm ci --silent && firebase emulators:exec --project demo-urban-runner --only firestore,auth "npm test" ) \
      > build/logs/firebase_test.log 2>&1 && record "firebase rules" PASS || record "firebase rules" FAIL
  fi
fi

echo "==== test_all ===="
printf '%s\n' "${RESULTS[@]}"
[ "$FAIL" = 0 ] && echo "ALL GREEN" || echo "FAILURES (see build/logs)"
exit "$FAIL"

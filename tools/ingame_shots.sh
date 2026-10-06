#!/usr/bin/env bash
# Design tool: renders in-game frames from the real Run scene (Editor, PlayMode) to build/shots/ingame/.
#   tools/ingame_shots.sh
set -u
source "$(dirname "$0")/env.sh"
cd "$ROOT"
ur_assert_editor_closed
rm -rf build/shots/ingame
# Regenerate materials and the Run scene from code first, so the frames show the current setup.
ur_unity configure -quit -executeMethod BuildScripts.Configure || exit 1
ur_unity shots -runTests -testPlatform PlayMode -testFilter InGameShots -testResults "$ROOT/build/test-results/shots.xml"
ls build/shots/ingame

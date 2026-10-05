#!/usr/bin/env bash
# Builds every output into build/out/ (HANDOVER §11.2 names):
#   UrbanRunner-<ver>-emulator.apk  Flutter + Unity, ARM + x86-64 (emulator only)
#   UrbanRunner-<ver>-phone.apk     Flutter + Unity, ARM only
#   UrbanRunner-<ver>.aab           store bundle, ARM only
#   UrbanRunner-<ver>-standalone-emulator.apk  Unity alone, autoplay testing
# Usage: tools/build_all.sh [emulator] [phone] [aab] [standalone]   (default: all)
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$ROOT"

VERSION="$(grep -E '^version:' app/pubspec.yaml | sed -E 's/version: *([^+]+).*/\1/')"
OUT="$ROOT/build/out"
mkdir -p "$OUT" build/logs
TARGETS=("$@"); [ ${#TARGETS[@]} -eq 0 ] && TARGETS=(emulator phone aab standalone)
want() { printf '%s\n' "${TARGETS[@]}" | grep -qx "$1"; }

export_unity() { # $1 = emulator|phone
  local flag=(); [ "$1" = emulator ] && flag=(-emulator)
  echo "== export unityLibrary ($1)"
  ur_unity "export_$1" -nographics -quit -buildTarget Android \
    -executeMethod BuildScripts.ExportForFlutter -exportPath "$ROOT/app/android/unityLibrary" "${flag[@]}"
}

if want emulator; then
  export_unity emulator
  echo "== flutter apk (emulator)"
  (cd app && flutter build apk --release --target-platform android-arm,android-arm64,android-x64) > build/logs/flutter_apk_emulator.log 2>&1
  cp app/build/app/outputs/flutter-apk/app-release.apk "$OUT/UrbanRunner-$VERSION-emulator.apk"
fi

if want phone || want aab; then
  export_unity phone
  if want phone; then
    echo "== flutter apk (phone)"
    (cd app && flutter build apk --release --target-platform android-arm,android-arm64) > build/logs/flutter_apk_phone.log 2>&1
    cp app/build/app/outputs/flutter-apk/app-release.apk "$OUT/UrbanRunner-$VERSION-phone.apk"
  fi
  if want aab; then
    echo "== flutter appbundle"
    (cd app && flutter build appbundle --release --target-platform android-arm,android-arm64) > build/logs/flutter_aab.log 2>&1
    cp app/build/app/outputs/bundle/release/app-release.aab "$OUT/UrbanRunner-$VERSION.aab"
  fi
fi

if want standalone; then
  echo "== unity standalone emulator apk"
  ur_unity standalone -nographics -quit -buildTarget Android \
    -executeMethod BuildScripts.BuildStandaloneEmulatorApk -outputPath "$OUT/UrbanRunner-$VERSION-standalone-emulator.apk"
fi

echo "==== build_all ===="
ls -la "$OUT"

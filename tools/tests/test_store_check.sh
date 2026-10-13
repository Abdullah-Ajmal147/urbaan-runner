#!/usr/bin/env bash
# Regression test for the store-build gate (D36).
# Bug: Unity's export packs x86_64, so the first store AAB shipped x86_64 libs despite --target-platform arm only.
set -euo pipefail
cd "$(dirname "$0")/../.."
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT

python3 -I - "$T" <<'PY'
import struct, sys, zipfile
def elf(align):  # minimal 64-bit ELF with one PT_LOAD segment
    h = bytearray(64 + 56); h[:5] = b"\x7fELF\x02"
    struct.pack_into("<Q", h, 0x20, 64); struct.pack_into("<HH", h, 0x36, 56, 1)
    struct.pack_into("<I", h, 64, 1); struct.pack_into("<Q", h, 64 + 48, align)
    return bytes(h)
for name, files in {
    "good": {"base/lib/arm64-v8a/libgame.so": elf(16384), "base/lib/armeabi-v7a/libgame.so": b"\x7fELF\x01"},
    "x86":  {"base/lib/arm64-v8a/libgame.so": elf(16384), "base/lib/x86_64/libgame.so": elf(16384)},
    "4kb":  {"base/lib/arm64-v8a/libgame.so": elf(4096)},
    "empty": {"base/manifest/AndroidManifest.xml": b""},
}.items():
    with zipfile.ZipFile(f"{sys.argv[1]}/{name}.aab", "w") as z:
        for n, b in files.items(): z.writestr(n, b)
PY

run() { # run <expected rc> <bundle> <description>
  local rc=0; python3 -I tools/lib/store_check.py "$T/$2.aab" > /dev/null || rc=$?
  if [ "$rc" = "$1" ]; then echo "PASS $3"; else echo "FAIL $3: rc=$rc expected $1"; exit 1; fi
}
run 0 good  "ARM-only, 16 KB aligned bundle passes"
run 1 x86   "x86_64 libraries are rejected"
run 1 4kb   "4 KB aligned arm64 library is rejected"
run 1 empty "a bundle with no native libraries is rejected"

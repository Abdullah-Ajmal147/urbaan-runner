"""Store-build gate (HANDOVER 11.1, D36): no x86_64 libraries, and every native library 16 KB page aligned.

Usage: python3 -I tools/lib/store_check.py <bundle.aab | app.apk>   (exit 1 on any problem)
"""
import struct, sys, zipfile

def load_aligns(elf):
    """p_align of each PT_LOAD segment of a 64- or 32-bit little-endian ELF."""
    if elf[:4] != b"\x7fELF":
        return []
    is64 = elf[4] == 2
    if is64:
        phoff, = struct.unpack_from("<Q", elf, 0x20); phentsize, phnum = struct.unpack_from("<HH", elf, 0x36)
    else:
        phoff, = struct.unpack_from("<I", elf, 0x1C); phentsize, phnum = struct.unpack_from("<HH", elf, 0x2A)
    out = []
    for i in range(phnum):
        off = phoff + i * phentsize
        if struct.unpack_from("<I", elf, off)[0] != 1:  # PT_LOAD
            continue
        out.append(struct.unpack_from("<Q", elf, off + 48)[0] if is64 else struct.unpack_from("<I", elf, off + 28)[0])
    return out

def check(path):
    problems = []
    with zipfile.ZipFile(path) as z:
        libs = [n for n in z.namelist() if n.endswith(".so") and "/lib/" in "/" + n]
        for n in libs:
            abi = n.split("lib/")[1].split("/")[0]
            if abi in ("x86_64", "x86"):
                problems.append(f"{n}: {abi} must not ship in store builds")
            elif abi == "arm64-v8a" and any(a < 16384 for a in load_aligns(z.read(n))):
                problems.append(f"{n}: LOAD segment not 16 KB aligned")
    return libs, problems

if __name__ == "__main__":
    libs, problems = check(sys.argv[1])
    for p in problems:
        print("FAIL", p)
    print(f"{'FAIL' if problems else 'OK'} {len(libs)} native libs checked in {sys.argv[1]}")
    sys.exit(1 if problems or not libs else 0)

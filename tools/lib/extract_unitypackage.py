#!/usr/bin/env python3
"""Extracts a .unitypackage into a Unity project without the Editor (batchmode can't run ImportPackage
synchronously). Usage: extract_unitypackage.py <package> <project_dir>. Existing files are left untouched."""
import os, sys, tarfile

pkg, project = sys.argv[1], sys.argv[2]
entries = {}
with tarfile.open(pkg, "r:gz") as tar:
    for m in tar.getmembers():
        parts = m.name.strip("./").split("/")
        if len(parts) != 2 or not m.isfile():
            continue
        entries.setdefault(parts[0], {})[parts[1]] = tar.extractfile(m).read()
written = 0
for guid, files in entries.items():
    path = files.get("pathname", b"").decode().splitlines()[0] if "pathname" in files else None
    if not path or not path.startswith("Assets/"):
        continue
    dest = os.path.join(project, path)
    if "asset" in files and not os.path.exists(dest):
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        open(dest, "wb").write(files["asset"])
        written += 1
    elif "asset" not in files:
        os.makedirs(dest, exist_ok=True)
    if "asset.meta" in files and not os.path.exists(dest + ".meta"):
        open(dest + ".meta", "wb").write(files["asset.meta"])
print(f"extracted {written} files from {os.path.basename(pkg)}")

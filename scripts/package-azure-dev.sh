#!/bin/bash

# Package a publish tree into a deployable zip, and refuse to emit one that
# Azure Linux cannot unpack.
#
#   ./scripts/package-azure-dev.sh [publish-dir] [output-zip]
#
# Defaults: ./publish  ->  $TMPDIR/rtub-dev.zip
#
# Why this exists rather than a one-line zip command:
#
# PowerShell's Compress-Archive writes entry names with BACKSLASH separators on
# Windows. The ZIP specification (APPNOTE 4.4.17.1) requires forward slashes.
# Linux unpacks such an archive into files whose names literally contain "\",
# so `wwwroot/manifest.webmanifest` arrives as a single flat file called
# `wwwroot\manifest.webmanifest`. The consequences seen on rtub-dev:
#
#   - Kudu's rsync fails on every one of them, because backslash is not a legal
#     character on the SMB-backed /home share:
#         rsync: [generator] recv_generator: failed to stat
#         "/home/site/wwwroot/LatoFont\Lato-Black.ttf": Invalid argument (22)
#   - and with run-from-package, the app starts anyway — the ~39 root-level
#     entries have no separator — but /home/site/wwwroot/wwwroot never exists,
#     so every static asset 404s and the log says "The WebRootPath was not found".
#
# The trap is that this is nearly invisible to verification: Python's zipfile
# does `filename.replace(os.sep, "/")` when READING, so on Windows a
# zipfile-based check converts the bad names and always reports "conformant".
# The verification below parses the central directory bytes instead.
#
# CI is not affected: azure/webapps-deploy packages ./publish on ubuntu-latest,
# where the separator is already "/".

set -u

PUBLISH="${1:-./publish}"
OUT="${2:-${TMPDIR:-/tmp}/rtub-dev.zip}"

if [ ! -d "$PUBLISH" ]; then
    echo "ERROR: publish directory not found: $PUBLISH" >&2
    exit 1
fi

if ! command -v python >/dev/null 2>&1; then
    echo "ERROR: python is required" >&2
    exit 1
fi

echo "packaging ${PUBLISH} -> ${OUT}"
rm -f "$OUT"

# zipfile's os.sep -> "/" rewrite is correct on WRITE, which is what makes this
# conformant on Windows and Linux alike. Entries are stored relative to the
# publish root with no "./" prefix.
python - "$PUBLISH" "$OUT" <<'PY'
import os, sys, zipfile

src, out = sys.argv[1], sys.argv[2]
n = 0
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, allowZip64=True) as z:
    for dirpath, _, names in os.walk(src):
        for name in names:
            full = os.path.join(dirpath, name)
            arc = os.path.relpath(full, src).replace(os.sep, "/")
            z.write(full, arc)
            n += 1
print(f"  wrote {n} entries")
PY

[ -s "$OUT" ] || { echo "ERROR: packaging produced nothing" >&2; exit 1; }

echo "verifying central directory"
python - "$OUT" <<'PY' || exit 1
import struct, sys

BS = chr(92)
path = sys.argv[1]

with open(path, "rb") as f:
    f.seek(0, 2)
    size = f.tell()
    tail = b""
    f.seek(max(0, size - (1 << 20)))
    tail = f.read()
    eocd = tail.rfind(b"PK\x05\x06")
    if eocd < 0:
        sys.exit("  ERROR: no end-of-central-directory record")
    cd_size = struct.unpack_from("<I", tail, eocd + 12)[0]
    cd_off = struct.unpack_from("<I", tail, eocd + 16)[0]
    f.seek(cd_off)
    cd = f.read(cd_size)

names, p = [], 0
while p + 46 <= len(cd) and cd[p:p + 4] == b"PK\x01\x02":
    nlen, elen, clen = struct.unpack_from("<HHH", cd, p + 28)
    names.append(cd[p + 46:p + 46 + nlen].decode("utf-8", "replace"))
    p += 46 + nlen + elen + clen

bad = [n for n in names if BS in n]
print(f"  entries: {len(names)}   backslash entries: {len(bad)}")

rc = 0
if bad:
    print("  ERROR: non-conformant separators, Azure Linux cannot unpack this:")
    for n in bad[:5]:
        print(f"      {n!r}")
    rc = 1

for required in ("wwwroot/manifest.webmanifest", "wwwroot/service-worker.js"):
    if required in names:
        print(f"  OK  {required}")
    else:
        print(f"  ERROR: {required} missing")
        rc = 1

for lib in ("libQuestPdfSkia.so", "libe_sqlite3.so"):
    if lib in names or f"runtimes/linux-x64/native/{lib}" in names:
        print(f"  OK  {lib}")
    else:
        print(f"  ERROR: {lib} missing")
        rc = 1

sys.exit(rc)
PY

echo "OK: $OUT"

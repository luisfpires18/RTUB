#!/bin/bash

# Release building blocks for Deploy • DEV and Deploy • PROD. Local only: no Azure, no network.
#
#   release.sh version                          print the root VERSION, validated byte for byte
#   release.sh bump-check <previous> <new>      new must be a higher SemVer than previous
#                                               (previous "-" = no earlier release)
#   release.sh package <publish-dir> <zip>      zip a publish tree, then verify the zip
#   release.sh verify <zip>                     the deploy guards, run on the ZIP itself
#   release.sh manifest <zip> <version> <commit> <run-id> <migrations-dir> <out.json>
#                                               RELEASE_VERSION_ENDPOINT=false for a build made
#                                               before GET /api/version existed (the 1.0.0 capture)
#   release.sh schema-gate <live.json> <target.json>
#                                               exit 3 and list them if the live release has
#                                               migrations the target does not know
#   release.sh --self-test
#
# The version is SemVer MAJOR.MINOR.PATCH from the root VERSION file - the number you say out
# loud ("deploy 2.0.1", "roll back to 2.0.0"). Commit SHA and SHA-256 are kept alongside it
# in release.json and verified on every deploy. See docs/release-and-rollback.md.

set -u

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SEMVER='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'

PY=""
for candidate in python3 python; do
    if command -v "$candidate" >/dev/null 2>&1 &&
        "$candidate" -c 'import sys; sys.exit(0 if sys.version_info >= (3, 8) else 1)' >/dev/null 2>&1; then
        PY=$candidate
        break
    fi
done

die() { echo "ERROR: $*" >&2; exit 1; }
need_python() { [ -n "$PY" ] || die "python 3.8+ is required"; }

# ---------------------------------------------------------------- version

cmd_version() {
    local file="${VERSION_FILE:-$ROOT/VERSION}"
    [ -f "$file" ] || die "no VERSION file at $file"

    # Exactly MAJOR.MINOR.PATCH plus at most one trailing LF. $(< file) drops trailing LFs and
    # keeps everything else, so CR, a BOM, spaces, a second line, prerelease and build
    # metadata all fail the pattern; the size check catches extra blank lines.
    local content size
    content=$(< "$file")
    [[ "$content" =~ $SEMVER ]] ||
        die "VERSION must be MAJOR.MINOR.PATCH (no leading zeros, prerelease or metadata, one line)"
    size=$(wc -c < "$file" | tr -d ' ')
    if [ "$size" -ne "${#content}" ] &&
        { [ "$size" -ne $(( ${#content} + 1 )) ] || [ "$(tail -c 1 "$file" | od -An -tx1 | tr -d ' ')" != "0a" ]; }; then
        die "VERSION must be a single line"
    fi
    echo "$content"
}

# 0 when $2 > $1 by SemVer precedence. Numeric per component - 1.10.0 > 1.9.0.
semver_gt() {
    local a b i
    IFS=. read -r -a a <<< "$1"
    IFS=. read -r -a b <<< "$2"
    for i in 0 1 2; do
        (( 10#${b[$i]} > 10#${a[$i]} )) && return 0
        (( 10#${b[$i]} < 10#${a[$i]} )) && return 1
    done
    return 1
}

cmd_bump_check() {
    [ $# -eq 2 ] || { echo "usage: release.sh bump-check <previous|-> <new>" >&2; exit 2; }
    local previous=$1 new=$2
    [[ "$new" =~ $SEMVER ]] || { echo "ERROR: '$new' is not MAJOR.MINOR.PATCH" >&2; exit 2; }
    if [ "$previous" = "-" ] || [ -z "$previous" ]; then
        echo "OK: $new is the first versioned release"
        return 0
    fi
    [[ "$previous" =~ $SEMVER ]] || { echo "ERROR: previous version '$previous' is not MAJOR.MINOR.PATCH" >&2; exit 2; }
    if semver_gt "$previous" "$new"; then
        echo "OK: $previous -> $new"
        return 0
    fi
    echo "ERROR: VERSION must go up. $previous is already released or pending; bump VERSION above it (PATCH for fixes, MINOR for features, MAJOR for breaking changes)." >&2
    exit 1
}

# ---------------------------------------------------------------- package + verify

cmd_package() {
    [ $# -eq 2 ] || die "usage: release.sh package <publish-dir> <out.zip>"
    local publish=$1 out=$2
    need_python
    [ -d "$publish" ] || die "publish directory not found: $publish"
    rm -f "$out"
    mkdir -p "$(dirname "$out")"

    # zipfile's os.sep -> "/" rewrite is correct on WRITE, which is what keeps entry names
    # conformant on Windows too (Compress-Archive writes backslashes). Sorted, so the entry
    # order does not depend on the file system.
    "$PY" - "$publish" "$out" <<'PY' || exit 1
import os, sys, zipfile
src, out = sys.argv[1], sys.argv[2]
entries = []
for dirpath, dirnames, filenames in os.walk(src):
    for name in filenames:
        full = os.path.join(dirpath, name)
        entries.append((os.path.relpath(full, src).replace(os.sep, "/"), full))
entries.sort()
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, allowZip64=True) as z:
    for arc, full in entries:
        z.write(full, arc)
print(f"  packaged {len(entries)} entries")
PY
    cmd_verify "$out"
}

# The deploy guards, run against the ZIP that will be archived and deployed - not against the
# publish folder it came from.
cmd_verify() {
    [ $# -eq 1 ] || die "usage: release.sh verify <zip>"
    need_python
    [ -s "$1" ] || die "no zip at $1"
    "$PY" - "$1" "${MIN_WWWROOT_FILES:-500}" <<'PY'
import struct, sys, zipfile

path, min_wwwroot = sys.argv[1], int(sys.argv[2])
BS = chr(92)

# orig_filename is the name as stored. .filename (and namelist()) rewrite os.sep on Windows,
# which would hide exactly the backslash names this guard exists to catch.
try:
    z = zipfile.ZipFile(path)
except zipfile.BadZipFile:
    sys.exit("  ERROR: not a zip archive")
with z:
    infos = z.infolist()
    raw = [i.orig_filename for i in infos]

    problems = []
    bad = [n for n in raw if BS in n]
    if bad:
        problems.append(f"{len(bad)} entries use backslash separators, e.g. {bad[0]!r} - Azure Linux cannot unpack them")
    unsafe = [n for n in raw if n.startswith(("/", "./")) or ".." in n.split("/")]
    if unsafe:
        problems.append(f"unsafe entry path {unsafe[0]!r}")

    sizes = {i.filename: i.file_size for i in infos}
    for required in ("RTUB.dll", "RTUB.deps.json", "RTUB.runtimeconfig.json",
                     "wwwroot/manifest.webmanifest", "wwwroot/service-worker.js", "wwwroot/offline.html"):
        if sizes.get(required, 0) == 0:
            problems.append(f"{required} is missing or empty")

    # QuestPDF loads libQuestPdfSkia.so at Program.cs startup, before any service exists; a
    # missing or wrong-architecture copy aborts the container with exit 134.
    for lib in ("libQuestPdfSkia.so", "libe_sqlite3.so"):
        name = next((n for n in (lib, f"runtimes/linux-x64/native/{lib}") if sizes.get(n, 0) > 0), None)
        if name is None:
            problems.append(f"{lib} is missing or empty")
            continue
        with z.open(name) as so:
            head = so.read(20)
        machine = struct.unpack_from("<H", head, 18)[0] if len(head) >= 20 else None
        if head[:4] != b"\x7fELF" or head[4] != 2 or head[5] != 1 or machine != 0x3E:
            problems.append(f"{name} is not a linux-x64 (ELF64, x86-64) shared object")

    wwwroot = sum(1 for n in sizes if n.startswith("wwwroot/") and not n.endswith("/"))
    if wwwroot < min_wwwroot:
        problems.append(f"only {wwwroot} files under wwwroot/ - expected at least {min_wwwroot}; the tree looks truncated")

print(f"  entries: {len(raw)}   wwwroot files: {wwwroot}")
for problem in problems:
    print(f"  ERROR: {problem}")
sys.exit(1 if problems else 0)
PY
    local rc=$?
    [ "$rc" -eq 0 ] && echo "OK: $1" || echo "REFUSED: $1 is not deployable" >&2
    return "$rc"
}

# ---------------------------------------------------------------- manifest + schema gate

cmd_manifest() {
    [ $# -eq 6 ] || die "usage: release.sh manifest <zip> <version> <commit> <run-id> <migrations-dir> <out.json>"
    need_python
    "$PY" - "$@" "${RELEASE_VERSION_ENDPOINT:-true}" <<'PY'
import datetime, hashlib, json, os, re, sys

zip_path, version, commit, run_id, migrations_dir, out, endpoint = sys.argv[1:8]
if endpoint not in ("true", "false"):
    sys.exit(f"ERROR: RELEASE_VERSION_ENDPOINT must be true or false, not {endpoint!r}")
if not re.fullmatch(r"(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)", version):
    sys.exit(f"ERROR: version {version!r} is not MAJOR.MINOR.PATCH")
if not re.fullmatch(r"[0-9a-f]{40}", commit):
    sys.exit(f"ERROR: commit {commit!r} is not a full 40-character SHA")

digest = hashlib.sha256()
with open(zip_path, "rb") as f:
    for chunk in iter(lambda: f.read(1 << 20), b""):
        digest.update(chunk)

# The IDs EF Core applies are the [Migration("...")] attributes, not the file names: a migration
# class without the attribute is never discovered, so it must not appear here either.
ids = set()
for name in os.listdir(migrations_dir):
    if name.endswith(".cs"):
        with open(os.path.join(migrations_dir, name), encoding="utf-8-sig") as f:
            ids.update(re.findall(r'\[Migration\("([^"]+)"\)\]', f.read()))
migrations = sorted(ids)

manifest = {
    "format": 1,
    "version": version,
    "commit": commit,
    "shortCommit": commit[:7],
    "artifact": f"rtub-{version}.zip",
    "sha256": digest.hexdigest(),
    "sizeBytes": os.path.getsize(zip_path),
    "githubRunId": run_id,
    "builtAtUtc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "versionEndpoint": endpoint == "true",
    "migrations": migrations,
    "newestMigration": migrations[-1] if migrations else None,
}
with open(out, "w", encoding="utf-8", newline="\n") as f:
    json.dump(manifest, f, indent=2)
    f.write("\n")
print(f"  release.json: {version} {commit[:7]} sha256={manifest['sha256'][:12]}... "
      f"{manifest['sizeBytes']} bytes, {len(migrations)} migrations")
PY
}

cmd_schema_gate() {
    [ $# -eq 2 ] || { echo "usage: release.sh schema-gate <live.json> <target.json>" >&2; exit 2; }
    need_python
    "$PY" - "$1" "$2" <<'PY'
import json, sys
try:
    live = json.load(open(sys.argv[1], encoding="utf-8"))
    target = json.load(open(sys.argv[2], encoding="utf-8"))
    live_ids, target_ids = set(live["migrations"]), set(target["migrations"])
except Exception as e:
    print(f"ERROR: cannot compare migrations: {e}", file=sys.stderr)
    sys.exit(2)
ahead = sorted(live_ids - target_ids)
if not ahead:
    print(f"OK: {target['version']} knows every migration {live['version']} applied")
    sys.exit(0)
print(f"The live release {live['version']} applied {len(ahead)} migration(s) that {target['version']} does not know:")
for m in ahead:
    print(f"  {m}")
print("They stay applied: an app rollback never changes the schema. This is safe only if each one")
print("followed the N-1 (expand/contract) rule. Otherwise restore the database instead - see")
print("docs/release-and-rollback.md, 'Restoring the database'.")
sys.exit(3)
PY
}

# ---------------------------------------------------------------- self-test

self_test() {
    need_python
    local tmp rc=0
    tmp=$(mktemp -d)
    trap 'rm -rf "$tmp"' EXIT

    expect() { # label wanted-exit command...
        local label=$1 want=$2
        shift 2
        local out got
        out=$("$@" 2>&1)
        got=$?
        if [ "$got" -eq "$want" ]; then
            echo "  OK    $label"
        else
            echo "  FAIL  $label (exit $got, wanted $want)"
            printf '%s\n' "$out" | sed 's/^/          /'
            rc=1
        fi
    }

    # Fixture publish trees. Fake natives carry just an ELF header - enough for the guard.
    "$PY" - "$tmp" <<'PY'
import os, struct, sys, zipfile
root = sys.argv[1]

def elf(machine=0x3E, cls=2):
    return b"\x7fELF" + bytes([cls, 1, 1]) + b"\0" * 11 + struct.pack("<H", machine) + b"\0" * 44

def tree(name, drop=(), empty=(), libs=None, wwwroot=4, extra=()):
    base = os.path.join(root, name)
    files = {
        "RTUB.dll": b"MZ", "RTUB.deps.json": b"{}", "RTUB.runtimeconfig.json": b"{}",
        "wwwroot/manifest.webmanifest": b"{}", "wwwroot/service-worker.js": b"//",
        "wwwroot/offline.html": b"<p>",
        "libQuestPdfSkia.so": elf(), "runtimes/linux-x64/native/libe_sqlite3.so": elf(),
    }
    for i in range(wwwroot):
        files[f"wwwroot/css/{i}.css"] = b"a{}"
    files.update(libs or {})
    for k in drop:
        files.pop(k)
    for k in empty:
        files[k] = b""
    for k, v in list(files.items()) + list(extra):
        p = os.path.join(base, *k.split("/"))
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "wb") as f:
            f.write(v)

tree("good")
tree("no-questpdf", drop=["libQuestPdfSkia.so"])
tree("empty-sqlite", empty=["runtimes/linux-x64/native/libe_sqlite3.so"])
tree("not-elf", libs={"libQuestPdfSkia.so": b"MZ not an ELF at all, padded out to twenty+ bytes"})
tree("arm64", libs={"libQuestPdfSkia.so": elf(machine=0xB7)})
tree("no-sw", drop=["wwwroot/service-worker.js"])
tree("no-dll", drop=["RTUB.dll"])
tree("truncated", wwwroot=0)

# Archives no packager of ours would write: built directly.
def raw_zip(name, entries):
    with zipfile.ZipFile(os.path.join(root, name), "w") as z:
        for arc, data in entries:
            info = zipfile.ZipInfo("placeholder")
            info.filename = arc  # after __init__, which rewrites a backslash on Windows
            z.writestr(info, data)
# Otherwise valid (enough wwwroot files too), so only the one defect can refuse them.
base = [("RTUB.dll", b"MZ"), ("RTUB.deps.json", b"{}"), ("RTUB.runtimeconfig.json", b"{}"),
        ("wwwroot/manifest.webmanifest", b"{}"), ("wwwroot/service-worker.js", b"//"),
        ("wwwroot/offline.html", b"<p>"), ("libQuestPdfSkia.so", elf()), ("libe_sqlite3.so", elf())]
base += [(f"wwwroot/css/{i}.css", b"a{}") for i in range(4)]
raw_zip("valid.zip", base)
raw_zip("backslash.zip", base + [("wwwroot" + chr(92) + "a.css", b"a{}")])
raw_zip("zipslip.zip", base + [("../evil.sh", b"#!")])

mig = os.path.join(root, "migrations")
os.makedirs(mig)
for fname, body in {
    "20250101000000_First.Designer.cs": '[DbContext(typeof(X))]\n[Migration("20250101000000_First")]',
    "20250101000000_First.cs": "partial class First : Migration {}",
    "20250202000000_Second.cs": '[DbContext(typeof(X))]\n    [Migration("20250202000000_Second")]',
    "20250303000000_Orphan.cs": "partial class Orphan : Migration {} // no attribute: EF never applies it",
    "ApplicationDbContextModelSnapshot.cs": "[DbContext(typeof(X))]",
}.items():
    with open(os.path.join(mig, fname), "w") as f:
        f.write(body)
PY

    echo "version"
    local v="$tmp/VERSION"
    printf '1.1.2\n' > "$v";   expect "1.1.2 + LF"                0 env VERSION_FILE="$v" bash "$0" version
    printf '1.1.2' > "$v";     expect "1.1.2 without LF"          0 env VERSION_FILE="$v" bash "$0" version
    printf '0.0.0\n' > "$v";   expect "0.0.0"                     0 env VERSION_FILE="$v" bash "$0" version
    printf '1.1.2\r\n' > "$v"; expect "CRLF refused"              1 env VERSION_FILE="$v" bash "$0" version
    printf ' 1.1.2\n' > "$v";  expect "leading space refused"     1 env VERSION_FILE="$v" bash "$0" version
    printf '1.01.2\n' > "$v";  expect "leading zero refused"      1 env VERSION_FILE="$v" bash "$0" version
    printf '1.1\n' > "$v";     expect "two parts refused"         1 env VERSION_FILE="$v" bash "$0" version
    printf '1.1.2-rc.1\n' > "$v"; expect "prerelease refused"     1 env VERSION_FILE="$v" bash "$0" version
    printf '1.1.2+abc\n' > "$v";  expect "build metadata refused" 1 env VERSION_FILE="$v" bash "$0" version
    printf '1.1.2\n1.1.3\n' > "$v"; expect "two lines refused"    1 env VERSION_FILE="$v" bash "$0" version
    printf '\xef\xbb\xbf1.1.2\n' > "$v"; expect "BOM refused"     1 env VERSION_FILE="$v" bash "$0" version
    : > "$v";                  expect "empty refused"             1 env VERSION_FILE="$v" bash "$0" version
    expect "missing refused" 1 env VERSION_FILE="$tmp/nope" bash "$0" version
    local out
    printf '1.1.2\n' > "$v"; out=$(VERSION_FILE="$v" bash "$0" version)
    [ "$out" = "1.1.2" ] && echo "  OK    prints exactly 1.1.2" || { echo "  FAIL  printed '$out'"; rc=1; }

    echo "bump-check"
    expect "first release"          0 bash "$0" bump-check - 1.1.2
    expect "patch up"               0 bash "$0" bump-check 1.1.1 1.1.2
    expect "minor up"               0 bash "$0" bump-check 1.1.2 1.2.0
    expect "major up"               0 bash "$0" bump-check 1.2.0 2.0.0
    expect "numeric not lexical"    0 bash "$0" bump-check 1.9.0 1.10.0
    expect "unchanged refused"      1 bash "$0" bump-check 1.1.2 1.1.2
    expect "down refused"           1 bash "$0" bump-check 1.2.0 1.1.9
    expect "lexical trap refused"   1 bash "$0" bump-check 1.10.0 1.9.0
    expect "bad new version"        2 bash "$0" bump-check 1.1.1 v1.1.2
    expect "bad previous version"   2 bash "$0" bump-check 1.1 1.1.2

    echo "package + verify (MIN_WWWROOT_FILES=5)"
    export MIN_WWWROOT_FILES=5
    expect "good tree packages"          0 bash "$0" package "$tmp/good" "$tmp/out/good.zip"
    expect "missing libQuestPdfSkia"     1 bash "$0" package "$tmp/no-questpdf" "$tmp/out/a.zip"
    expect "empty libe_sqlite3"          1 bash "$0" package "$tmp/empty-sqlite" "$tmp/out/b.zip"
    expect "native not ELF"              1 bash "$0" package "$tmp/not-elf" "$tmp/out/c.zip"
    expect "native is arm64"             1 bash "$0" package "$tmp/arm64" "$tmp/out/d.zip"
    expect "missing service worker"      1 bash "$0" package "$tmp/no-sw" "$tmp/out/e.zip"
    expect "missing RTUB.dll"            1 bash "$0" package "$tmp/no-dll" "$tmp/out/f.zip"
    expect "truncated wwwroot"           1 bash "$0" package "$tmp/truncated" "$tmp/out/g.zip"
    expect "control: hand-built zip is valid" 0 bash "$0" verify "$tmp/valid.zip"
    expect "backslash entry names"       1 bash "$0" verify "$tmp/backslash.zip"
    expect "path traversal entry"        1 bash "$0" verify "$tmp/zipslip.zip"
    expect "not a zip"                   1 bash "$0" verify "$tmp/migrations/20250101000000_First.cs"
    unset MIN_WWWROOT_FILES

    echo "manifest + schema-gate"
    local sha="0123456789abcdef0123456789abcdef01234567"
    expect "manifest writes" 0 bash "$0" manifest "$tmp/out/good.zip" 1.1.2 "$sha" 42 "$tmp/migrations" "$tmp/target.json"
    expect "manifest refuses short sha" 1 bash "$0" manifest "$tmp/out/good.zip" 1.1.2 abc1234 42 "$tmp/migrations" "$tmp/x.json"
    expect "manifest refuses bad version" 1 bash "$0" manifest "$tmp/out/good.zip" 1.1 "$sha" 42 "$tmp/migrations" "$tmp/x.json"
    expect "legacy manifest writes" 0 env RELEASE_VERSION_ENDPOINT=false bash "$0" manifest "$tmp/out/good.zip" 1.1.1 "$sha" manual "$tmp/migrations" "$tmp/legacy.json"
    if "$PY" -c 'import json,sys; m=json.load(open(sys.argv[1])); sys.exit(0 if m["versionEndpoint"] is False else 1)' "$tmp/legacy.json" &&
       "$PY" -c 'import json,sys; m=json.load(open(sys.argv[1])); sys.exit(0 if m["versionEndpoint"] is True else 1)' "$tmp/target.json"; then
        echo "  OK    versionEndpoint is true by default, false for a legacy capture"
    else
        echo "  FAIL  versionEndpoint flag"; rc=1
    fi
    if "$PY" - "$tmp/target.json" "$tmp/out/good.zip" "$sha" <<'PY'
import hashlib, json, os, sys
m = json.load(open(sys.argv[1]))
want = hashlib.sha256(open(sys.argv[2], "rb").read()).hexdigest()
assert m["version"] == "1.1.2" and m["commit"] == sys.argv[3] and m["shortCommit"] == sys.argv[3][:7], m
assert m["sha256"] == want and m["sizeBytes"] == os.path.getsize(sys.argv[2]), m
assert m["githubRunId"] == "42" and m["artifact"] == "rtub-1.1.2.zip", m
assert m["migrations"] == ["20250101000000_First", "20250202000000_Second"], m["migrations"]
assert m["newestMigration"] == "20250202000000_Second", m
PY
    then echo "  OK    manifest fields (attribute IDs only, orphan class excluded)"; else echo "  FAIL  manifest fields"; rc=1; fi

    "$PY" - "$tmp" <<'PY'
import json, os, sys
root = sys.argv[1]
def w(name, version, migrations):
    json.dump({"version": version, "migrations": migrations}, open(os.path.join(root, name), "w"))
w("live-same.json", "1.1.2", ["20250101000000_First", "20250202000000_Second"])
w("live-older.json", "1.1.1", ["20250101000000_First"])
w("live-ahead.json", "1.2.0", ["20250101000000_First", "20250202000000_Second", "20250404000000_Third"])
open(os.path.join(root, "broken.json"), "w").write("{not json")
PY
    expect "redeploy same release"          0 bash "$0" schema-gate "$tmp/live-same.json" "$tmp/target.json"
    expect "forward release"                0 bash "$0" schema-gate "$tmp/live-older.json" "$tmp/target.json"
    expect "rollback behind live schema"    3 bash "$0" schema-gate "$tmp/live-ahead.json" "$tmp/target.json"
    expect "unreadable manifest"            2 bash "$0" schema-gate "$tmp/broken.json" "$tmp/target.json"
    expect "missing manifest"               2 bash "$0" schema-gate "$tmp/nope.json" "$tmp/target.json"
    out=$(bash "$0" schema-gate "$tmp/live-ahead.json" "$tmp/target.json" 2>&1)
    [[ "$out" == *"20250404000000_Third"* ]] && echo "  OK    names the migration that stays applied" \
        || { echo "  FAIL  schema-gate did not name the migration"; rc=1; }

    [ "$rc" -eq 0 ] && echo "release self-test PASSED" || echo "release self-test FAILED"
    exit "$rc"
}

case "${1:-}" in
    version)      shift; cmd_version "$@" ;;
    bump-check)   shift; cmd_bump_check "$@" ;;
    package)      shift; cmd_package "$@" ;;
    verify)       shift; cmd_verify "$@" ;;
    manifest)     shift; cmd_manifest "$@" ;;
    schema-gate)  shift; cmd_schema_gate "$@" ;;
    --self-test)  self_test ;;
    *) sed -n '3,21p' "$0"; exit 2 ;;
esac

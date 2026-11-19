#!/bin/bash

# Release building blocks for Deploy • DEV and Deploy • PROD. No Azure; the only network call is
# previous-version's read-only GitHub API request (gh).
#
#   release.sh version                          print the root VERSION, validated byte for byte
#   release.sh previous-version <owner/repo> <ref>
#                                               print VERSION as of <ref> on GitHub; "-" when
#                                               that commit has none (first versioned release)
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
#   release.sh notes-check <version> <notes-dir> [--optional]
#                                               the member-facing release notes of <version>
#                                               (task 083, #264): releases/<version>.json is
#                                               valid, names that version, and holds short
#                                               plain-text pt-PT items; exit 5 when it fails.
#                                               --optional (090): a missing file is fine (no
#                                               notes = no dialog); a present file must pass
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

# shellcheck source=release-checks.sh
source "$ROOT/scripts/release-checks.sh"

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

# The previous release's VERSION, read from GitHub. "-" only when GitHub answers 404 for VERSION
# at a commit that exists: legacy master (before 2.0.0) has no VERSION, which makes this the first
# versioned release. Every other failure - auth, rate limit, network, an unknown commit or
# repository, an empty file - exits 1, so a failed read can never pass as a first release.
cmd_previous_version() {
    [ $# -eq 2 ] || { echo "usage: release.sh previous-version <owner/repo> <ref>" >&2; exit 2; }
    local repo=$1 ref=$2 body err content not_found='HTTP 404([^0-9]|$)'
    body=$(mktemp)
    if err=$(gh api -H "Accept: application/vnd.github.raw+json" \
            "repos/$repo/contents/VERSION?ref=$ref" 2>&1 >"$body"); then
        content=$(tr -d '\r\n' < "$body")
        rm -f "$body"
        [ -n "$content" ] || die "VERSION at $ref is empty"
        echo "$content"
        return 0
    fi
    rm -f "$body"
    [[ "$err" =~ $not_found ]] || { echo "$err" >&2; die "cannot read VERSION at $ref in $repo"; }
    gh api --silent "repos/$repo/commits/$ref" >/dev/null 2>&1 ||
        die "VERSION lookup answered 404 and $repo has no commit $ref either; not a first release"
    echo "No VERSION at $ref: first versioned release" >&2
    echo "-"
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
    # Floor for "the tree looks truncated". A healthy linux-x64 publish has 260 wwwroot files since 042 (portal 206):
    # 042 dropped the Identity UI package, whose 187 unused Bootstrap / jQuery files had kept it above 400 (452 before).
    "$PY" - "$1" "${MIN_WWWROOT_FILES:-200}" <<'PY'
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

case "${1:-}" in
    version)      shift; cmd_version "$@" ;;
    previous-version) shift; cmd_previous_version "$@" ;;
    bump-check)   shift; cmd_bump_check "$@" ;;
    package)      shift; cmd_package "$@" ;;
    verify)       shift; cmd_verify "$@" ;;
    manifest)     shift; cmd_manifest "$@" ;;
    schema-gate)  shift; cmd_schema_gate "$@" ;;
    notes-check)  shift; cmd_notes_check "$@" ;;
    --self-test)  source "$ROOT/scripts/release-self-test.sh"; self_test ;;
    *) sed -n '3,24p' "$0"; exit 2 ;;
esac

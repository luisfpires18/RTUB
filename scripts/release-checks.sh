#!/bin/bash

# notes-check and schema-gate of release.sh (sourced by it at start), kept apart so each file stays readable.
# Uses release.sh's PY, die and need_python.

# The release notes members see after a deploy (docs/release-and-rollback.md → Release notes). Fails
# before anything is packaged: a release without its notes, or with a stale or malformed file, is not
# a release. Plain text only - the client renders the strings as text, never as HTML - and short.
cmd_notes_check() {
    [ $# -eq 2 ] || { [ $# -eq 3 ] && [ "$3" = "--optional" ]; } || die "usage: release.sh notes-check <version> <notes-dir> [--optional]"
    need_python
    "$PY" - "$@" <<'PY'
import json, os, re, sys

version, notes_dir = sys.argv[1:3]
optional = len(sys.argv) > 3 and sys.argv[3] == "--optional"
SEMVER = r"(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)"
if not re.fullmatch(SEMVER, version):
    sys.exit(f"ERROR: version {version!r} is not MAJOR.MINOR.PATCH")
path = os.path.join(notes_dir, f"{version}.json")
if not os.path.isfile(path):
    if optional:
        print(f"  no release notes for {version} ({path} absent): members see no dialog for this release")
        sys.exit(0)
    print(f"ERROR: no release notes for {version}: write {path} (see docs/release-and-rollback.md)", file=sys.stderr)
    sys.exit(5)

def fail(reason):
    print(f"ERROR: {path}: {reason}", file=sys.stderr)
    sys.exit(5)

try:
    with open(path, encoding="utf-8") as f:
        raw = f.read()
    if raw.startswith("﻿"):
        fail("must not start with a byte-order mark")
    notes = json.loads(raw)
except (OSError, ValueError) as e:
    fail(f"not valid JSON ({e})")

if not isinstance(notes, dict):
    fail("must be a JSON object")
extra = set(notes) - {"version", "title", "items"}
if extra:
    fail(f"unknown fields: {', '.join(sorted(extra))}")
if notes.get("version") != version:
    fail(f"\"version\" is {notes.get('version')!r}, the file is for {version}")
title = notes.get("title")
if not isinstance(title, str) or not title.strip():
    fail("\"title\" must be a non-empty string")
if len(title) > 80:
    fail("\"title\" must be at most 80 characters")
items = notes.get("items")
if not isinstance(items, list) or not items:
    fail("\"items\" must be a non-empty list of strings")
if len(items) > 12:
    fail("\"items\" must hold at most 12 entries: keep it brief")
UNSAFE = re.compile(r"[<>]|https?://|javascript:|[\x00-\x08\x0b\x0c\x0e-\x1f]")
PLACEHOLDER = re.compile(r"\b(TODO|TBD|lorem|xxx)\b", re.IGNORECASE)
if UNSAFE.search(title):
    fail("\"title\" must be plain text")
for i, item in enumerate(items, 1):
    if not isinstance(item, str) or not item.strip():
        fail(f"item {i} must be a non-empty string")
    if len(item) > 240:
        fail(f"item {i} must be at most 240 characters")
    if "\n" in item:
        fail(f"item {i} must be one line")
    if UNSAFE.search(item):
        fail(f"item {i} must be plain text: no markup, links or control characters")
    if PLACEHOLDER.search(item):
        fail(f"item {i} looks like a placeholder")
print(f"  release notes {version}: \"{title}\", {len(items)} items")
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
# Every migration up to SQUASHED_THROUGH is inside BASELINE (src/RTUB.Application/Data/MigrationBaseline.cs holds the
# same two ids), so a target with the Baseline knows them. EF Core orders migration ids as plain strings; so does "<=".
BASELINE = "20261118134743_Baseline"
SQUASHED_THROUGH = "20261117191014_AddAprenderViewsAndStars"
unknown = live_ids - target_ids
squashed = {m for m in unknown if BASELINE in target_ids and m <= SQUASHED_THROUGH}
ahead = sorted(unknown - squashed)
if not ahead:
    through = f" ({len(squashed)} of them through {BASELINE})" if squashed else ""
    print(f"OK: {target['version']} knows every migration {live['version']} applied{through}")
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

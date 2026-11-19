#!/bin/bash

# The self-test of release.sh, kept apart because it is longer than the script it checks. release.sh sources this
# file for "release.sh --self-test", so $0, ROOT, PY, die and need_python are release.sh's own.
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
# The squash: the old chain (one id has a short date, as in the real one) against a release with the Baseline.
chain = ["20251026142327_Db", "20260204_AddBattlePendingRewardsFields", "20261117191014_AddAprenderViewsAndStars"]
squashed = ["20261118134743_Baseline", "20261118140141_UserXpLevelDefaults"]
w("chain.json", "2.3.12", chain)
w("squashed.json", "2.3.13", squashed)
w("chain-later.json", "2.3.12", chain + ["20261118000000_AfterTheSquashPoint"])
w("no-baseline.json", "2.3.13", ["20261118140141_UserXpLevelDefaults"])
PY
    expect "redeploy same release"          0 bash "$0" schema-gate "$tmp/live-same.json" "$tmp/target.json"
    expect "forward release"                0 bash "$0" schema-gate "$tmp/live-older.json" "$tmp/target.json"
    expect "rollback behind live schema"    3 bash "$0" schema-gate "$tmp/live-ahead.json" "$tmp/target.json"
    expect "unreadable manifest"            2 bash "$0" schema-gate "$tmp/broken.json" "$tmp/target.json"
    expect "missing manifest"               2 bash "$0" schema-gate "$tmp/nope.json" "$tmp/target.json"
    expect "forward over the squash"        0 bash "$0" schema-gate "$tmp/chain.json" "$tmp/squashed.json"
    expect "rollback behind the Baseline"   3 bash "$0" schema-gate "$tmp/squashed.json" "$tmp/chain.json"
    expect "a migration after the squash point is not in the Baseline" 3 bash "$0" schema-gate "$tmp/chain-later.json" "$tmp/squashed.json"
    expect "no Baseline, nothing is squashed" 3 bash "$0" schema-gate "$tmp/chain.json" "$tmp/no-baseline.json"
    out=$(bash "$0" schema-gate "$tmp/live-ahead.json" "$tmp/target.json" 2>&1)
    [[ "$out" == *"20250404000000_Third"* ]] && echo "  OK    names the migration that stays applied" \
        || { echo "  FAIL  schema-gate did not name the migration"; rc=1; }

    echo "notes-check, as CI and Deploy • PROD run it"
    "$PY" - "$tmp" <<'PY'
import json, os, sys
root = os.path.join(sys.argv[1], "notes"); os.makedirs(root)
def w(name, data):
    with open(os.path.join(root, name), "w", encoding="utf-8") as f:
        f.write(data if isinstance(data, str) else json.dumps(data, ensure_ascii=False))
w("1.1.2.json", {"version": "1.1.2", "title": "Nova atualização 1.1.2", "items": ["Podes agora ver as novidades.", "Corrigimos a Galeria."]})
w("1.1.3.json", {"version": "1.1.2", "title": "Nova atualização 1.1.3", "items": ["x"]})
w("1.1.4.json", {"version": "1.1.4", "title": "Nova atualização 1.1.4", "items": []})
w("1.1.5.json", {"version": "1.1.5", "title": "", "items": ["x"]})
w("1.1.6.json", {"version": "1.1.6", "title": "T", "items": ["Vê em <a href=x>aqui</a>."]})
w("1.1.7.json", {"version": "1.1.7", "title": "T", "items": ["Mais em https://rtub.pt."]})
w("1.1.8.json", {"version": "1.1.8", "title": "T", "items": ["TODO escrever"]})
w("1.1.9.json", "{not json")
w("1.2.0.json", {"version": "1.2.0", "title": "T", "items": ["ok"], "html": "<b>"})
w("1.2.1.json", {"version": "1.2.1", "title": "T", "items": ["x" * 241]})
PY
    expect "notes present and valid"            0 bash "$0" notes-check 1.1.2 "$tmp/notes"
    expect "notes missing refused"              5 bash "$0" notes-check 1.1.1 "$tmp/notes"
    expect "notes missing fine with --optional" 0 bash "$0" notes-check 1.1.1 "$tmp/notes" --optional
    expect "broken json refused even if optional" 5 bash "$0" notes-check 1.1.9 "$tmp/notes" --optional
    expect "notes for another version refused"  5 bash "$0" notes-check 1.1.3 "$tmp/notes"
    expect "empty items refused"                5 bash "$0" notes-check 1.1.4 "$tmp/notes"
    expect "empty title refused"                5 bash "$0" notes-check 1.1.5 "$tmp/notes"
    expect "markup refused"                     5 bash "$0" notes-check 1.1.6 "$tmp/notes"
    expect "links refused"                      5 bash "$0" notes-check 1.1.7 "$tmp/notes"
    expect "placeholder refused"                5 bash "$0" notes-check 1.1.8 "$tmp/notes"
    expect "broken json refused"                5 bash "$0" notes-check 1.1.9 "$tmp/notes"
    expect "unknown field refused"              5 bash "$0" notes-check 1.2.0 "$tmp/notes"
    expect "long item refused"                  5 bash "$0" notes-check 1.2.1 "$tmp/notes"
    expect "bad version argument refused"       1 bash "$0" notes-check 1.1 "$tmp/notes"
    expect "the repository's own notes for VERSION (optional)" 0 bash "$0" notes-check "$(bash "$0" version)" "$ROOT/releases" --optional

    echo "previous-version + bump-check, as both workflows run them (fake gh)"
    mkdir -p "$tmp/bin"
    cat > "$tmp/bin/gh" <<'GH'
#!/bin/bash
# The two reads previous-version makes. FAKE_GH picks the answer; FAKE_REF is the only commit.
error() { echo "{\"message\":\"$1\",\"status\":\"$2\"}"; echo "gh: $1 (HTTP $2)" >&2; exit 1; }
raw="api -H Accept: application/vnd.github.raw+json repos/o/r/contents/VERSION?ref="
case "$*" in
    "$raw$FAKE_REF")
        case "$FAKE_GH" in
            present)      printf '2.0.0\n' ;;
            crlf)         printf '2.0.0\r\n' ;;
            empty)        ;;
            missing)      error "Not Found" 404 ;;
            unauthorized) error "Bad credentials" 401 ;;
            rate-limited) error "API rate limit exceeded" 403 ;;
            server-error) echo "gh: HTTP 502" >&2; exit 1 ;;
            offline)      echo "error connecting to api.github.com" >&2; exit 1 ;;
            *)            echo "fake gh: unknown FAKE_GH '$FAKE_GH'" >&2; exit 90 ;;
        esac ;;
    "$raw"*)                                    error "No commit found for the ref" 404 ;;
    "api --silent repos/o/r/commits/$FAKE_REF") ;;
    "api --silent repos/o/r/commits/"*)         error "No commit found for SHA" 422 ;;
    *) echo "fake gh: unexpected call: $*" >&2; exit 91 ;;
esac
GH
    chmod +x "$tmp/bin/gh"
    # A real gh would read GitHub with this machine's credentials. Refuse to run a single case
    # unless the fake resolves first, and leave a real one no token or login to use anyway.
    export PATH="$tmp/bin:$PATH" GH_CONFIG_DIR="$tmp/gh-config" FAKE_REF="$sha"
    unset GH_TOKEN GITHUB_TOKEN
    [ "$(command -v gh)" = "$tmp/bin/gh" ] ||
        die "self-test: fake gh does not resolve first on PATH ($(command -v gh)); aborting"

    guard() { # answer new [ref]
        local previous
        previous=$(FAKE_GH=$1 bash "$0" previous-version o/r "${3:-$sha}") || return
        bash "$0" bump-check "$previous" "$2"
    }
    expect "master without VERSION -> first release" 0 guard missing 2.0.0
    expect "master on 2.0.0, new 2.0.1"              0 guard present 2.0.1
    expect "master on 2.0.0, new 2.0.0 refused"      1 guard present 2.0.0
    expect "master on 2.0.0 with CRLF, new 2.0.1"    0 guard crlf 2.0.1
    expect "404 for an unknown commit refused"       1 guard missing 2.0.0 feedface
    expect "HTTP 401 refused"                        1 guard unauthorized 2.0.0
    expect "HTTP 403 (rate limit) refused"           1 guard rate-limited 2.0.0
    expect "HTTP 502 refused"                        1 guard server-error 2.0.0
    expect "no connection refused"                   1 guard offline 2.0.0
    expect "empty VERSION refused"                   1 guard empty 2.0.0

    [ "$rc" -eq 0 ] && echo "release self-test PASSED" || echo "release self-test FAILED"
    exit "$rc"
}

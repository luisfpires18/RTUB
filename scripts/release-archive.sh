#!/bin/bash

# The immutable production release archive: a private Azure Blob container, one folder per
# version. Deploy • PROD writes a version exactly once; Deploy • PROD and Rollback • PROD read it
# back and deploy those bytes. Nothing in this script deletes or overwrites a blob.
#
#   release-archive.sh put <version> <zip> <release.json>
#   release-archive.sh get <version> <dir>         <dir>/rtub-<version>.zip + release.json, verified
#   release-archive.sh manifest <version> <file>   release.json only
#   release-archive.sh list                        archived versions, lowest to highest
#   release-archive.sh --self-test                 against a fake az; never touches Azure
#
# Exit codes: 0 ok, 1 integrity or uniqueness failure, 2 usage, 4 version not archived.
#
# Needs az logged in (the workflows use the production OIDC identity) and
# RELEASE_STORAGE_ACCOUNT. RELEASE_CONTAINER defaults to "releases". Every call is
# --auth-mode login: the storage account allows no shared key, so there is no key to leak.
#
# Layout:  <version>/rtub-<version>.zip   (blob metadata: version, commit, sha256)
#          <version>/release.json         written LAST - its presence means "archived"

set -u

SEMVER='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
CONTAINER="${RELEASE_CONTAINER:-releases}"

PY=""
for candidate in python3 python; do
    if command -v "$candidate" >/dev/null 2>&1 &&
        "$candidate" -c 'import sys; sys.exit(0 if sys.version_info >= (3, 8) else 1)' >/dev/null 2>&1; then
        PY=$candidate
        break
    fi
done

fail()  { echo "ERROR: $*" >&2; exit 1; }
usage() { echo "ERROR: $*" >&2; exit 2; }

preflight() {
    [ -n "$PY" ] || fail "python 3.8+ is required"
    [ -n "${RELEASE_STORAGE_ACCOUNT:-}" ] || usage "RELEASE_STORAGE_ACCOUNT is not set (a variable on the production environment)"
}

check_version() { [[ "$1" =~ $SEMVER ]] || usage "'$1' is not a MAJOR.MINOR.PATCH version"; }

blob() { # verb [args...]
    local verb=$1
    shift
    az storage blob "$verb" --auth-mode login --account-name "$RELEASE_STORAGE_ACCOUNT" \
        --container-name "$CONTAINER" --only-show-errors "$@"
}

blob_exists() {
    local answer
    answer=$(blob exists --name "$1" --query exists -o tsv) || fail "could not query blob $1"
    answer=${answer%$'\r'}
    [ "$answer" = "true" ]
}

download() { blob download --name "$1" --file "$2" --no-progress -o none || fail "could not download $1"; }

# field <json-file> <key>
field() { "$PY" -c 'import json,sys; v=json.load(open(sys.argv[1],encoding="utf-8")).get(sys.argv[2]); print("" if v is None else v)' "$1" "$2"; }

sha256_of() { "$PY" -c 'import hashlib,sys
h=hashlib.sha256()
with open(sys.argv[1],"rb") as f:
    for c in iter(lambda: f.read(1<<20), b""): h.update(c)
print(h.hexdigest())' "$1"; }

cmd_put() {
    [ $# -eq 3 ] || usage "usage: release-archive.sh put <version> <zip> <release.json>"
    local version=$1 zip=$2 manifest=$3
    check_version "$version"
    preflight
    [ -s "$zip" ] || fail "no zip at $zip"
    [ -s "$manifest" ] || fail "no release.json at $manifest"

    local sha commit
    sha=$(sha256_of "$zip")
    commit=$(field "$manifest" commit)
    [ "$(field "$manifest" version)" = "$version" ] || fail "release.json is for version $(field "$manifest" version), not $version"
    [ "$(field "$manifest" sha256)" = "$sha" ] || fail "release.json sha256 does not match the zip"
    [[ "$commit" =~ ^[0-9a-f]{40}$ ]] || fail "release.json has no full commit SHA"

    local zip_blob="$version/rtub-$version.zip" manifest_blob="$version/release.json"
    local tmp
    tmp=$(mktemp -d)
    trap 'rm -rf "$tmp"' RETURN

    if blob_exists "$manifest_blob"; then
        download "$manifest_blob" "$tmp/existing.json"
        if [ "$(field "$tmp/existing.json" commit)" = "$commit" ] && [ "$(field "$tmp/existing.json" sha256)" = "$sha" ]; then
            echo "Already archived: $version ($commit, sha256 $sha) - identical, nothing written."
            return 0
        fi
        fail "$version is already archived for commit $(field "$tmp/existing.json" commit) (sha256 $(field "$tmp/existing.json" sha256)). A version never points to different bytes or a different commit: bump VERSION. If this commit was simply rebuilt, redeploy the archived artifact with Rollback • PROD instead."
    fi

    if blob_exists "$zip_blob"; then
        # A previous run uploaded the zip and died before the manifest. Finish it only if the
        # stored bytes are these bytes.
        blob show --name "$zip_blob" --query metadata -o json > "$tmp/meta.json" || fail "could not read $zip_blob metadata"
        [ "$(field "$tmp/meta.json" sha256)" = "$sha" ] && [ "$(field "$tmp/meta.json" commit)" = "$commit" ] ||
            fail "$zip_blob already exists with different content. Refusing to archive $version."
        echo "Zip already stored from an interrupted run (same sha256); completing the manifest."
    else
        blob upload --name "$zip_blob" --file "$zip" --overwrite false --no-progress -o none \
            --content-type application/zip --metadata "version=$version" "commit=$commit" "sha256=$sha" ||
            fail "upload of $zip_blob failed"
    fi

    blob upload --name "$manifest_blob" --file "$manifest" --overwrite false --no-progress -o none \
        --content-type application/json || fail "upload of $manifest_blob failed"
    echo "Archived $version: commit $commit, sha256 $sha"
}

cmd_manifest() {
    [ $# -eq 2 ] || usage "usage: release-archive.sh manifest <version> <file>"
    check_version "$1"
    preflight
    blob_exists "$1/release.json" || { echo "Version $1 is not in the release archive." >&2; exit 4; }
    download "$1/release.json" "$2"
    [ "$(field "$2" version)" = "$1" ] || fail "archived release.json for $1 names version $(field "$2" version)"
}

cmd_get() {
    [ $# -eq 2 ] || usage "usage: release-archive.sh get <version> <dir>"
    local version=$1 dir=$2
    mkdir -p "$dir"
    cmd_manifest "$version" "$dir/release.json"

    local zip="$dir/rtub-$version.zip"
    download "$version/rtub-$version.zip" "$zip"

    local want_sha want_size sha size
    want_sha=$(field "$dir/release.json" sha256)
    want_size=$(field "$dir/release.json" sizeBytes)
    sha=$(sha256_of "$zip")
    size=$(wc -c < "$zip" | tr -d ' ')
    [ "$sha" = "$want_sha" ] || fail "rtub-$version.zip sha256 $sha does not match release.json ($want_sha)"
    [ "$size" = "$want_size" ] || fail "rtub-$version.zip is $size bytes, release.json says $want_size"
    echo "Verified $version: commit $(field "$dir/release.json" commit), sha256 $sha, $size bytes"
}

cmd_list() {
    preflight
    local names
    names=$(blob list --query "[?ends_with(name, '/release.json')].name" -o tsv) || fail "could not list the archive"
    printf '%s\n' "$names" | tr -d '\r' | sed -n 's#^\([0-9.]*\)/release\.json$#\1#p' | sort -t. -k1,1n -k2,2n -k3,3n
}

# ------------------------------------------------------------------------------------ self-test

self_test() {
    [ -n "$PY" ] || fail "python 3.8+ is required"
    local tmp rc=0 script
    script="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")"
    tmp=$(mktemp -d)
    trap 'rm -rf "$tmp"' EXIT
    mkdir -p "$tmp/bin" "$tmp/store" "$tmp/azconfig"

    # A file-system stand-in for the handful of `az storage blob` calls above.
    cat > "$tmp/bin/az" <<'FAKE'
#!/bin/bash
[ "$1 $2" = "storage blob" ] || { echo "fake az: unexpected: $*" >&2; exit 90; }
verb=$3; shift 3
name="" file="" overwrite="" meta=() query=""
while [ $# -gt 0 ]; do
    case "$1" in
        --name) name=$2; shift 2 ;;
        --file) file=$2; shift 2 ;;
        --overwrite) overwrite=$2; shift 2 ;;
        --query) query=$2; shift 2 ;;
        --metadata) shift; while [ $# -gt 0 ] && [ "${1#--}" = "$1" ]; do meta+=("$1"); shift; done ;;
        --auth-mode) [ "$2" = "login" ] || { echo "fake az: only --auth-mode login is allowed" >&2; exit 91; }; shift 2 ;;
        *) shift ;;
    esac
done
store="$FAKE_BLOB_STORE"
case "$verb" in
    exists) [ -f "$store/$name" ] && echo true || echo false ;;
    upload)
        [ "$overwrite" = "false" ] || { echo "fake az: uploads must pass --overwrite false" >&2; exit 92; }
        [ -f "$store/$name" ] && { echo "BlobAlreadyExists" >&2; exit 1; }
        mkdir -p "$(dirname "$store/$name")"; cp "$file" "$store/$name"
        printf '%s\n' "${meta[@]}" > "$store/$name.meta" ;;
    download) [ -f "$store/$name" ] || { echo "BlobNotFound" >&2; exit 1; }; cp "$store/$name" "$file" ;;
    show)
        python3 -c 'import sys' 2>/dev/null && py=python3 || py=python
        "$py" -c 'import json,sys; print(json.dumps(dict(l.strip().split("=",1) for l in open(sys.argv[1]) if "=" in l)))' "$store/$name.meta" ;;
    list) (cd "$store" && find . -name release.json | sed 's#^\./##') ;;
    *) echo "fake az: unexpected verb $verb" >&2; exit 93 ;;
esac
FAKE
    chmod +x "$tmp/bin/az"

    # Unit 029's harness ran the REAL az because a shim path did not win on PATH. Refuse to run
    # a single case unless the fake resolves first, and give a real az no login to use anyway.
    export PATH="$tmp/bin:$PATH" AZURE_CONFIG_DIR="$tmp/azconfig" FAKE_BLOB_STORE="$tmp/store"
    [ "$(command -v az)" = "$tmp/bin/az" ] || fail "self-test: fake az does not resolve first on PATH ($(command -v az)); aborting"
    export RELEASE_STORAGE_ACCOUNT=selftest

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

    local c1=1111111111111111111111111111111111111111 c2=2222222222222222222222222222222222222222
    make_release() { # dir version commit payload
        mkdir -p "$1"
        printf '%s' "$4" > "$1/rtub-$2.zip"
        "$PY" - "$1/rtub-$2.zip" "$2" "$3" "$1/release.json" <<'PY'
import hashlib, json, os, sys
z, version, commit, out = sys.argv[1:5]
json.dump({"version": version, "commit": commit, "sha256": hashlib.sha256(open(z, "rb").read()).hexdigest(),
           "sizeBytes": os.path.getsize(z), "migrations": []}, open(out, "w"))
PY
    }
    make_release "$tmp/a" 1.1.2 "$c1" "bytes of 1.1.2"
    make_release "$tmp/rebuilt" 1.1.2 "$c1" "different bytes, same commit"
    make_release "$tmp/other" 1.1.2 "$c2" "bytes of 1.1.2"
    make_release "$tmp/b" 1.1.10 "$c2" "bytes of 1.1.10"
    make_release "$tmp/c" 1.1.9 "$c2" "bytes of 1.1.9"

    echo "put"
    expect "archives a new version"                  0 bash "$script" put 1.1.2 "$tmp/a/rtub-1.1.2.zip" "$tmp/a/release.json"
    expect "same version, same bytes: idempotent"    0 bash "$script" put 1.1.2 "$tmp/a/rtub-1.1.2.zip" "$tmp/a/release.json"
    expect "same version, rebuilt bytes: refused"    1 bash "$script" put 1.1.2 "$tmp/rebuilt/rtub-1.1.2.zip" "$tmp/rebuilt/release.json"
    expect "same version, other commit: refused"     1 bash "$script" put 1.1.2 "$tmp/other/rtub-1.1.2.zip" "$tmp/other/release.json"
    cmp -s "$tmp/store/1.1.2/rtub-1.1.2.zip" "$tmp/a/rtub-1.1.2.zip" && echo "  OK    refused puts left the archive untouched" \
        || { echo "  FAIL  archived bytes changed"; rc=1; }
    expect "manifest for another version refused"    1 bash "$script" put 1.1.3 "$tmp/a/rtub-1.1.2.zip" "$tmp/a/release.json"
    expect "invalid version refused"                 2 bash "$script" put v1.1.2 "$tmp/a/rtub-1.1.2.zip" "$tmp/a/release.json"
    expect "no storage account: usage error"         2 env -u RELEASE_STORAGE_ACCOUNT bash "$script" put 1.1.2 "$tmp/a/rtub-1.1.2.zip" "$tmp/a/release.json"

    # An interrupted put: zip stored, manifest missing.
    rm "$tmp/store/1.1.2/release.json"
    expect "interrupted put, same bytes: completed"  0 bash "$script" put 1.1.2 "$tmp/a/rtub-1.1.2.zip" "$tmp/a/release.json"
    rm "$tmp/store/1.1.2/release.json"
    expect "interrupted put, other bytes: refused"   1 bash "$script" put 1.1.2 "$tmp/rebuilt/rtub-1.1.2.zip" "$tmp/rebuilt/release.json"
    [ ! -f "$tmp/store/1.1.2/release.json" ] && echo "  OK    no manifest written over foreign bytes" \
        || { echo "  FAIL  manifest written"; rc=1; }
    expect "restore the manifest"                    0 bash "$script" put 1.1.2 "$tmp/a/rtub-1.1.2.zip" "$tmp/a/release.json"

    echo "get / manifest / list"
    expect "get verifies and fetches"                0 bash "$script" get 1.1.2 "$tmp/out"
    cmp -s "$tmp/out/rtub-1.1.2.zip" "$tmp/a/rtub-1.1.2.zip" && echo "  OK    fetched bytes are the archived bytes" \
        || { echo "  FAIL  fetched bytes differ"; rc=1; }
    expect "unknown version: exit 4"                 4 bash "$script" get 9.9.9 "$tmp/out9"
    expect "manifest only"                           0 bash "$script" manifest 1.1.2 "$tmp/only.json"
    printf 'tampered' >> "$tmp/store/1.1.2/rtub-1.1.2.zip"
    expect "tampered archive: refused"               1 bash "$script" get 1.1.2 "$tmp/out2"
    bash "$script" put 1.1.10 "$tmp/b/rtub-1.1.10.zip" "$tmp/b/release.json" >/dev/null 2>&1
    bash "$script" put 1.1.9 "$tmp/c/rtub-1.1.9.zip" "$tmp/c/release.json" >/dev/null 2>&1
    local listed
    listed=$(bash "$script" list | tr '\n' ' ')
    [ "$listed" = "1.1.2 1.1.9 1.1.10 " ] && echo "  OK    list is in SemVer order" \
        || { echo "  FAIL  list printed '$listed'"; rc=1; }

    [ "$rc" -eq 0 ] && echo "release-archive self-test PASSED" || echo "release-archive self-test FAILED"
    exit "$rc"
}

case "${1:-}" in
    put)         shift; cmd_put "$@" ;;
    get)         shift; cmd_get "$@" ;;
    manifest)    shift; cmd_manifest "$@" ;;
    list)        shift; cmd_list ;;
    --self-test) self_test ;;
    *) sed -n '3,21p' "$0"; exit 2 ;;
esac

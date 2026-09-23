#!/bin/bash

# Azure App Service smoke test - read-only. Used by Deploy • DEV, Deploy • PROD, Rollback • PROD
# and (through smoke-azure-dev.sh) Database • Refresh DEV from PROD.
#
# Checks that the app is up and still honours the deployment-visible contracts from units 025
# and 026. With EXPECT_VERSION / EXPECT_COMMIT it first waits until GET /api/version answers with
# exactly that build - a healthy OLD instance answering /health is not a pass.
#
# It writes NOTHING: no app settings, no config, no deploy. If the site needs starting it prints
# the command and stops, so starting stays a deliberate act.
#
#   APP=rtub-dev ./scripts/smoke-azure.sh
#   SKIP_AZ=1 APP=rtub EXPECT_VERSION=2.0.0 EXPECT_COMMIT=<40-hex sha> ./scripts/smoke-azure.sh
#   SKIP_AZ=1 APP=rtub EXPECT_LEGACY=1 ./scripts/smoke-azure.sh     # a pre-/api/version build
#
# Environment:
#   APP                   App Service name (default rtub-dev); BASE_URL defaults from it
#   SKIP_AZ=1             skip the two read-only az state checks (the workflows always do)
#   EXPECT_VERSION        wait for /api/version to report this version
#   EXPECT_COMMIT         wait for /api/version to report this full commit SHA
#   EXPECT_LEGACY=1       wait for /api/version to be 404 (release built before it existed)
#   VERSION_WAIT_SECONDS  how long to wait for the expected build (default 600)
#
# Requires curl, and az only for the state checks.

set -u

APP="${APP:-rtub-dev}"
GROUP="${GROUP:-rtub_group}"
BASE="${BASE_URL:-https://${APP}.azurewebsites.net}"
FAILED=0

pass() { echo "  OK    $1"; }
fail() { echo "  FAIL  $1"; FAILED=1; }

# Parses `az webapp show --query "[state,usageState]" -o tsv` into SITE_STATE and
# USAGE_STATE. Reads from stdin so it can be unit-tested without Azure.
#
# Two separate traps, both of which this got wrong before:
#
#  1. The query returns a two-element ARRAY, and az prints array elements one per
#     LINE — not tab-separated. Splitting on tab yields one blob that matches no
#     case branch.
#  2. On Windows (Git Bash, PowerShell-hosted az) every line ends CRLF, so `read`
#     leaves a trailing CR: SITE_STATE becomes $'Running\r'. `case` then falls
#     through to the default branch, and printing it makes the carriage return
#     overwrite the start of the line — which is why the failure rendered as
#         usageState=Normal
#         ' FAIL  unexpected state 'Running
#     Stripping CR is what fixes it; it is a no-op on Linux CI.
parse_site_state() {
    SITE_STATE=""
    USAGE_STATE=""
    local line1 line2
    IFS= read -r line1 || true
    IFS= read -r line2 || true
    SITE_STATE=${line1%$'\r'}
    USAGE_STATE=${line2%$'\r'}
}

read_site_state() {
    parse_site_state < <(
        az webapp show -n "$APP" -g "$GROUP" -o tsv --query "[state,usageState]" 2>/dev/null
    )
}

# Reads the /api/version JSON from stdin into VERSION_SEEN and COMMIT_SEEN. sed, not jq or
# python, so the script runs anywhere curl does. A null or missing field reads as empty.
parse_version_json() {
    local json
    json=$(tr -d '\r\n')
    VERSION_SEEN=$(printf '%s' "$json" | sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
    COMMIT_SEEN=$(printf '%s' "$json" | sed -n 's/.*"commit"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
}

# 0 when the answering build is the expected one.
build_matches() {
    [ -z "${EXPECT_VERSION:-}" ] || [ "$VERSION_SEEN" = "$EXPECT_VERSION" ] || return 1
    [ -z "${EXPECT_COMMIT:-}" ] || [ "$COMMIT_SEEN" = "$EXPECT_COMMIT" ] || return 1
}

# `./scripts/smoke-azure.sh --self-test` — exercises the parsers alone, with no Azure and no
# network.
if [ "${1:-}" = "--self-test" ]; then
    rc=0
    check() { # expected_state expected_usage label <input on stdin>
        local want_state=$1 want_usage=$2 label=$3
        parse_site_state
        if [ "$SITE_STATE" = "$want_state" ] && [ "$USAGE_STATE" = "$want_usage" ]; then
            echo "  OK    ${label}"
        else
            echo "  FAIL  ${label}: got state=[${SITE_STATE}] usage=[${USAGE_STATE}]"
            rc=1
        fi
    }

    echo "parser self-test"
    check Running Normal        "LF   Running/Normal"            < <(printf 'Running\nNormal\n')
    check Running Normal        "CRLF Running/Normal"            < <(printf 'Running\r\nNormal\r\n')
    check QuotaExceeded Exceeded "CRLF QuotaExceeded/Exceeded"   < <(printf 'QuotaExceeded\r\nExceeded\r\n')
    check Stopped Normal        "CRLF Stopped/Normal"            < <(printf 'Stopped\r\nNormal\r\n')
    check "" ""                 "empty (az failed)"              < <(printf '')

    # The trailing CR must be gone, not merely invisible: a bare `case` on a
    # CR-suffixed value is exactly the bug this guards.
    parse_site_state < <(printf 'Running\r\nNormal\r\n')
    case "$SITE_STATE" in
        Running) echo "  OK    case matches Running after CRLF" ;;
        *)       echo "  FAIL  case did not match Running"; rc=1 ;;
    esac

    sha=0123456789abcdef0123456789abcdef01234567
    version_check() { # want_version want_commit label <json on stdin>
        local want_version=$1 want_commit=$2 label=$3
        parse_version_json
        if [ "$VERSION_SEEN" = "$want_version" ] && [ "$COMMIT_SEEN" = "$want_commit" ]; then
            echo "  OK    ${label}"
        else
            echo "  FAIL  ${label}: got version=[${VERSION_SEEN}] commit=[${COMMIT_SEEN}]"
            rc=1
        fi
    }
    version_check 1.1.2 "$sha" "compact JSON"   < <(printf '{"version":"1.1.2","commit":"%s"}' "$sha")
    version_check 1.1.2 "$sha" "spaced, CRLF"   < <(printf '{ "version" : "1.1.2",\r\n  "commit" : "%s" }\r\n' "$sha")
    version_check 1.1.2-dev.7 "" "null commit"  < <(printf '{"version":"1.1.2-dev.7","commit":null}')
    version_check "" "" "not JSON (404 page)"   < <(printf '<html>Not Found</html>')

    VERSION_SEEN=1.1.2 COMMIT_SEEN=$sha
    EXPECT_VERSION=1.1.2 EXPECT_COMMIT=$sha build_matches && echo "  OK    expected build matches" \
        || { echo "  FAIL  expected build did not match"; rc=1; }
    EXPECT_VERSION=1.1.1 EXPECT_COMMIT=$sha build_matches && { echo "  FAIL  older version accepted"; rc=1; } \
        || echo "  OK    older version is not the expected build"
    EXPECT_VERSION=1.1.2 EXPECT_COMMIT=ffffffffffffffffffffffffffffffffffffffff build_matches \
        && { echo "  FAIL  other commit accepted"; rc=1; } || echo "  OK    same version, other commit is not the expected build"

    [ "$rc" -eq 0 ] && echo "parser self-test PASSED" || echo "parser self-test FAILED"
    exit "$rc"
fi

echo "=========================================="
echo "Azure smoke test — ${BASE}"
echo "=========================================="
echo ""

if [ "${SKIP_AZ:-0}" != "1" ]; then
  # A. healthCheckPath. A failing probe restarts the only instance; on Free tier that is
  #    what disabled rtub-dev during unit 027. Set it only after a green deploy.
  echo "A. healthCheckPath"
  HCP=$(az webapp config show -n "$APP" -g "$GROUP" -o tsv --query "healthCheckPath" 2>/dev/null)
  if [ -z "$HCP" ]; then
    pass "empty"
  else
    fail "set to '${HCP}' — clear it before starting the app"
  fi
  echo ""

  # B. Site state. QuotaExceeded means a Free-tier counter has not reset yet.
  echo "B. site state"
  read_site_state
  echo "     state=${SITE_STATE:-<unknown>}  usageState=${USAGE_STATE:-<unknown>}"

  if [ "$USAGE_STATE" = "Exceeded" ]; then
    fail "quota exceeded — wait for the 00:00 UTC reset; do not start or deploy"
    exit 1
  fi

  case "$SITE_STATE" in
    Running)
      pass "running" ;;
    Stopped)
      echo ""
      echo "  Site is stopped. Start it ONCE, by hand, then re-run this script:"
      echo ""
      echo "    az webapp start -n ${APP} -g ${GROUP}"
      echo ""
      echo "  Wait ~60s after starting: the first boot runs EF migrations and"
      echo "  the seed against an empty database."
      exit 0 ;;
    QuotaExceeded)
      fail "quota exceeded — wait for the 00:00 UTC reset; do not start or deploy"
      exit 1 ;;
    "")
      fail "could not read site state (is az logged in?)"
      exit 1 ;;
    *)
      fail "unexpected state '${SITE_STATE}'" ;;
  esac
  echo ""
fi

# The deployed build must be the one answering. A restart after a deploy is not instant, and
# until it happens the OLD instance keeps answering /health with 200.
WAIT="${VERSION_WAIT_SECONDS:-600}"
if [ -n "${EXPECT_VERSION:-}${EXPECT_COMMIT:-}" ]; then
  echo "B2. waiting up to ${WAIT}s for ${EXPECT_VERSION:-<any version>} @ ${EXPECT_COMMIT:-<any commit>}"
  deadline=$(( $(date +%s) + WAIT ))
  VERSION_SEEN="" COMMIT_SEEN=""
  while :; do
    parse_version_json < <(curl -s --max-time 30 "${BASE}/api/version" || true)
    build_matches && break
    [ "$(date +%s)" -ge "$deadline" ] && break
    sleep 15
  done
  if build_matches; then
    pass "/api/version = ${VERSION_SEEN} @ ${COMMIT_SEEN}"
  else
    fail "/api/version = '${VERSION_SEEN:-<none>}' @ '${COMMIT_SEEN:-<none>}' — the deployed build never started answering"
    exit 1
  fi
  echo ""
elif [ "${EXPECT_LEGACY:-0}" = "1" ]; then
  echo "B2. waiting up to ${WAIT}s for a build without /api/version (legacy release)"
  deadline=$(( $(date +%s) + WAIT ))
  while :; do
    CODE=$(curl -s -o /dev/null -w '%{http_code}' --max-time 30 "${BASE}/api/version") || CODE=000
    [ "$CODE" = "404" ] && break
    [ "$(date +%s)" -ge "$deadline" ] && break
    sleep 15
  done
  [ "$CODE" = "404" ] && pass "/api/version = 404 (legacy build is answering)" \
    || { fail "/api/version = ${CODE} — the legacy build never started answering"; exit 1; }
  echo ""
fi

# C..E. HTTP contracts. Headers come from a normal GET, not HEAD — HEAD is not
#       the request path a browser takes and the CSP header is attached from
#       OnStarting based on the response Content-Type.
echo "C. HTTP contracts"

CODE=$(curl -s -o /dev/null -w '%{http_code}' --max-time 60 "${BASE}/health") || CODE=000
[ "$CODE" = "200" ] && pass "/health = 200" || fail "/health = ${CODE} (expected 200)"

CODE=$(curl -s -o /dev/null -w '%{http_code}' --max-time 60 "${BASE}/login") || CODE=000
[ "$CODE" = "200" ] && pass "/login = 200" || fail "/login = ${CODE} (expected 200)"

CODE=$(curl -s -o /dev/null -w '%{http_code}' --max-time 60 "${BASE}/manifest.webmanifest") || CODE=000
[ "$CODE" = "200" ] && pass "/manifest.webmanifest = 200" || fail "/manifest.webmanifest = ${CODE}"

CODE=$(curl -s -o /dev/null -w '%{http_code}' --max-time 60 "${BASE}/service-worker.js") || CODE=000
[ "$CODE" = "200" ] && pass "/service-worker.js = 200" || fail "/service-worker.js = ${CODE}"

HTML_CSP=$(curl -s -o /dev/null -D - --max-time 60 "${BASE}/login" | grep -ci '^content-security-policy:' || true)
[ "$HTML_CSP" -ge 1 ] && pass "CSP present on HTML" || fail "no CSP on HTML (unit 025 contract)"

SW_CSP=$(curl -s -o /dev/null -D - --max-time 60 "${BASE}/service-worker.js" | grep -ci '^content-security-policy:' || true)
[ "$SW_CSP" -eq 0 ] && pass "no CSP on /service-worker.js" || fail "CSP on /service-worker.js (breaks SW caching)"

echo ""

# F. The app must still be up after all of that. A crash-loop shows as /health
#    answering once and then not at all - or as a different build answering.
echo "D. still running after the checks"
sleep 5
CODE=$(curl -s -o /dev/null -w '%{http_code}' --max-time 60 "${BASE}/health") || CODE=000
[ "$CODE" = "200" ] && pass "/health still 200" || fail "/health = ${CODE} — app did not stay up"
if [ -n "${EXPECT_VERSION:-}${EXPECT_COMMIT:-}" ]; then
  parse_version_json < <(curl -s --max-time 30 "${BASE}/api/version" || true)
  build_matches && pass "still ${VERSION_SEEN} @ ${COMMIT_SEEN}" \
    || fail "/api/version changed to '${VERSION_SEEN}' @ '${COMMIT_SEEN}'"
fi

echo ""
echo "=========================================="
if [ "$FAILED" -eq 0 ]; then
  echo "PASS"
else
  echo "FAIL — see above"
fi
echo "=========================================="
exit "$FAILED"

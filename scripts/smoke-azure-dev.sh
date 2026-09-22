#!/bin/bash

# Azure DEV smoke test — read-only.
#
# Checks that rtub-dev is up and still honours the deployment-visible contracts
# from units 025 and 026. Run it by hand after a deploy, or after the Free-tier
# quota resets at 00:00 UTC.
#
# It writes NOTHING: no app settings, no config, no deploy. If the site needs
# starting it prints the command and stops, so starting stays a deliberate act.
# On Free tier every restart counts against a 15/day allowance.
#
#   ./scripts/smoke-azure-dev.sh
#
# Requires: az (logged in) and curl. az is only used for the two read-only
# state checks; skip them with SKIP_AZ=1 to run the HTTP checks alone.

set -u

APP="rtub-dev"
GROUP="rtub_group"
BASE="https://${APP}.azurewebsites.net"
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

# `./scripts/smoke-azure-dev.sh --self-test` — exercises the parser alone, with
# no Azure and no network. Covers LF, CRLF and the empty case.
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

    [ "$rc" -eq 0 ] && echo "parser self-test PASSED" || echo "parser self-test FAILED"
    exit "$rc"
fi

echo "=========================================="
echo "Azure DEV smoke test — ${BASE}"
echo "=========================================="
echo ""

if [ "${SKIP_AZ:-0}" != "1" ]; then
  # A. healthCheckPath must stay empty on Free tier. A probe against an app
  #    that is not answering restarts the instance, and that is what disabled
  #    this site during unit 027.
  echo "A. healthCheckPath"
  HCP=$(az webapp config show -n "$APP" -g "$GROUP" -o tsv --query "healthCheckPath" 2>/dev/null)
  if [ -z "$HCP" ]; then
    pass "empty (correct for Free tier)"
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
#    answering once and then not at all.
echo "D. still running after the checks"
sleep 5
CODE=$(curl -s -o /dev/null -w '%{http_code}' --max-time 60 "${BASE}/health") || CODE=000
[ "$CODE" = "200" ] && pass "/health still 200" || fail "/health = ${CODE} — app did not stay up"

echo ""
echo "=========================================="
if [ "$FAILED" -eq 0 ]; then
  echo "PASS"
else
  echo "FAIL — see above"
fi
echo "=========================================="
exit "$FAILED"

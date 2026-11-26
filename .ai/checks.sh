#!/usr/bin/env bash
# Pre-push gate for this repository. scripts/gate.py runs the integration
# branch's copy of this file inside the task's worktree, so a branch cannot
# weaken its own checks. Fast, deterministic, non-interactive; no full build.
# Not run here: RTUB.Integration.Tests, RTUB.Web.Tests (slow, CI covers them),
# the Release build and `npm run build:portal`.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet restore RTUB.sln
dotnet build RTUB.sln --no-restore --configuration Debug
dotnet test --project tests/RTUB.Core.Tests --no-build --configuration Debug
dotnet test --project tests/RTUB.Application.Tests --no-build --configuration Debug

cd src/RTUB.Web
npm ci --no-audit --no-fund
npm run check:portal
npm run check:calendar
npm run check:startup
npm run check:load

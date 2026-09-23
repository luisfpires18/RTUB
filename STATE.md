# STATE.md

Living execution state. **Read this first.** A status board, not a diary: history is in git, and
durable detail lives in the docs linked below.

_Last updated: 2026-09-22_

## Phase
**Unit 030 - production release pipeline.** Repository work (S1-S6) is **complete, uncommitted, on
`chore/030/production-release-pipeline`** (branched from `dev` at `b9288ef0`), awaiting owner review.
**No Azure resource, GitHub setting or production app was changed.** The production path goes live
only through the owner actions below, then a `dev → master` merge.

## Where things are
| Topic | Doc |
| --- | --- |
| Branch model, versions, releasing, archive, rollback, hotfix, database rules and restore | `docs/release-and-rollback.md` |
| DEV/PROD matrix, workflows, packaging, run-from-package, OIDC, setting names, smoke | `docs/ci-cd-and-azure-environments.md` |
| Why | `docs/architecture/adr/0001-production-release-and-rollback.md` |
| R2, daily backups, DEV refresh (029), storage ownership | `docs/cloudflare-r2-and-database-backups.md` |

## Unit 030 - what changed
- **Workflows** (Actions sidebar): **CI • Build & Test** · **Deploy • DEV** · **Deploy • PROD** ·
  **Rollback • PROD** · **Database • Refresh DEV from PROD** (029 internals untouched - name only).
  One `dotnet publish` (`.github/actions/package-release`), one deploy+smoke
  (`.github/actions/deploy-and-verify`). No publish profile anywhere.
- **Versions:** root `VERSION` = `2.0.0`, strict SemVer, stamped into every build;
  `GET /api/version` → `{version, commit}`. DEV reports `<VERSION>-dev.<run>` (`2.0.0-dev.<run>`
  until 2.0.0 ships; after every release `dev`'s `VERSION` moves to the next unreleased version).
- **Build once / immutable:** a master commit → one zip → guards on the zip → private Azure Blob
  archive `releases/<version>/` (create-only; a version never maps to other bytes or another commit)
  → read back → deployed → smoke must see that version AND commit. Rollback • PROD redeploys an
  archived version and never builds. No automatic rollback.
- **Database:** `PreMigrationSnapshot` before `MigrateAsync` (online backup, validated, newest 5,
  fails closed); N-1 expand/contract rule; Rollback • PROD refuses a target that does not know the
  live release's migrations unless `allow_newer_schema`. No automatic DB rollback.
- **LOG-1 fixed:** one rule, `LogisticsAuthorization.CanManage` = Admin or Mod, used by both Logistics
  pages and re-checked in every mutating handler. Mod now has Admin's Logistics capabilities;
  Member/other roles cannot create, edit, delete, move or finish anything - including the empty-state
  create buttons and the JS-invokable `OnCardMoved` (now throws, and drag-and-drop is never
  initialised for them). Reminders stay open to every member. Mod gains nothing outside Logistics.

## Validation (030)
- `dotnet build --configuration Release`: 0 warnings, 0 errors.
- `dotnet test` (all five suites): **4855 total, 0 failed, 4789 succeeded, 66 skipped** on Windows
  (the 6 bash self-tests run on Linux CI → 60 skipped there). Net **+61** on 4794: +36 Logistics bUnit,
  +4 Logistics integration, +7 version, +1 `/api/version`, +6 snapshot, +17 workflow contract,
  −10 placebo tests that asserted string literals.
- Red-before-green by mutation: Logistics rule without Mod / drag guard removed (9 red, bUnit;
  2 red, integration); raw `File.Copy` instead of the online backup (WAL rows lost → red); archive
  accepting duplicates (2 red); zip guard without backslash / arch checks (2 red); workflow
  mutations (3 red). All restored and green.
- Script self-tests: `release.sh` 48 checks, `release-archive.sh` 18 (fake `az`; refuses to run unless
  the fake wins on `PATH`), `smoke-azure.sh` 13. Workflow YAML parsed; all 38 `run:` bodies `bash -n`.
- Live, read-only: `smoke-azure.sh` passes against DEV and (legacy mode) PROD, and fails when told to
  expect a commit that is not deployed.

## Owner actions - in this order
Nothing below has been done. Items 1-11 do not touch the running production app; 12 restarts it.

1. Review, commit, PR `chore/030/production-release-pipeline` → `dev`, merge. **Deploy • DEV** runs the
   new path: check it is green and `https://rtub-dev.azurewebsites.net/api/version` shows
   `2.0.0-dev.<n>` and the commit; check a Mod account on a DEV Logistics board.
2. Azure: user-assigned managed identity **`rtub-prod-deploy`** in `rtub_group`.
3. Federated credential **`github-production-env`** on it: issuer
   `https://token.actions.githubusercontent.com`, subject
   `repo:luisfpires18/RTUB:environment:production`, audience `api://AzureADTokenExchange`.
4. Role **Website Contributor** for it, scope = the **`rtub` site resource only**.
5. Storage account for the archive (globally unique name): StorageV2, Standard_LRS, Italy North,
   TLS 1.2, HTTPS only, **public blob access off, shared-key access off**.
6. Container **`releases`** (private) - control plane: `az storage container-rm create`.
7. Time-based **immutability policy, 180 days**, on `releases` (start unlocked).
8. Role **Storage Blob Data Contributor** for `rtub-prod-deploy`, scope = the **`releases` container**.
9. GitHub: environment **`production`**, deployment branches = **`master` only**, no reviewers, no
   secrets.
10. Variables on `production`: `AZURE_CLIENT_ID` (of `rtub-prod-deploy`), `AZURE_TENANT_ID`,
    `AZURE_SUBSCRIPTION_ID`, `RELEASE_STORAGE_ACCOUNT` (the account from 5).
11. Ruleset on `master` and `dev`: block force pushes and deletions. (Optional: require *Build & Test*
    on PRs into `master`; optional: restrict `development` to `dev`.)
12. **Enable Always On** on `rtub` (restarts it). Next day, confirm `rtub-db/database/current.db` is
    fresh.
13. Archive the running pre-030 build as **1.0.0** - `docs/release-and-rollback.md` → *One-time:
    archive the running pre-030 build as 1.0.0* (read-only on production).
14. PR `dev → master` (checks: *Build & Test*, *VERSION is bumped*), merge with **Create a merge
    commit** → **Deploy • PROD** releases **2.0.0** (still extract mode). If it stops before deploying
    (login, role, variable), production is untouched: fix, then *Re-run failed jobs*.
15. Check `https://rtub.azurewebsites.net/api/version` = `2.0.0` + the merge commit.
16. **Merge `master` back into `dev`, then bump `dev`'s `VERSION` to `2.0.1`** (or `2.1.0` / `3.0.0` if
    the next release is already known to be MINOR / MAJOR) before other work merges. DEV then
    reports `2.0.1-dev.<run>` - never `2.0.0-dev.<run>`, which SemVer ranks below the released 2.0.0.
17. **Run-from-package cutover, part 1** - in a quiet window, set **`WEBSITE_RUN_FROM_PACKAGE=1`** on
    `rtub`. This restarts the app with no package stored yet, so it may be down until step 18
    finishes: start step 18 straight away.
18. **Run-from-package cutover, part 2** - redeploy the archived 2.0.0:
    `gh workflow run rollback-prod.yml --ref master -f version=2.0.0 -f confirm=2.0.0`. It stores and
    mounts the package; its smoke test must pass. (The first real use of Rollback • PROD.)
19. Retire basic auth: delete repo secret **`AZURE_WEBAPP_PUBLISH_PROFILE`**; set `rtub`
    `basicPublishingCredentialsPolicies/scm` **allow=false** (FTP is already off). Optionally reset the
    publish profile first.
20. Delete the dead **`IDrive__*`** app settings on `rtub` (unit 027 finding).
21. GitHub Copilot coding agent (not a checked-in file; it is the `dynamic/copilot-swe-agent/copilot`
    workflow): profile → **Copilot settings → Cloud agent → Policies → Repository access → No
    repositories**. Then delete the **`copilot`** environment. `copilot/*` branches are leftovers;
    delete when convenient. `.github/copilot-instructions.md` stays - `CLAUDE.md` routes to it as the
    general conventions doc.
22. **CodeQL is not running today.** The "CodeQL" sidebar entry is one failed run (2026-01-06, branch
    `copilot/fix-code-analysis-error`) of a file that never reached `dev` or `master`; code-scanning
    default setup is *not configured*. To keep CodeQL: Settings → Code security → Code scanning →
    CodeQL analysis → **Set up → Default**. Deleting that one old run clears the dead entry.

## Blocking the first production release
Nothing in the repository. Owner actions **2-10** must exist first - without them Deploy • PROD stops
at "Refuse without a release archive" or at the Azure login, before anything is deployed. **12**
(backups actually running) and **13** (a 1.0.0 to roll back to) are strongly recommended before
**14**. The first release ships units 001-030 together (dev is 29 merges ahead of master) but no new
migration: production's newest migration is already dev's newest.

## Deferred - recorded, not fixed
Raised by 030:
- `UpdateCardStatusEnumValues` and `NerbaOrderEventRequired` migration classes have no `[Migration]`
  attribute, so EF has never run them anywhere. Both hold destructive SQL; do not "fix" them by adding
  the attribute without deciding what they should do.
- `refresh-dev-database.yml` uses concurrency group `refresh-dev-database`, Deploy • DEV uses
  `deploy-dev`: a push to `dev` during a refresh can deploy mid-refresh. Also still pins
  `checkout@v4`/`setup-dotnet@v4`. Left byte-identical by instruction.
- `LogisticsBoard.razor` has no Leitão redirect (`Logistics.razor` has); a Leitão member can open a board
  by URL (read-only since 030).
- `development` environment has no deployment branch policy.
- `README.md` outside its Deployment section is stale: `isEmptyDb` seeding (now `SeedData:SeedFullDataset`),
  IDrive keys, and "Production Configuration" (the connection string is an App Service setting).
Carried (one line each; detail in git history):
- Security: `UseHttpsRedirection` skipped outside Development (now redundant, forwarded headers are
  on); login `OnRejected` is global; 429 on a form POST is a bare text page; `UpdateSecurityStampAsync`
  on role change is a forced logout; `ValidationInterval` default 30 min; COOP/COEP/CORP not set;
  unused `X-CSRF-TOKEN` antiforgery header; dead `POST /api/push/broadcast|send-to-selected`.
- Password-policy hardening skipped by owner decision (2026-09-21).
- `LastLoginDate` means "last authenticated activity"; `IsInRoleAsync` pair is 2 SELECTs.
- Dependencies: Microsoft 10.0.11 → 10.0.12 (unblocks `MockQueryable.Moq` 10.0.12); AWSSDK train;
  QuestPDF 2026.x major; dead `AWSSDK.Core` references in three test projects.
- `xUnit1051` suppressed; MTP telemetry (`TESTINGPLATFORM_TELEMETRY_OPTOUT=1` to opt out).
- Push: `WebPushClient` newed up (untestable sends); two service-worker registration paths;
  unbounded broadcast; `PushNotificationsManager` parked on `window`.
- Dead/orphaned front-end bits: `rtub.carousel.js`, `.meeting-today-badge`, rare-tab animation;
  `Profile.razor` overflow intent unimplemented; `.no-scroll` loses scroll position.
- Payload: `wwwroot/sprites` is ~180 MB of the ~230 MB zip; moving sprites to R2 would shrink every
  deploy.
- `.deployment` (`SCM_DO_BUILD_DURING_DEPLOYMENT=true`) is inert.
- Owner Storage Maintenance page (029 contract) not built.
- After a DEV refresh, keep `DevelopmentDataReset__Enabled` off on `rtub-dev`.
- iPhone installed-PWA check on `/music` still needs a real device (DEV URL is fine).
- GitGuardian: historical incidents stay historical; mark them by hand.
- Old work branches `chore/001`…`chore/029`, `fix/012`…`fix/029`, `perf/019`: delete when convenient.

## Next
Owner actions above. After step 18: STATE.md → "030 complete".

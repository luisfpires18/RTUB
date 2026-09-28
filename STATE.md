# STATE.md

Living execution state. **Read this first.** A status board, not a diary: history is in git, and
durable detail lives in the docs linked below.

_Last updated: 2026-09-28_

## Phase
**2.0.2 development on `dev`.** Unit 030 (production release pipeline) is **complete**. UI refactor
program: 032-035 merged. **036 (Task 005, RTUB.Shared component system: `FormField` on 126 fields,
toast feedback replacing page timers, EmptyState/LoadingSpinner/ErrorDisplay/Alert/SearchBar/
FilterDropdown refinements, `FilterToolbar` on 18 pages, `AppErrorBoundary` on 54 pages +
`#blazor-error-ui`) complete on `chore/036/ui-shared-component-system`, not yet merged.** Also fixes a
live crash: `Alert` had no `Title` parameter that 6 "member added" feedbacks passed. Contract:
`docs/design/RTUB_UI_REFACTOR.md` sections 20-23.

## Where things are
| Topic | Doc |
| --- | --- |
| UI refactor: audit, design direction, shared-component plan, roadmap | `docs/design/RTUB_UI_REFACTOR.md` |
| Branch model, versions, releasing, archive, rollback, hotfix, database rules and restore | `docs/release-and-rollback.md` |
| DEV/PROD matrix, workflows, packaging, run-from-package, OIDC, setting names, smoke | `docs/ci-cd-and-azure-environments.md` |
| Why | `docs/architecture/adr/0001-production-release-and-rollback.md` |
| R2, daily backups, DEV refresh (029), storage ownership | `docs/cloudflare-r2-and-database-backups.md` |

## Environments - verified 2026-09-24
- **PROD** `rtub` ← `master` `88213950`: **2.0.1** (`/api/version` = that commit), B1, run from
  package (`WEBSITE_RUN_FROM_PACKAGE=1`), Always On enabled, `/health` Healthy.
- **DEV** `rtub-dev` ← `dev` `5f2aa064`: **2.0.2-dev.6** (`/api/version` = that commit), **Free F1**
  (moved from B1), Always On disabled, no `healthCheckPath`, `DatabaseBackup__Enabled=false`,
  `DevelopmentDataReset__Enabled=false`, `/health` Healthy.

## Unit 030 - complete
- Live: **Deploy • PROD**, **Rollback • PROD** and the private release archive; GitHub reaches Azure
  by OIDC only. Repository work: PR #203. Releases: 2.0.0 (PR #205), hotfix 2.0.1 (PR #207, merged
  back in #209; `dev` bumped to 2.0.2 in #210).
- Legacy pre-030 PROD build archived as **1.0.0** in the private release archive: the rollback
  artifact for the pre-modernization production state.
- PROD moved to run-from-package; a same-version 2.0.1 redeploy under it succeeded.
- Basic auth retired: repo secret `AZURE_WEBAPP_PUBLISH_PROFILE` deleted; SCM and FTP basic
  publishing credentials disabled on `rtub`.
- Dead `IDrive__AccessKey`/`Bucket`/`Endpoint`/`SecretKey` settings removed from `rtub`; PROD
  healthy after the restart.
- Ruleset *Protect permanent branches*: no deletion or force push on `master` and `dev`.

## Android / Google Play - complete (issue #208 closed)
Package `ipb.pt.rtub.app`: developer verification **Registered**; signing key **Validated**, its
SHA-256 matches `src/RTUB.Web/wwwroot/.well-known/assetlinks.json`. Production bundle: version code
14, version name 3.0.0.0, **target SDK 36** (Android 16); the old SDK 35 bundle has 0 active
releases. Regenerated with PWABuilder - no RTUB code change.

## Deferred - recorded, not fixed
Raised by 036 (detail: `docs/design/RTUB_UI_REFACTOR.md` 23.9, 23.12):
- `AppErrorBoundary` cannot catch a page's own event handlers/lifecycle (Blazor attributes them to
  the page); they still end the circuit (now with `#blazor-error-ui`). Phase 4 try/catch retrofit.
- 170 unassociated `<label>`s and 78 raw `alert` divs outside the migrated shape; `questions.css`
  global `.filter-dropdown-container` rules; `PaginatedList` English default.

Raised by 035 (detail: `docs/design/RTUB_UI_REFACTOR.md` 22.3, 22.11):
- Browser/hardware Back with a dialog open still changes the page underneath: a history decision
  for Phase 4 (forms, dialogs and data safety), with `Modal.CanClose` as the guard hook.
- Albums' phone bar mixes section jumps with actions; `ConfirmDialog` still used for a few info
  dialogs.

Raised by 034 (detail: `docs/design/RTUB_UI_REFACTOR.md` 21.7-21.8):
- Real-device check of the shell: installed iOS app under the status bar (`viewport-fit=cover`),
  landscape notch insets, Android Back with the drawer open.
- Unknown URLs return an empty 404 (no status-code page); `Error.razor` is still English.

Raised by 033 (detail: `docs/design/RTUB_UI_REFACTOR.md` 20.7-20.8; the CSS cache-busting item is
resolved by 034):
- `bg-info` badges (`#007bff`) fail contrast with white and dark text; needs a color decision.

Raised by 032 (UI audit; UI findings themselves live in `docs/design/RTUB_UI_REFACTOR.md`):
- Local Development CSP `img-src` allows only this environment's R2 public domain, so a sanitized
  production snapshot's media (slideshow, covers, avatars) is blocked in local runs.
- Local run with User Secrets holding R2 credentials is refused by the production-bucket guard
  unless `Cloudflare__R2__Bucket` is overridden (how the audit ran: doc section 2.1).
- `global.json` has no SDK pin; `docs/architecture/system-index.md` says it pins the SDK.
- Unused shared code: `PaginatedList`, `ManagedModalPageBase` (`MemberListItem` is planned for
  adoption by the UI roadmap, not removal).

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
- Skipped by owner decision, not work: password-policy hardening (2026-09-21), Copilot coding-agent
  cleanup, CodeQL setup.
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
- iPhone installed-PWA check on `/music` still needs a real device (DEV URL is fine), including the
  lock-screen card opening another web app (LoreX): iOS routing suspected, RTUB causes excluded (#204).
- GitGuardian: historical incidents stay historical; mark them by hand.
- Old work branches `chore/001`…`chore/030`, `fix/012`…`fix/030/*`, `perf/019`, `hotfix/2.0.1/*`:
  delete when convenient.

## Next
- UI refactor Phase 4 / Task 006 (forms, dialogs and data safety: unsaved-change policy on
  `Modal.CanClose`, dialog/Back behaviour, busy states and try/catch around saves) per
  `docs/design/RTUB_UI_REFACTOR.md` sections 17, 22.12 and 23.13; owner decisions in its section 18.
- The next normal merge to `dev` is the first real **Deploy • DEV** run on F1: check it is green and
  `/api/version` shows the new `2.0.2-dev.<run>` and commit. No commit just to test it.
- Check the scheduled PROD backup stays fresh: `rtub-db/database/current.db` Last-Modified is today
  (03:30 UTC run).

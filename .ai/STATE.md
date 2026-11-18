# .ai/STATE.md

Living execution state. **Read this first.** A status board, not a diary: history is in git and GitHub, durable detail
in the docs `CLAUDE.md` links. Overwrite stale entries.

_Last updated: 2026-11-18_

## Release state
- `master` (PROD): **2.3.12**. `dev`: **2.3.13** (unreleased).
- Member release notes: `releases/<VERSION>.json` (`docs/release-and-rollback.md` → *Release notes*).

## Current
Task 120 (`chore/120/test-speed`): phase 1 of the second cleanup round (audit of 2026-11-18). Test hosts no longer start
the application's 13 background workers; one copy of the shared test helpers (`tests/Shared/RepoPaths.cs`,
`Api/AreaHttp.cs`); the sleep, the skipped tests and the tests that asserted nothing are gone (4,048 → 4,038 tests).
Full suite on the same machine: 3 m 28 s → 2 m 11 s. No production code changed.
Task 121 (`perf/121/backend-queries`, on top of 120): the treasury report history loads the report's transactions in
one query (was one per audit log); seven notification loops send one batch (`SendToSelectedUsersAsync`: discussion post
and comment, event and song video, new meeting, meeting request, public request). Same recipients and same output.
Task 122 (`chore/122/backend-shared`, on top of 121): one copy of the shared backend pieces.
`Endpoints/EndpointResults.cs` maps a service outcome to HTTP (19 endpoint files; an area keeps a short `ToResult` only
for its own 404 title or special answers) and builds the one-field 400; `Helpers/ClaimsPrincipalExtensions.cs`
(`user.IsMember()`, `user.UserId()`) replaces 31 copies; Music uses the shared `NoStore` / `RequireAntiforgery`;
`RequireMember` lives in `EventEndpoints`; four repositories lost the `UpdateAsync` / `DeleteAsync` overrides that only
preloaded audit names. Same answers, same audit text.

Last: tasks 110-119 (PRs #314-#325, merged into `dev`): the first cleanup round. No Blazor leftovers; `portal/src` in
`pages/`, `components/`, `data/`, `styles/`; one shared UI set in `components/ui/`; no file over 300 lines except
five that are one function each (`components/MeetingAta.tsx`, `MessageThread.tsx`, `FamilyTree.tsx`,
`Data/SeedData.Member.cs`, `Services/AtaPdfService.cs`); unused code removed.

## Not verified yet
- Aprender: real R2 audio, real phones, the prototype chart's timing by ear ("É Esta a Tuna", album 4; the chart is not
  in the repo, upload it on DEV/PROD), bandolim and cavaquinho chord shapes.
- Release notes modal on a real iPhone / Android, and on DEV with `ShowPrerelease`.
- Push on an Android installed app, an iPhone/iPad Home Screen app and DEV (only Edge desktop was checked).
- Real R2 on DEV for every storage-backed module (local runs use placeholder storage).
- iOS Safari native date/time controls and the `black-translucent` status-bar inset on a real iPhone.

## Owner actions (status unknown: check Azure/GitHub first)
- Always On on PROD (cold start after idle); HTTP/2 on both App Services.
- DEV (`rtub-dev`) has no VAPID settings, so push is "not-configured" there.
- Run `scripts/config/audit-azure-app-settings.ps1` after `az login` and act on its warnings; delete dead `IDrive__*`,
  `Games__*`, `MyTunoScaling__*` app settings. Local user-secrets may still hold dead `IDrive:*` keys.
- Retire basic auth: delete repo secret `AZURE_WEBAPP_PUBLISH_PROFILE`; `rtub` `basicPublishingCredentialsPolicies/scm`
  allow=false.
- Copilot coding agent: Repository access → No repositories; delete the `copilot` environment and the
  `copilot/read-docs-and-apply-best-practices` branch (the Copilot instruction files are gone from the repo).
- CodeQL is not configured (Settings → Code security → Code scanning → Default), if wanted.
- Rulesets on `master` and `dev`: block force pushes and deletions; `development` environment has no branch policy.
- `iarc_rating_id` for the manifest needs the IARC certificate from Play Console.
- After a DEV refresh, keep `DevelopmentDataReset__Enabled` off on `rtub-dev`.
- Old work branches `chore/001`…`chore/029`, `fix/012`…`fix/029`, `perf/019`: delete when convenient.

## Deferred (recorded, not fixed)

**Portal (left after phase 3)**
- `EventManage`, `GalleryManage` and `MeetingDialogs` keep their own error sentences (formal "Tente"/"Entre", other
  rules); 17 notices with their own buttons stay hand-written. Tabs (4) and avatar images differ in class and size.

**UI kit (left after 119)**
- Kept hand-written on purpose (different look or attributes): `lx-tabs`/`lx-views`/`news-tabs`/`playalong__group`
  button groups, `pa-pager`, OwnerUi `Flash`/`SearchBox`, `span.control` fields, radio and `role="switch"` rows,
  `form__label`-headed fields whose error has no id, MeetingAta required-marker fields, link-style icon actions.
- Possible next kit additions: SelectField with an icon, SearchField `autoFocus`/extra class, IconAction as `<a>` and
  with `aria-expanded`, a single-primary-button footer, DialogFooter keeping Cancelar enabled while busy.

**Tests**
- One host per integration test class (about 68 boots, about 7 s each alone). One shared host for the 28
  `IntegrationTestBase` classes cut the Integration project from about 130 s to 87 s, but 3 tests failed
  (`HomePreviewTests` needs an empty database; `AuthAntiforgeryTests` hit the login rate limit, which is per IP).
  Sharing only the 9 read-only classes gained nothing measurable.
- Sleeps that make two timestamps differ: `LeaderboardCommentServiceTests.cs:147`,
  `PostServiceTests.PinLockDelete.cs:178`, `RehearsalAttendanceAdminWorkflowTests.InstrumentAndConsistency.cs:142`,
  `RTUB.Core.Tests/Entities/PostTests.cs:175`; `ApplicationUserExtensionsTests.*` compute expectations from
  `DateTime.Now`. Both need a clock the entities accept.
- Nine `Json(response)` helpers in the API tests assert exactly 200 (`AreaHttp.Json` accepts any 2xx); nine page test
  classes repeat the same `_client` constructor; the Cloudflare storage `Constructor_*InitializesSuccessfully` tests
  assert only "not null".
- `NoStylesheet_SetsOverflowHiddenOnTheViewportRoot` sweeps only `error.css` / `offline.css`, not the portal
  `styles.css`.
- Windows-only: an ignored `src/RTUB.Web/publish/` output, when present, fails guard tests that sweep `src/`.
- No JS test runner: the member shell's browser behaviour is checked by hand; `MemberShellTests` pins source only.

**Security and privacy**
- Notification links are built from the request Host header while `AllowedHosts` is `*`: a manager forging Host could
  point push links elsewhere. Fix app-wide (real `AllowedHosts`, or one configured base URL).
- `www.rtub.pt` serves the app as a second origin with no redirect to `rtub.pt` (second installed-app identity,
  duplicate content): decide a 301 to the apex.
- Gallery, Naipes and receipt files are public-read R2 objects: members-only means "not listed", not "not reachable".
  Private objects + pre-signed URLs would be a storage change.
- forgot-password / reset-password have no rate limit; `/confirm-email` has no link generator in the app.
- `POST /auth/login` reports Locked/Expelled before checking the password (kept to preserve behaviour).
- No guard against the Owner demoting themselves or the last Owner; Owner can delete their own account.
- `/owner/db` shows `AspNetUsers` secret columns and can SELECT the messaging tables (Owner only): decide.
- Decide: members (not only Admin) read requesters' email and phone; Leitões see other members' email and phone.
- Carried: `UseHttpsRedirection` skipped outside Development (redundant, forwarded headers on); login `OnRejected` is
  global; 429 on a form POST is a bare text page; role change forces logout (`UpdateSecurityStampAsync`);
  `ValidationInterval` 30 min; COOP/COEP/CORP not set. Password-policy
  hardening skipped by owner decision.
- `public-requests` rate limit partitions on `RemoteIpAddress`: confirm forwarded headers on DEV/PROD.

**Push**
- "Presença recusada" never sends (`CancelAttendanceAsync` has no caller); leaderboard comment/like push failures are
  swallowed with no log; discussion pushes stop at the event start (UTC compare).
- `PushNotificationService` news up `WebPushClient` (no send tests); no `CancellationToken`; 5xx from a push service is
  not retried; fan-out runs inline in requests. `WebPush` 1.0.13 sends legacy `aesgcm` (move to RFC 8291/8292 only if
  an iPhone test push comes back `rejected`).
- A browser on a push service outside `PushSubscriptionRules.PushServiceHosts` is refused: add its host if one appears.
- PROD App Service has no persisted application logs; push warnings show only in the live log stream.

**Product decisions**
- Reuniões: the fiscal-year filter ends at midnight on 31 August; Owner limits on CV/Direção; requests-section gate;
  unreachable Direção branch of `CanCreateMeetingType`; `Meeting.DelegatedAtaWriterMemberId` never set; no UI for ata
  attachments or delete; a published Direção ata PDF lands in `Atas {FY}`, open to every non-Leitão
  (`docs/react-meetings.md`).
- Messaging v2: group membership frozen at v1's last auto-sync; polling only; no pin/archive UI. DEV after a refresh
  holds production's messages unsanitized (`DatabaseSanitizer` skips them).
- Members map lists expelled accounts; Hall of Fame counts every account.
- `/naipes/config` is not in the member menu (reached from `/naipes`).
- Novidades: no images, comments, reactions, pinned posts or per-post share pages.
- `RoleManagementService.PromoteUserForPositionAsync` keeps an existing Mod as Mod when the member takes an Admin seat.
- Owner Storage Maintenance page not built.

**Bugs and small fixes**
- Treasury: deleting a draft report still referenced by `NerbaOrders.ReportId` answers 500; should be 409.
- `ReportPdfService` caches by `Report.UpdatedAt` (transaction edits do not touch it): PDF up to 1 h stale.
- Shop: a reservation snapshots no product name or price and is deleted with its product; `InFiscalYear` compares UTC
  `CreatedAt` with local-calendar bounds.
- Deleting an instrument leaves its thumbnail in R2; "Remover imagem" on `/naipes/config` leaves the R2 object.
- `UserProfileService.UpdateProfilePictureAsync` deletes the old image before the upload succeeds.
- `NewsEndpoints.cs` 403 title names system roles (API callers only; the portal shows its own message).
- React `/roles` probes `GET /api/governance/manage`, logging a 403 console error for members without rights.
- `portal/index.html` `theme-color` is `#000000`; the manifest says `#3F2A86`.
- Portal `index.html` loads `/js/sw-register.js` unversioned: one visit after a deploy can run the old script.
- `XpSettings` and `RankingConfiguration` both bind `Ranking`; equal level+XP keep SQLite's unordered order.
- Play cooldown and the system announcement live in one app instance's memory (a scale-out needs a shared store).

**Performance** (`docs/performance-gate.md`)
- Left by task 121. `TreasuryService.Reports.cs:28` loads every activity and transaction for the reports list: SQLite
  cannot sum `decimal` in SQL, and a narrow projection would copy the `IsHiddenFromCalculations` rule. Push loops kept
  per recipient: mentions (a member named twice gets two pushes today), the reminder workers (cancellation between
  sends, one log line each) and `MeetingBoardService.Messages.cs:219` (failure isolated per recipient). Not done, low
  traffic: `Notify*` methods loading whole entities (`EnrollmentService.Notifications.cs`,
  `RehearsalAttendanceService.Notifications.cs`), per-row `UpdateAsync` in the reorder loops
  (`EventRepertoireService.cs:59`, `EventService.Videos.cs`), `CalotesNotificationBackgroundService.cs:138`,
  `RehearsalAdminService.cs:148`, `PendingActionsReminderService.cs:125`.
- The report history still reads every `Transaction` audit log (a deleted transaction is matched by its JSON).
- Hot reads: messaging unread/list/thread shapes and a `Messages(ConversationId, Id)` index, `/api/members` full load,
  leaderboard recompute per request, `/profile` ≈ 14 queries, meetings loaded with `int.MaxValue`, `AuditLogs.Action` /
  `PushSubscriptions.Endpoint` indexes, `.Date.Date` range compares.
- Writes in-request: push fan-out with back-off sleeps, audited save's second transaction, member-status reconcile, bulk
  email.
- Avatar and gallery thumbnails (schema), CSS split per area, route-chunk preload, static asset precompression,
  `IMemoryCache` `SizeLimit` for cached PDFs.

**Backend duplicates (left after 122)**
- `OwnerEndpoints`, `MusicEndpoints` (`MusicResult`) and `AccountRecoveryEndpoints` keep their own result mappers (other
  shapes: Owner answers 404 where the others answer 401 / 403). `PushEndpoints` / `ReleaseEndpoints` keep a private
  non-null `UserId` and answer an empty 401 after the filter.
- The shared filters live in `EventEndpoints` and log "Events API … failed" for every area, Music included.
- `AddAsync` overrides in the Enrollment, EventRepertoire, MeetingParticipation and RehearsalAttendance repositories
  still preload for the audit log: the preload also fills the returned entity's navigations, so callers must be checked
  first.
- Not done: 14 one-line repository wrappers of base methods, `MeetingAtaRepository` outside `Repository<T>`, five "load
  member, 401, 403" blocks (`DocumentationService.Helpers.cs:21`, `LogisticsKanbanService.Helpers.cs:25`,
  `RequestAdminService.cs:174`, `TreasuryService.Rules.cs:131`, `TreasuryRecordsService.cs:55`), `IsInRole("Admin") ||
  IsInRole("Owner")` ×14 and `IsOwner` ×3 in the `*Authorization` helpers, full name built inline in 26 places.

**Dead code and config**
- `AspNetUsers.ExperiencePoints` / `Level` have no writer (the leaderboard computes XP per request): DB cleanup candidate.
- `PostMedia` / `CommentImages`: production row counts needed before dropping (`docs/db-cleanup-inventory.md`).
- Kept on purpose: npm `bootstrap-icons` (path-data source for `icons.tsx`).

**Tooling and dependencies**
- `npm audit`: 5 high in vite 6 dependencies (build time only).
- Dependencies to bump: Microsoft 10.0.x train (unblocks `MockQueryable.Moq`), AWSSDK, QuestPDF 2026.x major.
- `xUnit1051` suppressed.
- `refresh-dev-database.yml` and Deploy • DEV use different concurrency groups (a push during a refresh can deploy
  mid-refresh); pinned `checkout@v4` / `setup-dotnet@v4`.
- Local runs read `ConnectionStrings:SqliteConnection`; point it at a scratch copy or the run writes to
  `src/RTUB.Web/app.db`. A local run without `Cloudflare:R2:PublicUrl` blocks R2 images in the CSP (local only).
- GitGuardian: historical incidents stay historical; mark them by hand.

## Next
Second cleanup round, one stacked branch and PR per phase; the owner starts each one ("yes, phase N"): 123 dead code and
stale docs, 124 portal data layer (one load hook, shared types), 125 UI kit leftovers, 126 portal lazy loading, 127 CSS
merges, 128 tests for money, permissions and push gaps.
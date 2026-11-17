# .ai/STATE.md

Living execution state. **Read this first.** A status board, not a diary: history is in git and GitHub, durable detail
in the docs `CLAUDE.md` links. Overwrite stale entries.

_Last updated: 2026-11-17_

## Release state
- `master` (PROD): **2.3.10**. `dev`: **2.3.11** (unreleased).
- Member release notes: `releases/<VERSION>.json` (`docs/release-and-rollback.md` → *Release notes*).

## Current
Task 110 (`chore/110/codebase-cleanup`): phase 1 of the readability cleanup. Blazor-era history comments rewritten as
present-tense rules, old-URL redirects moved out of `Program.cs` into `Extensions/LegacyRedirectExtensions.cs`, service
methods with no production caller removed (with their tests). No behaviour change.
Task 111 (`chore/111/portal-folders`, on top of 110): phase 2, `portal/src` split into `pages/`, `components/`, `data/`
(moves and import paths only; layout rule in `docs/frontend-practices.md` → *Layout*).
Task 112 (`chore/112/shared-ui`, on top of 111): phase 3, shared pieces. `components/Notice.tsx` (Notice,
SignInNotice, RetryNotice: same markup), `data/format.ts`, `queryString` + `outcomeText` in `data/api.ts`, `loginTo`
in `data/content.ts`; one copy of `DEFAULT_AVATAR`, the Logística status/priority lists and the antiforgery token.
Task 113 (`chore/113/split-portal-files`, on top of 112): phase 4, every portal file at most 300 lines by pure moves
(`styles.css` now imports `styles/NNN-area.css` in cascade order; built CSS byte-identical). Three files stay over
300: `components/MeetingAta.tsx` (472), `MessageThread.tsx` (318), `FamilyTree.tsx` (310): one function each.
Task 114 (`chore/114/split-csharp-files`, on top of 113): phase 5, C# under `src/` at most 300 lines: services as
partial classes by topic, `Program.cs` → `Extensions/*Extensions.cs`, `ServiceCollectionExtensions` partials. Two stay
over 300 (one method each): `Data/SeedData.Member.cs` (766, a 701-line seed method sharing ~75 locals) and
`Services/AtaPdfService.cs` (447, a 344-line PDF method).
Task 115 (`chore/115/split-test-files`, on top of 114): phase 6, every test file at most 300 lines (partial test
classes by topic, one class per file); test count unchanged (4175).
Task 116 (`chore/116/remove-unused-code`, on top of 115): unused code deleted: 30 public members with no caller in
`src/` (and the 88 tests that only covered them), unused private members/parameters, 2,141 unused `using`s, 11 dead
CSS rules.

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
- Copilot coding agent: Repository access → No repositories; delete the `copilot` environment and `copilot/*` branches.
- CodeQL is not configured (Settings → Code security → Code scanning → Default), if wanted.
- Rulesets on `master` and `dev`: block force pushes and deletions; `development` environment has no branch policy.
- `iarc_rating_id` for the manifest needs the IARC certificate from Play Console.
- After a DEV refresh, keep `DevelopmentDataReset__Enabled` off on `rtub-dev`.
- Old work branches `chore/001`…`chore/029`, `fix/012`…`fix/029`, `perf/019`: delete when convenient.

## Deferred (recorded, not fixed)

**Code**
- `NominatimGeocodingService.GeocodeAsync` tries three "strategies" but `TryGeocode` ignores its `featureType`, so the
  same Nominatim query can go out three times. Collapsing them changes the request count: decide.

**Portal (left after phase 3)**
- `data/musicApi.ts` keeps its own `call` and `Outcome` (adds `title`, `unavailable`); merging it into `data/api.ts`
  changes every music page's error handling.
- `EventManage`, `GalleryManage` and `MeetingDialogs` keep their own error sentences (formal "Tente"/"Entre", other
  rules); 17 notices with their own buttons stay hand-written. Tabs (4) and avatar images differ in class and size.

**Tests**
- Integration test hosts run `PendingActionsReminderService`, so a full `dotnet test` between 18:00 and 24:00 Portugal
  time can fail 3 `NewsApiTests` ("Push.Invocations to be empty"). Fix: disable the worker in the test factories.
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
- `CachedGeocodingService` creates a DbContext per lookup without disposing it.
- `UserProfileService.UpdateProfilePictureAsync` deletes the old image before the upload succeeds.
- `NewsEndpoints.cs` 403 title names system roles (API callers only; the portal shows its own message).
- React `/roles` probes `GET /api/governance/manage`, logging a 403 console error for members without rights.
- `icons.tsx` `apple` lists the same path twice (duplicate `key` warning, dev build only).
- `portal/index.html` `theme-color` is `#000000`; the manifest says `#3F2A86`.
- Portal `index.html` loads `/js/sw-register.js` unversioned: one visit after a deploy can run the old script.
- `XpSettings` and `RankingConfiguration` both bind `Ranking`; equal level+XP keep SQLite's unordered order.
- Play cooldown and the system announcement live in one app instance's memory (a scale-out needs a shared store).

**Performance** (`docs/performance-gate.md`)
- Hot reads: messaging unread/list/thread shapes and a `Messages(ConversationId, Id)` index, `/api/members` full load,
  leaderboard recompute per request, `/profile` ≈ 14 queries, meetings loaded with `int.MaxValue`, `AuditLogs.Action` /
  `PushSubscriptions.Endpoint` indexes, `.Date.Date` range compares.
- Writes in-request: push fan-out with back-off sleeps, audited save's second transaction, member-status reconcile, bulk
  email.
- Avatar and gallery thumbnails (schema), CSS split per area, route-chunk preload, static asset precompression,
  `IMemoryCache` `SizeLimit` for cached PDFs.

**Dead code and config**
- `AspNetUsers.ExperiencePoints` / `Level` have no writer (the leaderboard computes XP per request): DB cleanup candidate.
- `PostMedia` / `CommentImages`: production row counts needed before dropping (`docs/db-cleanup-inventory.md`).
- ~80 interface methods with no production caller (storage `*ExistsAsync` / `GetFileSizeAsync` / `ListFoldersAsync`,
  CRUD helpers); `IRequestService.GetPendingRequestsAsync`; `MimeTypeHelper.GetMediaMimeType`;
  `ApplicationUserExtensions.GetYearsAsTuno` and friends (tests only).
- Committed but unread settings: `Gallery:MaxVideoSizeMB` (code reads `GalleryMedia:MaxVideoSize`), `ImageCaching:*`,
  `UseSqlServer`.
- Kept on purpose: the old-URL 302s in `Program.cs`; npm `bootstrap-icons` (path-data source for `icons.tsx`).
- `wwwroot/images/icons/*` has no in-repo reference (old logo copies).

**Tooling and dependencies**
- `npm audit`: 5 high in vite 6 dependencies (build time only).
- Dependencies to bump: Microsoft 10.0.x train (unblocks `MockQueryable.Moq`), AWSSDK, QuestPDF 2026.x major.
- `xUnit1051` suppressed.
- `refresh-dev-database.yml` and Deploy • DEV use different concurrency groups (a push during a refresh can deploy
  mid-refresh); pinned `checkout@v4` / `setup-dotnet@v4`.
- `.deployment` (`SCM_DO_BUILD_DURING_DEPLOYMENT=true`) is inert; `deploy-prod.yml` comment says npm runs in the build
  (it does not).
- Local runs read `ConnectionStrings:SqliteConnection`; point it at a scratch copy or the run writes to
  `src/RTUB.Web/app.db`. A local run without `Cloudflare:R2:PublicUrl` blocks R2 images in the CSP (local only).
- GitGuardian: historical incidents stay historical; mark them by hand.
- `scripts/test-twa-config.sh` points at docs that never existed (`docs/URGENT-TWA-SETUP-INSTRUCTIONS.md`,
  `docs/twa-configuration-guide.md`).

## Next
Owner decides the next task (GitHub Issues).

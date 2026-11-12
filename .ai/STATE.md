# .ai/STATE.md

Living execution state. **Read this first.** A status board, not a diary: history is in git and GitHub, durable detail
in the docs `CLAUDE.md` links. Overwrite stale entries.

_Last updated: 2026-11-12_

## Release state
- `master` (PROD): **2.3.7**. `dev`: **2.3.8** (unreleased).
- On `dev`, not released: Aprender / play-along (094, 095, 097: `SongPlayAlongs` table, migration `AddSongPlayAlongs`,
  additive), the ai-workflow adoption (100), `STATE.md` moved to `.ai/` (101), web preset settings (102), docs cleanup
  (103).
- Member release notes: `releases/<VERSION>.json` (`docs/release-and-rollback.md` → *Release notes*).

## Current
None. Last: task 104 (PR #306, merged into `dev`): every Logística file (board files, card attachments) is a
Documentação document in `Logistics/{board}`; rename moves the folder; delete on either side removes it from both;
startup moves older files in; folders no board owns are Mod/Admin/Owner only. Owner check on DEV: the startup move
against real R2, an image on a card showing in Documentação, a delete there leaving the card.

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
  `ValidationInterval` 30 min; COOP/COEP/CORP not set; dead `POST /api/push/broadcast|send-to-selected`. Password-policy
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

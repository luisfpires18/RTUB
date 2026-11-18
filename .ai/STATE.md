# .ai/STATE.md

Living execution state. **Read this first.** A status board, not a diary: history is in git and GitHub, durable detail
in the docs `CLAUDE.md` links. Overwrite stale entries.

_Last updated: 2026-11-18_

## Release state
- `master` (PROD): **2.3.12**. `dev`: **2.3.13** (unreleased).
- Member release notes: `releases/<VERSION>.json` (`docs/release-and-rollback.md` → *Release notes*).

## Current
None. Last: the second cleanup round, tasks 120-128 (PRs #326-#333, merged into `dev`; audit of 2026-11-18). No
behaviour change except where noted.
- 120: test hosts without the 13 background workers; one copy of the test helpers (`tests/Shared/RepoPaths.cs`,
  `Api/AreaHttp.cs`); full suite 3 m 28 s → 2 m 11 s.
- 121: treasury report history in one query; seven notification loops send one batch (`SendToSelectedUsersAsync`).
- 122: `Endpoints/EndpointResults.cs` (one result mapper), `Helpers/ClaimsPrincipalExtensions.cs` (`user.IsMember()`,
  `user.UserId()`), shared endpoint filters, redundant repository overrides removed.
- 123: ten members with no production caller deleted; stale docs fixed.
- 124: the portal imports the request helper from `data/api`; one `queryString`, `Option` (`data/sharedTypes.ts`) and
  `fold`.
- 125: `IconAction` (`large`, `className`), `SearchField`, `SelectField` and the new `LinkNotice` replace 58
  hand-written copies.
- 126: `/meetings` loads its dialogs on first open (page chunk 15.7 → 6.5 KB gzip; the first dialog shows "A abrir…");
  `Avatar` decodes async.
- 127: not done. Merging duplicate CSS blocks moves rules in the cascade and nothing here can prove the look is
  unchanged (see Deferred).
- 128: rule tests for `TreasuryAuthorization`, `ShopAuthorization` and `ClaimsPrincipalExtensions`
  (`tests/Shared/TestPrincipal.cs`); visitors cannot read, create, vote, close or delete a poll (4,023 → 4,062 tests).

Before it, tasks 110-119 (PRs #314-#325): no Blazor leftovers; `portal/src` in `pages/`, `components/`, `data/`,
`styles/`; one shared UI set in `components/ui/`; no file over 300 lines except five that are one function each
(`components/MeetingAta.tsx`, `MessageThread.tsx`, `FamilyTree.tsx`, `Data/SeedData.Member.cs`,
`Services/AtaPdfService.cs`).

## Not verified yet
- Cleanup round 120-128 on DEV: the lazy `/meetings` dialogs on a phone and through a push link (`?meeting=`); the
  converted icon buttons and filters (Documentação, Loja, Inventário, event management, Perguntas, Pedidos, Reuniões,
  Votações) were checked only on a local run for `/meetings`, `/questions` and `/inventory`.
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

**CSS (phase 127, not done)**
- 69 groups of identical declaration blocks under different selectors (about 805 source lines: `display:flex;
  flex-wrap:wrap; gap:8px` ×14, a list reset ×24 differing only in gap, a focus ring ×7, `.lx-filters button` =
  `.pa-tabs button`). Merging them changes rule order; it needs a before / after computed-style comparison
  (`tests/responsive/responsive_audit.py` needs Playwright, which this machine's Python does not have). No CSS class is
  unused.

**Portal data layer (left after 124)**
- No shared load hook: pages turn an `Outcome` into state in nine shapes (`null`, `[]`, `'signin'`, `'forbidden'`,
  `'missing'`, `'failed'`, objects), 26 with a hand-written `live` guard; `useOwnerLoad` (`components/OwnerUi.tsx`)
  serves the Owner pages only. Converting them cannot be checked without a JS test runner.
- Still copied: the antiforgery token fetch (`signInApi.ts` ×2 and `publicRequestApi.ts` need the field name and a fresh
  token), the XHR upload (`galleryUploadApi.ts`, `logisticsApi.ts`), the sign-in status mapping (`passkey.ts`,
  `signInApi.ts`), `fold` variants (`hierarchyTree.ts` null-safe, `treasuryApi.ts` with `\p{M}`, `musicFormHelpers.ts`
  trims). `shopApi.ts` and `requestsApi.ts` build their own query on purpose (an empty `fiscalYear=` means every year).
- Same type name, different meaning: `PushResult` (`ownerApi.ts`, `push.ts`), `MemberDetail` (`leaderboardApi.ts`,
  `membersApi.ts`), `NoticeAudience` (`eventsTypes.ts`, `rehearsalsApi.ts`); the person shape is declared nine times.

**UI kit (left after 125)**
- Kept hand-written on purpose (different look or attributes): `lx-tabs`/`lx-views`/`news-tabs`/`playalong__group`
  button groups, `pa-pager`, OwnerUi `Flash`/`SearchBox`, 42 `span.control` fields, 45 standalone `form__hint` notes,
  radio and `role="switch"` rows, `form__label`-headed fields whose error has no id, MeetingAta required-marker fields,
  link-style icon actions.
- Icon buttons still hand-written (16): five use `aria-label` instead of the hidden span (`EventDiscussionComments.tsx`,
  `EventDiscussionLift.tsx`, `MeetingPollCreateDialog.tsx`, `RehearsalAttendees.tsx`), the menu buttons carry
  `aria-expanded` / `aria-controls` / `ref`, four are `<a>`. Search fields with `autoFocus` or `aria-controls`
  (`MeetingParticipantsDialog.tsx`, `MessageStart.tsx`, `FamilyTree.tsx`) and selects whose change handler does more
  than set a value (`LogisticsBoardToolbar.tsx`, `Meetings.tsx`) stay too.
- Not done: notices with an id or an icon (`EventDetail.tsx`, `NaipesConfig.tsx`), `MeetingPolls.tsx` "show all" notice,
  two `form__check` checkboxes (`EventManageForm.tsx`, `Request.tsx`), `LogisticsCard.tsx` conflict banner, `TextPager`
  in `ui/Pager.tsx` (one user).

**Tests**
- Gaps left after 128: `CalotesNotificationBackgroundService` has no test; `TreasuryService`, `TreasuryRecordsService`,
  `ProductShopService`, `ShopPromotionService` and `FinanceManagementService` are tested only through the API; 14
  `*Authorization` helpers have no direct rule test (Treasury and Shop do); no test that every push sender writes the
  inbox copy. `TreasuryAuthorization`'s role checks do not ask `IsMember` themselves (the services check the sign-in
  first). About 20 test classes each keep a private `SignedIn(...)` helper that `TestPrincipal` could replace.
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
- Leaderboard comment/like push failures are swallowed with no log; discussion pushes stop at the event start (UTC
  compare).
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
- Left by task 126: `StartupModals` (with `data/push.ts`) stays in the entry chunk because `App.tsx` also needs
  `resetStartup` from the same module; `Home` is static in `main.tsx` (the LCP page: an owner decision);
  `LogisticsBoard` (19 KB gzip) and `Shop` likely ship dialogs the same way `/meetings` did; four avatar images bypass
  `Avatar` (`ui/PersonChip.tsx`, `MusicAlbumForm.tsx`, `MusicStatistics.tsx`, `GalleryFormParts.tsx`).
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
- Kept by task 123, used only by tests: `INerbaOrderService.GetAllAsync` / `GetByIdAsync` / `GetByEventIdAsync`,
  `IMbwayTransferService.GetAllAsync` and `IRehearsalAttendanceService.GetAttendanceStatsAsync` (the money and
  attendance tests read their writes back through them); five `IRankingService` methods,
  `IDiscussionService.CreateForEventAsync`, `IMemberInstrumentService.HasInstrumentAsync` and
  `IProductReservationService.GetByProductAndUserAsync` are called only inside their own service.
  `ApplicationDbContext.Transportations` has no reader (dropping the `DbSet` could rename the table in the model).
  `RTUB.csproj` names four packages it also gets through `RTUB.Application`.
- `AspNetUsers.ExperiencePoints` / `Level` have no writer (the leaderboard computes XP per request): DB cleanup candidate.
- `PostMedia` / `CommentImages`: production row counts needed before dropping (`docs/db-cleanup-inventory.md`).
- Kept on purpose: npm `bootstrap-icons` (path-data source for `icons.tsx`).

**Tooling and dependencies**
- `npm audit`: 5 high in vite 6 dependencies (build time only).
- Dependencies to bump: Microsoft 10.0.x train (unblocks `MockQueryable.Moq`), AWSSDK, QuestPDF 2026.x major.
- `xUnit1051` suppressed.
- `refresh-dev-database.yml` and Deploy • DEV use different concurrency groups (a push during a refresh can deploy
  mid-refresh); `refresh-dev-database.yml` still pins `checkout@v4` / `setup-dotnet@v4`.
- Local runs read `ConnectionStrings:SqliteConnection`; point it at a scratch copy or the run writes to
  `src/RTUB.Web/app.db`. A local run without `Cloudflare:R2:PublicUrl` blocks R2 images in the CSP (local only).
- GitGuardian: historical incidents stay historical; mark them by hand.

## Next
Owner decides the next task (GitHub Issues). Decisions the 2026-11-18 audit left to the owner: squash the 180 migrations
(363 files, 686,048 lines); drop the unused columns (`MemberStatus.OverrideRetired`, `AspNetUsers.ExperiencePoints` /
`Level`, `MeetingAtaAttachment.FileUrl`, `Report.PdfData`); the empty 401 of `PushEndpoints` / `ReleaseEndpoints`; the
`NoStore` filter missing on the two public Gallery GETs; a lazy `Home`; splitting the five files over 300 lines.

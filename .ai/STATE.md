# .ai/STATE.md

Living execution state. **Read this first.** A status board, not a diary: history is in git and GitHub, durable detail
in the docs `CLAUDE.md` links. Overwrite stale entries.

_Last updated: 2026-11-24_

## Release state
- `master` (PROD): **2.3.18** (released 2026-11-24, tag `v2.3.18`, PR #372: Trintuna, tasks 153-157; no member release notes, owner). `dev`: **2.3.19** (unreleased).
- Member release notes: `releases/<VERSION>.json` (`docs/release-and-rollback.md` → *Release notes*).

## Current
Task 160 (`feat/160/trintuna-mark-attendance`, Issue #375, PR #376, Codex ACCEPTED round 1, merged into `dev`): on a past gathering the Owner or its
organizer marks who went (Fui) and removes a Fui (`PUT`/`DELETE /api/trintuna/gatherings/{id}/attendees/{key}`); a mark
is the person's own answer row, so Quem foi and the Hall of Fame count it. Owner checks (plan defaults applied): any Fui
can be removed, a mark overwrites Não fui, everyone who can see the page is offered.

Task 157 (`fix/157/trintuna-menu-last`, PR #371, merged, released in 2.3.18): Trintuna is the last link of the Tuna menu.

Task 156 (`fix/156/trintuna-answer-on-click`, PR #370, merged into `dev`): Trintuna Vou / Fui saves on click, no
Guardar / Fechar in the answer. Not checked in a browser: check the dialog on DEV.

Task 155 (`feat/155/trintuna-hall-of-fame`, Issue #368, PR #369, Codex ACCEPTED round 4, merged into `dev`): Trintuna number given by the server (highest + 1,
retry on the unique index, no number in the form), `GET /api/trintuna/organizers` (Owner(s) + list) for the Organizador,
Fui / Não fui on past gatherings, Hall of Fame (everyone who can see, zeros included, 15 per page) replacing the Top 3,
share link `/trintuna?almoco={id}` with "Copiar link". Owner checks: Owners listed with 0 in the Hall of Fame; a shared
link from WhatsApp in the installed Android app; DEV numbering continues at 8º.
Task 154 (`feat/154/trintuna-attendance`, Issue #366, PR #367, Codex ACCEPTED round 6, merged into `dev`): Trintuna Vou / Não vou on past gatherings with who
answered, a Top 3 dialog (dense places, ties share a medal), the 7 almoços as a dev seed only (none on PROD; DEV keeps its
#365 rows, 7º historical), one shared select (`SelectField`) for the whole portal. Not shot: logistics board toolbar and
templates, Pager, ata, calendar offer, shop promotions (no data in the scratch DB); check on DEV.

Task 153 (`feat/153/trintuna`, Issue #364, PR #365, Codex ACCEPTED round 4, merged into `dev`): `/trintuna`, the Trintuna gatherings (5 new tables,
migration `AddTrintunaGatherings`, 7 seeded historical gatherings), date poll settled from its own options, Vou / Não vou,
one Owner-managed access list. `docs/react-trintuna.md`. Owner checks: real dates and organizers of 1-7, the menu icon,
whether presence names are listed. Not checked in a browser: the create/edit form and settling a date
(API tests and source pins only); check on DEV.

Task 150 (`fix/150/chat-avatars`, PR #359): others' chat messages show the sender's photo (Messenger style, by the last
bubble of a run; `ThreadMessage` `lastOfRun`, `.bubble__face`), released in 2.3.17. Not checked in a browser: check
on PROD (group, direct, Notificações RTUB, phone width).

Task 147 (`chore/147/unmap-post-media`): `PostMedia` / `CommentImages` (0 rows on PROD, 2026-11-19 backup) are unmapped:
entities, repositories, delete clean-up and the dashboard uploads entries removed; `StopMappingPostMediaAndCommentImages` is
model only (empty Up/Down), the tables stay. The drop is task 148 (`chore/148/drop-post-media-tables`, `20261119170000_DropPostMediaAndCommentImages`), a draft PR:
merge only after the release with 147 is live on PROD and its rollback window is over; backup and row count first.

Task 141 (`chore/141/readable-portal`), tenth phase of the audit round. Same UI, smaller pieces (proof: `tsc`, the
source pins; no browser run, no component tests):
- `MeetingAta.tsx` 228 + `data/useAtaEditor.ts` + `MeetingAtaBoard.tsx` + parts in `MeetingAtaParts.tsx`.
- `LogisticsBoard.tsx`: the nine address-held filters are one `filters` object (`BoardFilters`, `withoutCard` in
  `data/logisticsBoardUrl.ts`); `back()` is one update.
- `MessageThread.tsx` 106 + `data/useThread.ts` + `ThreadHeader` / `ThreadNotice` / `ThreadMessage` in `MessageParts.tsx`.
- `FamilyTree.tsx` 68 + `data/useTreeNavigation.ts` + `FamilyTreeParts.tsx`; magic numbers named.

Task 140 (`chore/140/readable-backend`), ninth phase of the audit round. Same behaviour, smaller pieces:
- Seed members: `SeedData.Member*.cs` (3 files), `.Owner.cs`, `.FiscalYearAndRoles.cs`, `.Mentors.cs` (a nickname
  table); pinned first by `SeedDataMemberSnapshotTests` (83 users, 68 mentor pairs, 12 roles, a hash of every user).
- Ata PDF: `AtaPdfService.cs` + `.Sections.cs` + `.Signatures.cs`; pinned first by `AtaPdfServiceGoldenTests` (six
  cases, SHA-256 of the PDF bytes with dates blanked; byte-identical after the split).
- Member delete in phases (`UserProfileService.Delete.cs`), pinned first by `UserProfileServiceDeleteTests`.
- `ApplicationDbContext.SaveChanges.cs`: timestamps and audit entries in their own methods (`.SaveRetry.cs` holds the
  retry). `LogisticsEndpoints.cs`: `s` is `logistics`. `release.sh` sources `release-checks.sh` (notes-check,
  schema-gate) and `release-self-test.sh`; the self-test prints the same 77 lines.

Merged and not released yet (they matter at the next release): task 130 squashed the 180 migrations into
`20261118134743_Baseline` (`Data/MigrationBaseline.cs`; Rollback • PROD to 2.3.12 or older needs `allow_newer_schema`);
task 131 stopped using five columns, which stay until the drop release (`docs/db-cleanup-inventory.md` → *Pending
drop*); task 132 taught `release.sh` `cmd_schema_gate` the squash; task 133 fixed five data-loss bugs
(`docs/react-members.md`, `docs/react-treasury.md`, `docs/react-member-area.md`); task 134 put fiscal years, discussion
pushes and the calotes reminder on Portugal's clock (`docs/backend-practices.md` → *Portugal time*); task 135 deleted
the download proxy, rate-limited `/api/account/*`, built email links from `App:BaseUrl` (PROD `https://www.rtub.pt`,
DEV `https://rtub-dev.azurewebsites.net`, owner 2026-11-18) and limited gallery uploads to seven checked types
(`docs/react-identity.md`, `docs/react-gallery.md`); task 136 added rule tests for the 14 `*Authorization` helpers
(`tests/RTUB.Application.Tests/Helpers/`, `TestPrincipal.WithRole` / `NotSignedIn`); task 137 built the endpoints
once per start (Integration tests about 175 s → 72 s) and added the `Local` launch profile (`docs/configuration-runbook.md`); task 138 guarded nine racing portal loads
(`useLoad`, `useLatest`; `docs/frontend-practices.md`); task 139 moved the startup dialogs and the board dialogs
into their own chunks and made `sw-register.js` `no-cache` (`docs/performance-gate.md`).

Before it: the second cleanup round, tasks 120-129 (PRs #326-#334, merged into `dev`; audit of 2026-11-18). No
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

- Trintuna (153): `Gathering` and `TrintunaAccess` have no display name in `ApplicationDbContext.EntityDisplay.cs`;
  the audit shows the raw type.

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
- `useLoad` exists since task 138 (the Owner pages and Aniversários use it). About 45 mount-once loads still turn an
  `Outcome` into state by hand, in eleven shapes; they cannot race. Converting them cannot be checked without a JS
  test runner.
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
- `AuditLogAppender` still handles binary properties, but no entity has one since task 131 (its test went with
  `Report.PdfData`).
- Gaps left after 136: `TreasuryService`, `TreasuryRecordsService`, `ProductShopService`, `ShopPromotionService` and
  `FinanceManagementService` are tested only through the API. `TreasuryAuthorization`'s role checks do not ask
  `IsMember` themselves (the services check the sign-in first). `GovernanceAuthorization.CanViewRgi` asks for a system
  role, "Member" included: an account with no role does not read the RGI.
- One host per integration test class (75 hosts; a warm host starts in about 2 s since task 137, half of it building
  the endpoints). A shared host for the 28 `IntegrationTestBase` classes was tried before 137 (130 s → 87 s, 3 tests
  failed: `HomePreviewTests` needs an empty database, `AuthAntiforgeryTests` hit the per-IP login limit); not retried.
- A new database is broken for member status: the Baseline creates `MemberStatuses.OverrideRetired` NOT NULL with no
  default, and the app no longer writes the column (task 131), so every insert fails ("NOT NULL constraint failed",
  seen on an empty `.local/app.db`). PROD and DEV are not affected: their column has `DEFAULT 0` from the old chain.
  Fixed by `DropUnusedColumns` (task 142, draft PR): an empty database then creates its member status rows.
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
- Leaderboard comment/like push failures are swallowed with no log.
- `IPushNotificationService` takes no `CancellationToken`; 5xx from a push service is not retried; fan-out runs inline
  in requests. `WebPush` 1.0.13 sends legacy `aesgcm` (move to RFC 8291/8292 only if
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
- Shop: a reservation snapshots no product name or price and is deleted with its product; `InFiscalYear` compares UTC
  `CreatedAt` with local-calendar bounds.
- Deleting an instrument leaves its thumbnail in R2; "Remover imagem" on `/naipes/config` leaves the R2 object.
- `NewsEndpoints.cs` 403 title names system roles (API callers only; the portal shows its own message).
- React `/roles` probes `GET /api/governance/manage`, logging a 403 console error for members without rights.
- `portal/index.html` `theme-color` is `#000000`; the manifest says `#3F2A86`.
- `XpSettings` and `RankingConfiguration` both bind `Ranking`; equal level+XP keep SQLite's unordered order.
- Play cooldown and the system announcement live in one app instance's memory (a scale-out needs a shared store).

**Performance** (`docs/performance-gate.md`)
- `Home` is static in `main.tsx` (the LCP page: an owner decision); `Shop` dialogs are in its chunk (about 2-3 KB).
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

**Found by the 2026-11-18 audit for tasks 132+ (new; not fixed)**
- Data: `UserProfileService.Delete.cs` still deletes a member's data in about 20 steps with no transaction (the
  repositories and `UserManager` each use their own context), so a failure half-way leaves part deleted; task 133
  removed the two known causes. A member named on an MBWay transfer cannot be deleted until the transfer is changed
  (owner decision: keep, or clear the member on the transfer). A reservation never lowers stock (owner decision).
  Meeting atas are last-write-wins (a 409 needs a version column, as `LogisticsCard.Version`). `EnrollmentService.cs:67`
  was not checked for the same double-submit 500.
- Dates: still on the server's clock (`DateTime.Now` / `Today`): the latest year a member form accepts
  (`MemberAdminService.Info.cs:68`, `.Rules.cs:68,77`, `MyProfileService.Profile.cs:51,122`), "not in the past" on a
  public request (`RequestValidationService.cs:23,37`), `Album.cs:41,59`. `FiscalYearHelperService` ends the year at
  23:59:59, so the last second of 31 August is in no year (requests filter only).
- Security: `AllowedHosts` is still `*` (owner chose the base URL setting, 2026-11-18); forgot-password answers a real
  account more slowly and has no per-email cooldown; seven email models hard-code `rtub.azurewebsites.net/profile`
  (`PreferencesLink`); five services keep a private `LooksLikeImage` that `MediaSignature` could replace; the portal's
  gallery input still offers every `image/*` / `video/*`; every API group is `AllowAnonymous()` and the checks live
  only in the services; member emails are logged at Information level (`EmailNotificationService.*`).
- Release: the daily backup keeps two generations, needs Always On and alerts nobody on failure; no test restores a
  backup; `release.sh` checks only that VERSION goes up, not the MAJOR bump of a drop release; `AdoptAsync` writes the
  Baseline row before the pre-migration snapshot.
- Local: a database that stopped before `20261117191014_AddAprenderViewsAndStars` is refused by `dev`; start commit
  `5d1b9cb9` (or release 2.3.12) against it once.
- Measured on a local copy (108 users, 17,761 messages): no `/api` GET over 95 ms, every SQL command 0 ms;
  `/api/me/profile` 15 queries, `/api/members` 7, `/api/leaderboard` 7. Every route loads 137 kB gzip first.

## Next
Phases the audit proposed, each waiting for the owner's "yes, phase N": 140 / 141 readability (backend, portal), 142 the drop below as a draft PR. The
portal intro animation is not to be removed or shortened. The drop release does not go to 3.0.0 (owner, 2026-11-18): the
"MAJOR bump" wording in the docs changes with phase 142. Not proposed: the CSS merges, backend query work.

After the release with tasks 130-131 is live on PROD and its rollback window is over: merge the draft PR of task 142
(`chore/142/drop-unused-columns`, `20261119020000_DropUnusedColumns`, rehearsed on a copy of the local snapshot; not
3.0.0, the owner picks the number; production backup first; `docs/db-cleanup-inventory.md`). Other owner decisions left by the audit: the empty 401 of `PushEndpoints` /
`ReleaseEndpoints`; the `NoStore` filter missing on the two public Gallery GETs; a lazy `Home`; the five files over 300
lines.

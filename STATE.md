# STATE.md

Living execution state. **Read this first.** A status board, not a diary: history is in git, and
durable detail lives in the docs linked below.

_Last updated: 2026-10-05_

## Phase
**Open: `feat/006/react-music-refactor`** (React track, task 006; from `dev` @ `94b77e4a`, committed
locally, not pushed; DEV only). Music is React: `/music`, `/music/albums/{id}` (old `/music/songs/{id}`
redirects), thin `/api/music` endpoints over a new `MusicService` with `MusicAuthorization`; server-side
play cooldown; Blazor Music UI and its circuit-only services retired. No schema change. Audit, rules,
API and follow-ups: `docs/react-music.md`. Next: review, PR → `dev`, then a DEV check with real audio
(local runs have no R2 credentials).

**Open: `fix/006-ci-red-after-music`** (from `dev` @ `2a2d8e89`). CI went red on 2026-10-01: 8 member/retirement
status tests built "current month" activity as `now.AddDays(-n)`, which is last month on the 1st. Fixtures
now use a moment earlier in the current month. One was a real bug: `RetirementStatusService` counted
consecutive months from the current month even before it had activity, so on month starts a retired member
with 3 full months never returned to active; it now counts from the last completed month, as
`MemberStatusService` already did. No schema change.

**Open: `fix/010-public-shell-polish-real-previews`** (React track 010, from `dev` @ `c007e207`; local, not pushed; DEV only).
Home agenda shows the next 3 events (`GET /api/public/events/upcoming`, read-only, not the Events rebuild);
home gallery shows the latest public photos (`GET /api/gallery?public=true`). Copy fixes (Música, Junta-te,
Órgãos Sociais, Pedidos, no gallery members teaser); one shared `.control` for search/selects and `.back-link`.
Also removes the old home `.tile` CSS that leaked into the 009 gallery tiles. No schema change.

**Open: `fix/0115-react-events-polish-admin-actions`** (React track 011.5, from `dev` @ `72200cd3`; 011 itself
is merged; local, not pushed; DEV only). Admin/Owner create, edit and delete events in React modals on
`/events` (`POST`/`PUT`/`DELETE /api/events`, antiforgery, 403 for Mod/Member); delete keeps the old hard
delete + cascades and now refuses events with NERBA orders (409). "Quem vai" / "Quem foi" (past) with
avatar tiles; no Blazor management links from the event page. `/member/events` stays only for advanced
management. Answers stay a modal; Prémios stays a modal. No schema change. Terminology: events =
enrollment / inscrição; rehearsals = attendance / presença. Detail: `docs/react-events.md`.

**Open: `feat/012e-react-event-participants`** (React track 012E, from `dev` @ `c264c7c9`, 012D merged; local, not
pushed; DEV only). Admin/Owner add (as going, primary instrument, no notification) and remove other members'
answers from **Gerir inscrições** on `/events/{id}` (`/api/events/{id}/enrollments`, antiforgery, 401/403
server-side, via `IEnrollmentService`); expelled members not offered, duplicates and cancelled events refused.
"As minhas inscrições" is React at `/events/my-enrollments`. The Blazor `/events/{id}/enrollments` page is
retired (302 to the event page) and `/member/events` lost its participants modal and Minhas Inscrições; it keeps
statistics, read-only videos/repertoire and the details-only edit. Contacts is linked from the event page (Mod+).
No schema change. Detail: `docs/react-events.md`.

**Open: `feat/023-react-logistics-kanban`** (React track 023, from `dev` @ `e6a0a837`, 022 merged; local, not pushed;
DEV only). **Logística is React:** `/logistics` (boards) and `/logistics/{id}` (Kanban) over `/api/logistics`
(`LogisticsKanbanService`, same tables): boards CRUD + finish/reopen, lists (now reorderable), cards with status,
labels, dates, checklist, links, members, drag and drop or "Mover…" (moves renumber positions), label/status/search
filters, board files (old per-board folder), reminders (any member; stored, still never sent). Members read; Mod, Admin
and Owner (Owner now inherits Admin) manage; Leitões refused on both pages. Blazor pages and `kanban.js` retired. No
schema change. Detail: `docs/react-logistics.md`.

**Open: `feat/022-react-documentation`** (React track 022, from `dev` @ `3d3b558c`, 021 merged; local, not pushed; DEV
only). **Documentação is React:** `/documentation` (members; visitors 302 to sign in; Leitões refused unless Owner) over
`/api/documentation` (`DocumentationService` over the old `IDocumentStorageService`; documents live only in R2, no table):
folders of one fiscal year (current by default) in storage order, Logistics boards as folders, the old Atas CV / Atas AG
visibility, pre-signed attachment downloads, search; any member uploads into a folder they see (same name refused unless
Owner, who replaces); Owner creates/deletes folders and deletes documents. Rules now server-side; keys never leave the
server. Blazor `Documentation.razor` retired. No schema change. Detail: `docs/react-documentation.md`.

**Open: `feat/021-react-shop`** (React track 021, from `dev` @ `34b57e08`, 020 merged; local, not pushed; DEV only).
**Loja is React:** `/shop` (members only; visitors 302 to sign in) over `/api/shop` (`ProductShopService` over the old
product and reservation services; not the MyTuno `ShopService`): products by type then name, current fiscal year by
default, search and type filter; members reserve members-only products in stock (one each, optional size and display
name, stock unchanged) and cancel their own; Mod adds products, Admin and Owner edit, delete, change images and see /
delete every reservation. Blazor `Shop.razor` retired. No schema change. Detail: `docs/react-shop.md`.

**Open: `feat/020-react-inventory`** (React track 020, from `dev` @ `0b2d3bcd`, 019 merged; local, not pushed; DEV
only). **Instrumentos is React:** `/inventory` (members only; visitors 302 to sign in) over `/api/inventory`
(`InstrumentInventoryService` over the old `InstrumentService`): list by name, old counters, search and type/condition
filters, details for any member; Mod, Admin and Owner create, edit (type fixed, as before), set image + cropped
thumbnail, and delete (old hard delete). Blazor `Inventory.razor` retired. `/shop` (Loja + reservations) stays Blazor as
its own module. No schema change. Detail: `docs/react-inventory.md`.

**Open: `feat/019-react-leaderboard-classification`** (React track 019, from `dev` @ `3219ec99`, 018 merged; local, not
pushed; DEV only). **Classificação is React:** `/leaderboard` (members only; visitors 302 to sign in) over
`/api/leaderboard` (`LeaderboardService`): same XP (configured per rehearsal and event type, computed on the fly), every
account listed, level-then-XP order, search and fiscal-year filter, details with the all-time XP origin, comments
(any member writes and likes; author or Admin/Owner deletes; push via the old service) and the "ranking_story" text
(Admin/Owner). Blazor `Leaderboard.razor` retired. No schema change. Detail: `docs/react-leaderboard.md`.

**Open: `feat/018-react-member-admin`** (React track 018, from `dev` @ `f38ec364`, 017 merged; local, not pushed; DEV
only; rebuilt on this machine because the first 018 was never pushed). **Member admin is React:** on `/members`
Admin/Owner add and edit members, instruments, set a Leitão's nickname, expel/reactivate Leitões, "Tornar ativo" and send
the push reminder; Owner deletes any member, Admin Leitões only (old hard delete). Thin writes on `/api/members`
(antiforgery, 401/403 server-side) over `MemberAdminService`; `/members/manage` 302s to `/members`, `Members.razor`
retired. Email/push only through fakes in tests. No schema change. Detail: `docs/react-members.md`.

**Open: `feat/017-react-members-hierarchy-classification`** (React track 017, from `dev` @ `c5211135`, 016 merged; local,
not pushed; DEV only). **Members is React:** `/members` (directory with the old filters, details, active members,
birthdays) and `/members/hierarchy` over read-only `/api/members`, signed-in members only; `/hierarchy` 302s there. The
old page's admin tools stay Blazor at `/members/manage` (Admin/Owner only) until a member-admin task; classification
(`/leaderboard`) deferred to its own task. No schema change. Detail: `docs/react-members.md`.

**Open: `feat/016-react-governance-management`** (React track 016, from `dev` @ `84193767`, 015 merged; local, not
pushed; DEV only). **Órgãos Sociais is fully React:** on `/roles` members open the RGI and Mod, Admin and Owner (was
Mod and Admin) add fiscal years and assign / remove positions in modals, over `/api/governance` (rules now server-side,
existing validation and role promotion reused); `/member/roles` 302s to `/roles`. Expelled members no longer offered.
No schema change. Detail: `docs/react-governance.md`.

**Open: `feat/015-react-gallery-management`** (React track 015, from `dev` @ `84d871de`, 014 merged; local, not
pushed; DEV only). **Gallery is fully React:** members upload on `/gallery` (image/video, 10/100 MB, title, date,
members-only, who appears; the people tagged get one push, as before) and the uploader, Admin or Owner (was Admin
only) edit and delete from the lightbox; `/member/gallery` 302s to `/gallery`. Rules now server-side; storage
unchanged (members-only is a listing rule: files stay `PublicRead`). No schema change. Detail: `docs/react-gallery.md`.

**Open: `feat/014-react-rehearsals`** (React track 014, from `dev` @ `8a36ecde`, 013 merged; local, not pushed; DEV only).
**Rehearsals are React:** `/rehearsals` (list, quick "Marcar presença", "As minhas presenças", statistics) and the new
`/rehearsals/{id}` (details, Presenças with confirm/remove/add) over `/api/rehearsals`; visitors get a 302 to sign in.
Rehearsals use presença/attendance, events inscrição/enrollment. Old UI-only rules are now server-side; management is
Admin or Owner (was Admin only); Mod none. Changed on purpose: description/notes typed at creation are saved. The
Blazor page and its rehearsal-only components/services are retired. No schema change. Detail: `docs/react-rehearsals.md`.

**Open: `feat/013-react-event-discussion-contacts`** (React track 013, from `dev` @ `76388369`, 012F merged; local,
not pushed; DEV only). **Events is fully React:** `/events/{id}/discussion` (members: a post/comment feed with lift
offers, pin/lock, `@mentions`) and `/events/{id}/contacts` (Mod and above) are React pages over
`/api/events/{id}/discussion` and `/api/events/{id}/contacts`; visitors get a 302 to sign in. The old rules, which
Blazor only hid in the UI, are now server-side. Changed on purpose: contacts (every member's phone) readable by Mod
and above only, not any member; expelled members not listed; post title optional; no post media upload (0 used),
mention autocomplete, search or pagination. No Blazor event page left; no schema change. Detail: `docs/react-events.md`.

**Unit 030 - production release pipeline.** Repository work (S1-S6) is **merged to `dev`** (PR #203,
`8bc9b61d`). **No Azure resource, GitHub setting or production app was changed.** The production path
goes live only through the owner actions below, then a `dev → master` merge.

**Open: `fix/030/ios-pwa-media-association`** (from `dev` @ `8bc9b61d`, uncommitted). iPhone PWA: the
lock-screen card shows RTUB's song, but tapping it can open another installed web app (LoreX). Most
likely iOS/WebKit Home Screen routing behaviour: RTUB-side identity and navigation causes were checked
and excluded. RTUB's manifest `id` `/` resolves to its own origin root, which LoreX (another origin,
`id` `/app`) cannot share; one manifest, one service-worker registration, one Apple title. `id` kept:
under the manifest spec a different id describes a distinct app, not a replacement for existing
installs. Fixed here: the album player's lock-screen session is released on ✕ and on leaving the page,
and bound again to each new `<audio>` (it used to stay on the removed one). Tests: identity contract in
`PwaManifestTests` (+2), lifecycle in `SongsPageTests` (+2). Next: owner DEV check on an iPhone, then
PR → `dev`, before step 14.

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
As of 2026-09-22 nothing below had been done; step 1's merge has since landed (PR #203). Items 1-11 do
not touch the running production app; 12 restarts it.

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
Raised by React track 023 (Logística):
- `LogisticsCardReminders` are stored but no job sends them (pre-existing); build delivery or drop the feature.
- `ILogistics{Board,List,Card}Service` (+ tests), `BoardCard` (Shared) and `css/3-components/kanban.css` have no page caller.

Raised by React track 022 (Documentação):
- `FolderCard`/`DocumentCard` (Shared, + bUnit tests) and `css/3-components/folder-card.css`/`document-card.css` have no page caller.
- React `/roles` probes `GET /api/governance/manage`, logging a 403 console error for members without management rights.

Raised by React track 021 (Loja):
- `ReservationCard` (Shared) has no caller; "Público (não membros)" never reached visitors (shop is members-only).

Raised by React track 020 (Instrumentos):
- (021) `/shop` is React.
- Deleting an instrument leaves its thumbnail in R2; `InstrumentCircle` (Shared) has no caller.

Raised by React track 019 (Classificação):
- `LeaderboardCard` and `LeaderboardCommentItem` (Shared) have no caller since `Leaderboard.razor` went.
- `XpSettings` and `RankingConfiguration` both bind `Ranking`; equal level+XP keep SQLite's unordered users order.

Raised by React track 018 (Member admin):
- `IMemberPositionService`, `IUserRoleQueryService` and the shared `AvatarCard` have no caller since `Members.razor` went.
- Owner can delete their own account from the tools (as before).

Raised by React track 017 (Members):
- (018) `/members/manage` retired; its tools are on the React `/members`.
- `wwwroot/js/familyTree.js` (still loaded by `MainLayout`) and `.family-tree-*` in `css/1-base/mobile.css` are unused.
- Hall of Fame and the member map are still Blazor ((019) classification is React).

Raised by React track 013 (Events):
- Global CSS for the retired `PostCard`, `CommentItem` and the PostComposer mention dropdown
  (`wwwroot/css/3-components/*`) is unused now; `PostMedia` upload code in `PostService` has no caller left.

Raised by React track 012F (Events):
- `IEnrollmentFilterService` / `EnrollmentFilterService` are registered but unused since the Blazor enrollments
  page went (012E); not removed here.
- Local runs read `ConnectionStrings:SqliteConnection` (not `DefaultConnection`); point it at a scratch copy before
  any browser check, or the run (and its `MemberStatusUpdateBackgroundService`) writes to `src/RTUB.Web/app.db`.
- `EventCard` is only referenced by the dead `AboutUsContent` (Components/Portal, dead since 004).

Raised by React track 011 (Events):
- A local run without `Cloudflare:R2:PublicUrl` leaves the R2 origin out of the CSP, so event images and
  videos are blocked locally only (DEV/PROD have the setting).
- (012F) `/member/events` retired; (013) discussion and contacts are React: no Blazor event page left.

Raised by React track 009 (Gallery):
- Gallery files are `PublicRead` in the public R2 bucket: members-only means "not listed", not
  "not reachable" by a leaked URL. Private objects + signed URLs would be a storage change.
- (015) Gallery management is React; Owner now inherits Admin there.

Raised by React track 008 (Órgãos Sociais):
- (016) Management and the RGI are React on `/roles`; `/member/roles` redirects. Owner now inherits Admin there.

Raised by React track 007 (Login):
- `POST /auth/login` reports Locked/Expelled before checking the password, so those states are
  visible to anyone who knows a username (pre-existing; kept to preserve behaviour).
- Sign-out: React `/profile` has "Terminar sessão" since 012F; the React top bar still has none.

Raised by 006 (shell banners):
- `VersionTests.VersionFile_IsStrictSemVer_WithNothingElseInIt` fails on Windows checkouts (`core.autocrlf`
  gives `VERSION` a CRLF); green on CI. A `.gitattributes` `VERSION text eol=lf` rule would fix it.
- Portal `index.html` loads `/js/sw-register.js` unversioned (stale-while-revalidate), so one visit after a
  deploy can still run the old script.

Raised by React track 006 (Music):
- `AlbumAccesses` holds 18 stale rows for album 12 (not exclusive); ignored by the rules, not deleted.
- Retired-Music leftovers still loaded globally: `css/3-components/music.css`, `song-card.css`,
  `audio-player.css`, and the PWA-queue half of `pwaMediaSession.js`/`MediaSessionInterop`.
- Play cooldown is per app instance (in-memory); a scaled-out App Service would need a shared cache.

Raised by React track 005 (content audit):
- **Security:** `/calotes`, `/mbway` and `/nerba/{id}` have no `[Authorize]` (no folder-level rule
  either), so anonymous visitors can open these finance pages. Not changed in 005; review what they
  render for an anonymous user and gate them.
- The old home's text was admin-editable (Labels, `/labels`); the React copy is static. Decide a
  read-only public labels API vs static copy before the PROD cutover.
- Public slideshow (`/images` admin) has no React counterpart; revisit with the Gallery module.

Raised by React track 004 (React `/`):
- The Blazor home was the only place rendering `PushNotificationPrompt`, `LoginPopup` and
  `PlayStorePrompt`; since 004 nothing shows them (the PWA `start_url` now opens React `/`). Decide
  whether they move to the React shell or a Blazor layout.
- Dead since 004 (only `Index.razor` used them): `Components/Portal/*` (AboutUs, JoinUs, History,
  Hierarchy, Fitab, PortalSectionNav), `wwwroot/js/home.js`, `scrollSpy.js`, the homepage CSS,
  `PortalContentStyleTests`, `LoginPopupOptions`.
- Email templates' `PreferencesLink`/`ProfileUrl` (`…/profile`) now land on the React member entry,
  one tap from the Blazor editor at `/member/profile`.

Raised by React track 001-003 (portal pilot):
- CI • Build & Test has no Node step for the portal bundle (command in `docs/react-portal-pilot.md`).
- Before the next `dev → master` release the owner must accept (or replace) the React home's artwork
  gallery tiles and static copy. (005: banner is pre-release only; no invented agenda dates.)
- `public-requests` rate limit partitions on `RemoteIpAddress`: confirm forwarded headers on DEV/PROD.
- Real `app.db`: 8 non-dated IDs in `__EFMigrationsHistory` (seen read-only in task 003; unexplained).
- The Phase text below predates units 032-038 and the 2026-09-30 UI rollback; not rewritten here.
- Windows-only local test noise: an ignored `src/RTUB.Web/publish/` output, when present, fails 5 guard
  tests that sweep `src/` on disk; `VersionTests` fails on the CRLF working copy of `VERSION`
  (`core.autocrlf=true`). CI is unaffected.
- `MessagesHubTests.SendTypingStarted_WhenUserNotParticipant_DoesNotNotify` fails when its class runs
  from a fresh checkout and passes alone (order-dependent; reproduced at `95441bd6`).
- A local run without `Cloudflare:R2:*` settings returns 500 on storage-backed Blazor pages (`/events`,
  `/music`, `/gallery`, `/roles`; `/` is React since 004); the integration tests' placeholder values
  avoid it.

Raised by fix/030 (iOS media association):
- `MainLayout` `<HeadContent>` repeats `mobile-web-app-capable`, `apple-mobile-web-app-capable` and
  `apple-mobile-web-app-status-bar-style` from `App.razor` (identical values, harmless).

Raised by 030:
- `UpdateCardStatusEnumValues` and `NerbaOrderEventRequired` migration classes have no `[Migration]`
  attribute, so EF has never run them anywhere. Both hold destructive SQL; do not "fix" them by adding
  the attribute without deciding what they should do.
- `refresh-dev-database.yml` uses concurrency group `refresh-dev-database`, Deploy • DEV uses
  `deploy-dev`: a push to `dev` during a refresh can deploy mid-refresh. Also still pins
  `checkout@v4`/`setup-dotnet@v4`. Left byte-identical by instruction.
- (023) Logistics is React; the API refuses Leitões on every board (the old board page did not).
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

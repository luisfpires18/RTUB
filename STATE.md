# STATE.md

Living execution state. **Read this first.** A status board, not a diary: history is in git, and
durable detail lives in the docs linked below.

_Last updated: 2026-10-06_

## Phase
**Open: `feat/032-react-member-profile-map-hall`** (from `dev` @ `cf70eee3`; DEV only), holding **032 and 033**:
032 (`3c90682`) is pushed to origin; **033 is committed on top and not pushed**. **UNVERIFIED cloud patch** (no `dotnet`
here): the C# services, endpoints and tests of both were written but never built or run here; `check:portal` +
`build:portal` and scratch browser checks (mock API with the real CSP header; Owner / Tuno / Leitão / visitor; 320, 375
and 1280px) passed. **GitHub Actions must pass before merge.**

**033 Naipes:** `/naipes` and `/naipes/config` are React (`/api/naipes*`, `NaipeBoardService` over the old
`NaipeService`: R2, audit log, push on a new item unchanged); signed-in members only, settings Admin / Owner (others get
a clear refusal). Changed on purpose: Owner inherits Admin; uploads are an allowlist checked by bytes and stored with the
server's type (SVG / HTML / renamed files refused; MKV / AVI dropped, GIF accepted); lengths, sizes and order enforced
server-side; a hidden instrument stays hidden by URL; picked instrument and search in the URL. Retired: both Blazor
pages, `NaipeCard`, `NaipeCommentItem`, `DetailsModal`, `InfoSection`, `ProfileField`, `naipe-card.css`,
`details-modal.css`, `naipes-config.css` and their bUnit tests. **11 Blazor routes left** (13 before). No schema change.
Detail: `docs/react-naipes.md`.

**032:** **`/profile` is now the member's own profile editor** (Pessoal, Tuna, Instrumentos, Foto, Notificações por
email, Segurança over `/api/me/*`); `/member/profile` 302s to it. **`/members/map`** (was `/member/map`, which 302s) and
**`/hall-of-fame`** are React (`/api/members/map`, `/api/hall-of-fame`); Leaflet is an npm dependency loaded only by the
map page. Changed on purpose: the session survives a password change; nickname / locked-date / email-in-use rules
refuse with a message; padrinho = Tuno or above. **Push: no opt-in / opt-out UI until the Push v2 task** (a later
task; push sending, subscriptions and endpoints untouched). No schema change. Detail: `docs/react-member-area.md`.

**031 merged to `dev`** (@ `cf70eee3`, CI green after two fixes: `QuestionRepository.AddAsync` re-inserted the users,
and a route test still read the deleted `Requests.razor`). `/requests` and `/questions` are React:
`docs/react-requests-questions.md`.

**030 merged to `dev`** (@ `fad2afa2`). **Members' navigation is now the React member shell** (`MemberShell.tsx`):
a collapsible rail on wide screens, a right-hand drawer on phones / the installed app. Visitors' header: public
sections, "Pedir atuação", "Login" (never "Membros" / "A minha conta"). Members' header: no "Pedir atuação", their
identity + the menu. `/profile` stopped being the member hub (and is the profile editor since 032). `/api/account/me` gains `menu`
(four booleans, `MemberMenuAccess`, the Blazor navbar's rules; hides links only). Removed modules stay out of the menu.
No schema change. Detail: `docs/react-portal-pilot.md` → *Member shell*.

**029A merged to `dev`** (@ `8aa97451`, CI green). **Images and Labels admin removed (UI only):** `/images` (slideshows) and `/labels` (site texts) are plain 404s (no replacement; `/images` needs an explicit
404 endpoint in `Program.cs`, else `ImagesController`'s catch-all answers 400; `/images/<file>` still served); the
Operações "Imagens" and "Conteúdo" items, `LabelCard`, `SlideshowCard` and their CSS are gone. **Leaderboard story is now
temporary code-backed text** (`LeaderboardService.Story`, the seeded label's text; owner decides the final text later):
no `ILabelService` dependency, `PUT /api/leaderboard/story`, `canEditStory` and the React "Editar" dialog removed, so
nobody edits it. **No migration, schema unchanged.** **029B remains** (needs `dotnet`): drop the `Labels` and `Slideshows`
tables with a migration, and delete their entities/configs/DbSets/seed, `ILabelService`/`LabelService` (last caller: the
dead `Components/Portal/*`), `ISlideshowService`/`SlideshowService`, their repositories and tests.

**027 merged to `dev`** (@ `77487805`). **Messages /
Conversas removed from the app (Phase A):** `/messages`, `/messages/{id}` and `/hubs/messages` are plain 404s (no
replacement); the inbox, `MessagesHub`, the user-menu "Mensagens" item and unread badges, "Sync Chat" on `/users`, the PWA
"Mensagens" shortcut, the service worker's `/messages` fallback and app-icon badge, message JS/CSS and every messaging
service are gone. **Push still sends but no longer writes an in-app copy** (the "Sistema RTUB" inbox, ~99% of the
`Messages` rows): members without push now see those notifications nowhere in the app. **No migration, schema unchanged**
(`has-pending-model-changes`: none): `Conversations`, `Messages`, `MessageReactions`, `ConversationUserSettings` keep their
data, unread and unwritten, until the contract task. No R2 objects involved. Backup ZIP (outside the repo, code only, no
message data): `F:\Workspace\Backups\RTUB\027-messages\RTUB-027-messages-e5a30a9a.zip`. Detail: `docs/messages-removal.md`.

**026 merged to `dev`** (@ `e5a30a9a`): Games / Bets / MyTuno removed from the app the same way (Option A); their 15
tables and `AspNetUsers.FidelisBalance` stay. Backup: `F:\Workspace\Backups\RTUB\026-games-bets-mytuno\RTUB-026-games-bets-mytuno-03f18c1d.zip`. Detail: `docs/games-bets-mytuno-removal.md`.
Roadmap and the task contract: `docs/RTUB_REACT_MIGRATION_GUIDE.md`.

**React track 001-025: merged to `dev`** (last: 025 @ `03f18c1d`). React owns
`/`, `/privacy`, `/profile`, `/request`, `/login`, `/music`, `/roles`, `/gallery`, `/events…`, `/rehearsals…`, `/members`,
`/members/hierarchy`, `/leaderboard`, `/inventory`, `/shop`, `/documentation`, `/logistics…`, `/treasury…` and `/news`;
the old Blazor URLs 302 to them (`Program.cs`). No schema change in 006-024; 025 added `NewsPosts` (`AddNewsPosts`).
Per-module rules, APIs and data audits: `docs/react-*.md` (table in `CLAUDE.md`).

**Also merged:** `fix/006-ci-red-after-music`, `fix/030/ios-pwa-media-association` (iPhone DEV check still open),
unit 030 (production release pipeline, PR #203). `master` is at `2.0.4`; `dev`'s `VERSION` is `2.0.5`.

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
Written 2026-09-22, before the first release; `master` has since shipped up to `2.0.4`, so the release steps
(14-16) have run at least once. Check the rest against Azure/GitHub before relying on this list. Items 1-11 do
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
Raised by 033 (Naipes):
- "Remover imagem" on `/naipes/config` leaves the R2 object behind (as before).
- No caller left, still registered: `INaipeAuthorizationService` / `NaipeAuthorizationService`, `INaipeConfigService` /
  `NaipeConfigService`. `.modal-info-section--danger` (`dynamic-style-classes.css`) had no user even before.
- Naipes media are public-read R2 objects (as the Gallery's): members-only means "not listed", not "not reachable".

Raised by 032 (profile, members map, Hall of Fame):
- The map lists expelled accounts (and the Hall of Fame counts every account), as before: decide whether to leave them out.
- Push opt-in / opt-out has no UI until the Push v2 task (not 033); `push-notifications.js`, `PushNotificationPrompt` and
  `/api/push/*` stay for it.
- CSP `script-src` / `style-src` still allow cdnjs and unpkg, now unused (Cropper.js and Leaflet left `MainLayout`).
- No caller left: `wwwroot/js/profilePictureRefresh.js` + `ProfilePictureUpdateService` (still wired in `MainLayout`),
  `wwwroot/lib/cropperjs`; profile / rank / timeline selectors in the Blazor CSS.
- `CachedGeocodingService` creates a DbContext per lookup without disposing it.
- `UserProfileService.UpdateProfilePictureAsync` deletes the old image before the upload succeeds (kept as is).

Raised by 031 (Pedidos and Perguntas):
- Members (not only Admin) read requesters' email and phone, as before; decide whether to narrow it.
- Unused now, left in place: `IQuestionService.GetAllOrgaoSocialMembersAsync` / `HasOrgaoSocialPosition` /
  `GetMembersWithPositionAsync` / `CanAnswerAsync`, `IRequestService.GetPendingRequestsAsync`; most of
  `css/3-components/request-card.css`, `question-card.css` and `css/4-pages/questions.css` (a few selectors are still
  used by Meetings / Naipes).

Raised by 030 (member shell):
- `/naipes/config` is not in the member menu (reached from `/naipes` → "Configurar" since 033); decide. (`/members/map` is, since 032.)
- The Blazor pages keep their own navbar (`MainLayout`), so a member moving between React and Blazor pages sees two
  different menus until those pages are React.
- No JS test runner in the repo: the shell's browser behaviour (drawer, rail, overflow) was checked by hand with a
  scratch Playwright script; `MemberShellTests` pins the source contract only.

Raised by 029A (Images / Labels admin removal, UI only):
- 029B: drop `Labels`/`Slideshows` (migration) and the services, repositories, seed and tests left for it (see Phase).
  Any production edits to the `ranking_story` label are no longer shown; copy them out first if wanted.
- `ImageUploadManager` (Shared, + bUnit tests) has no caller since `Slideshows.razor` went.
- Slideshow images in R2 (upload category `slideshows`) are left in place, unused.

Raised by 027 (Messages / Conversas removal, Phase A):
- Contract task (bundled with 026's, below): drop `Conversations`, `Messages`, `MessageReactions`,
  `ConversationUserSettings` (migration `DropMessagesConversations`) and delete the kept entities/configs/DbSets. Private
  member chats: take the production DB backup first and keep it privately, no longer than needed.
  `docs/messages-removal.md`.
- Now unused, left in place: `window.appBadge` (`pwa-helper.js`); the `/hubs/` never-cache prefix in `service-worker.js`
  (harmless, pinned by `ServiceWorkerReliabilityTests`).
- The Play Store (TWA) app may carry the old "Mensagens" shortcut until it is rebuilt from the manifest; it opens a 404.
- DEV after a refresh holds production's messages unsanitized (`DatabaseSanitizer` skips them) until the contract task.

Raised by 026 (Games / Bets / MyTuno removal):
- Contract task (one release with 027's, above), only after the release carrying 026 is live in production: drop
  the 15 game tables and `AspNetUsers.FidelisBalance` (migration `DropGamesBetsMyTuno`), delete the kept
  entities/configs/DbSets; take a production DB backup first; that release may be MAJOR. Exact list:
  `docs/games-bets-mytuno-removal.md`.
- R2 objects left in place, public-read and unused: `images/{env}/bets/`, `images/{env}/bet-comments/`,
  `item-configs/{env}/images/`.
- Now unused (their only users were game pages), left for the dead-code cleanup: `window.isMobileDevice`
  (`pwa-helper.js`); 77 classes in `css/9-overrides/inline-style-utilities.css`, `u-cursor-default`,
  `u-cursor-not-allowed`, `u-fill-pct--smooth` (`dynamic-style-classes.css`) and `.hp-bar-fill` (`rank-card.css`).
- Any `Games__*` / `MyTunoScaling__*` app settings on `rtub` / `rtub-dev` are now dead (not checked; none in the repo).
- `deploy-prod.yml` comment "npm and NuGet code runs here": npm no longer runs in the build (workflow left untouched).

Raised by React track 025 (Novidades):
- No images, comments, reactions, pinned posts or per-post page with share previews yet (`docs/react-news.md`).
- `AddNewsPosts` is the React track's first schema change; the next PROD release runs it (additive, N-1 safe).

Accepted after the 018-024 audit (2026-10-05):
- Treasury: deleting a draft report that a `NerbaOrders.ReportId` row still references (ON DELETE RESTRICT) rolls back
  correctly but answers 500; it should answer 409 with a message.
- Members: every signed-in member, Leitões included, sees other members' email and phone in "Detalhes" (as the old
  page did; `docs/react-members.md`). Decide whether Leitões keep that.
- Dead code with no caller (one cleanup task, not piecemeal): Shared `ReportCard`, `TransactionCard`, `MbwayTransferCard`,
  `NerbaOrderCard`, `ReservationCard`, `FolderCard`, `DocumentCard`, `BoardCard`, `LeaderboardCard`, `LeaderboardCommentItem`,
  `InstrumentCircle`, `AvatarCard`, `EventCard`; services `ITransactionFilterService`, `IDebtService`,
  `ILogistics{Board,List,Card}Service`, `IMemberPositionService`, `IUserRoleQueryService`, `IEnrollmentFilterService`;
  CSS `kanban.css`, `folder-card.css`, `document-card.css`, `avatar-card.css`, `instrument-circle.css`,
  `music.css`, `song-card.css`, `audio-player.css`; JS `familyTree.js`, `home.js`, `scrollSpy.js`.
- Real R2 still unchecked (local runs have placeholder storage): Music audio, Gallery upload, Inventory/Shop images,
  Documentation upload/download, Logistics board files, Treasury receipts and PDF. Check on DEV.

Raised by React track 024 (Tesouraria):
- Receipts are public-read R2 objects (anyone with the URL opens them); private objects + pre-signed links need a storage change.
- `ReportPdfService` caches by `Report.UpdatedAt` (transaction edits do not touch it): PDF up to 1 h stale.
- `ReportCard`, `TransactionCard`, `MbwayTransferCard`, `NerbaOrderCard` (Shared), `ITransactionFilterService`, `IDebtService` have no page caller.
- 14 local `Transactions` rows have no activity and appear nowhere.
- Local run: ports 58869/58870 now fall in a Windows excluded port range; use another port.

Raised by React track 023 (Logística):
- `LogisticsCardReminders` are stored but no job sends them (pre-existing); build delivery or drop the feature.
- `ILogistics{Board,List,Card}Service` (+ tests), `BoardCard` (Shared) and `css/3-components/kanban.css` have no page caller.

Raised by React track 022 (Documentação):
- `FolderCard`/`DocumentCard` (Shared, + bUnit tests) and `css/3-components/folder-card.css`/`document-card.css` have no page caller.
- React `/roles` probes `GET /api/governance/manage`, logging a 403 console error for members without management rights.

Raised by React track 021 (Loja):
- `ReservationCard` (Shared) has no caller; "Público (não membros)" never reached visitors (shop is members-only).

Raised by React track 020 (Instrumentos):
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
- (030, resolved) Sign-out: in the member shell (rail / drawer) and on `/profile`.

Raised by 006 (shell banners):
- (post-024, resolved) `VERSION` CRLF on Windows checkouts: `.gitattributes` pins `VERSION text eol=lf`.
- Portal `index.html` loads `/js/sw-register.js` unversioned (stale-while-revalidate), so one visit after a
  deploy can still run the old script.

Raised by React track 006 (Music):
- `AlbumAccesses` holds 18 stale rows for album 12 (not exclusive); ignored by the rules, not deleted.
- Retired-Music leftovers still loaded globally: `css/3-components/music.css`, `song-card.css`,
  `audio-player.css`, and the PWA-queue half of `pwaMediaSession.js`/`MediaSessionInterop`.
- Play cooldown is per app instance (in-memory); a scaled-out App Service would need a shared cache.

Raised by React track 005 (content audit):
- (024, resolved) `/calotes`, `/mbway`, `/nerba` were open to visitors; they now 302 to the members-only React
  `/treasury…` pages and the API refuses visitors (401) and non-treasury members (403).
- (029A, resolved) The old home's text (Labels, `/labels`) stays static in React; the `/labels` admin is gone.
- (029A, resolved) The public slideshow is dropped; the `/images` admin is gone.

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
  tests that sweep `src/` on disk. CI is unaffected.
- (027, resolved) `MessagesHubTests` order-dependent failure: the class went with Messages.
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
- (026, resolved) Payload: `wwwroot/sprites` (~180 MB of the ~230 MB zip) and `wwwroot/sound` went with MyTuno.
- `.deployment` (`SCM_DO_BUILD_DURING_DEPLOYMENT=true`) is inert.
- Owner Storage Maintenance page (029 contract) not built.
- After a DEV refresh, keep `DevelopmentDataReset__Enabled` off on `rtub-dev`.
- iPhone installed-PWA check on `/music` still needs a real device (DEV URL is fine).
- GitGuardian: historical incidents stay historical; mark them by hand.
- Old work branches `chore/001`…`chore/029`, `fix/012`…`fix/029`, `perf/019`: delete when convenient.

## Next
Owner actions above. After step 18: STATE.md → "030 complete".

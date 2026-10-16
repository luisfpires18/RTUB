# STATE.md

Living execution state. **Read this first.** A status board, not a diary: history is in git, and
durable detail lives in the docs linked below.

_Last updated: 2026-10-16 (#220 / #223)_

## Phase
**#220 tracing names + cards and #223 hierarchy tree (uncommitted on `dev` @ 2.0.10, not pushed, issues stay open):**
#220: audit rows named after the person / object for every audited type (one batched lookup per table before the save,
none when not needed; deletes named before they go), push keys redacted in new entries, old entries never rewritten
(`currentDisplayName` at read time only; export raw), search over stored changes or name with the filters and an exact
total, Portuguese type labels, new card and Details. #223: CSS-only WAI-ARIA tree (left to right ≥ 900 px, indented
below), search, path bar, "Focar ramo", keyboard; loop / self-padrinho guard in `MemberHierarchyService`. No schema
change, no new dependency. Detail: `docs/react-owner-operations.md` → *Names and cards (#220)*, `docs/react-members.md`
→ *Hierarchy tree (#223)*.

**#221 shop products in every fiscal year (uncommitted on `dev` @ 2.0.10, not pushed, issue stays open):**
`Products.AvailableAllYears` + migration `AddProductAvailableAllYears` (default 0: existing and N-1 products stay
year-specific, by `CreatedAt`); one rule `ProductShopService.InFiscalYear`; "Disponível em todos os anos letivos" in the
product form (`availableAllYears` absent = unchanged on edit). Reservations untouched. Detail: `docs/react-shop.md` →
*Products in every fiscal year*. **#220 / #223: audit only, no code** (proposals in the batch report).

**UI/PWA batch #229 + #224 + #225 (uncommitted on `dev` @ 2.0.10, not pushed, issues stay open):** no page scrolls
sideways at 320-414 px, fixed per element, and the old global mask `body { overflow-x: clip }` removed. Member header
at < 360 px (logo only, category truncates); `.sr-only` hardened (hidden file inputs widened "Adicionar atuação");
Tesouraria tabs reflow on phones; "+N XP" badges no longer spill out of the details dialogs' activity rows; header pads by `--safe-top` (iOS installed app). #224: `/roles` on the shared
`events-page__head`, year controls in one panel, "Adicionar ano letivo" only while the server lists a missing year
(Galeria / Música heads fixed the same way). #225: levels open by default, balanced 1 / 2 / 3 / 4 columns, the reader's
level from the new `me` of `GET /api/leaderboard`. Audit: `tests/responsive/responsive_audit.py`. Detail:
`docs/frontend-practices.md`, `docs/react-governance.md`, `docs/react-leaderboard.md`.

**#222 News public / members-only + #228 Music teaser removed (committed on `dev` @ `66bff340`):** `NewsPosts.IsPublic` + migration
`AddNewsPostIsPublic` (default 1), `NewsService.VisibleTo` on the only news read, `MembersOnly` on create / update (absent
keeps), "Privado (só membros)" toggle, "Só membros" tag, Todas / Públicas / Privadas tabs for signed-in readers; public
Music page no longer shows "Há também álbuns reservados a membros". Detail: `docs/react-news.md` → *Visibility*.

**P1 hotfix #230 push enabled but not received (released in PROD 2.0.9, merged back into `dev`; #230 stays open):** every send
returns a classified `PushSendResult` (only 404/410 delete; 4xx/5xx kept and logged with the push service's reason,
never keys / endpoint), per-fan-out summary warnings, `/profile` "Enviar notificação de teste" → `POST /api/push/test`
(own endpoint only, 30 s cooldown, says "enviada ao serviço push", never "recebida"), "on" now also needs granted
permission. PROD VAPID verified (configured, valid, private ↔ public match); real FCM delivery verified locally. Needs
the affected iPhone test after release. Detail: `docs/react-push-v2.md` → *Test push and delivery outcomes*.

**P0 hotfix #227 events public / members-only (uncommitted on `master`, not pushed):** `Events.IsPublic` + migration
`AddEventIsPublic` (default 1: existing rows and N-1 inserts stay public), server-side `EventAgendaService.VisibleTo`
on the agenda, archive, event page and video plays; home preview public-only; "Privado (só membros)" toggle (off by
default) and "Só membros" tag in React. Push unchanged (subscribers are signed-in members only). Detail:
`docs/react-events.md` → *Visibility and permissions*. Needs a hotfix branch, VERSION bump and the master → dev
back-merge before release.

**046 new RTUB logo (uncommitted on `dev`, not pushed):** every brand asset regenerated from the square `wwwroot/images/rtub_new_logo.png`: canonical `/images/rtub_logo.webp` (1024px), `/icons/rtub-logo-352.webp`, manifest 192 / 512 + new maskable 512, push badge, `apple-touch-icon.png`, `favicon.ico` (16/32/48). Unused sizes removed (16, 32, 120, 152, 167, 180, 256, 384, `apple-touch-icon-180x180.png`). Service worker `CACHE_VERSION` v2.9.0 (images are cache-first). No API, schema or migration change. Detail: `docs/pwa-practices.md` → *RTUB brand assets*.

**045 performance gate (uncommitted on `dev`, not pushed):** splash / header logo 465 KB PNG → 45 KB WebP (`icons/rtub-logo-352.webp`), CLS fixes (`.boot` padding, `main` min-height, `/members` actions while loading), logistics board no longer re-lists R2 on every card / list change. Mobile Lighthouse 61-79 → 94-98, mobile LCP 4.35 → 2.25-3.0 s, CLS ≤ 0.03. Frontend only; no API, schema or migration change. Detail: `docs/performance-gate.md`.

**044B (uncommitted on `dev`, not pushed): member activity status refactor.** One pure evaluator
(`MemberActivityStatusEvaluator`), `MemberStatusService` reconciles every member (active and Reformado), per-member
failure isolation, audit + push only on real transitions, `TimeProvider`. Leitões are in the rule (six months
from the Leitão month when there is no activity); reads never write, only reconcile paths do. `RetirementStatusService` and "Tornar ativo" /
`OverrideRetired` usage removed (column kept, dead — drop in a later migration). No migration. Detail:
`docs/member-activity-status.md`. **First DEV/PROD run will reform active members with no activity in 6 months**
(incl. old `OverrideRetired` ones) — expected per the rules.

**044 batch 3 (uncommitted on `dev`, not pushed): profile polish.** `/profile`: photo moved into the header, "Entraste como" removed, "Informação da Tuna" with a connected-card Percurso timeline (real `member.timeline`), one shared `MemberBadge` / `.member-badge` for the whole app, aligned action buttons. React only; no API, schema or migration change. Batch 4 (same files, uncommitted): header order inbox / category badge / profile / menu, original per-kind badge colours restored, legacy-coloured timeline, flatter Tuna section. Detail: `docs/react-member-area.md` → *Profile polish, batch 3* and *Batch 4*.

**044 UI polish (uncommitted on `feat/044-ui-polish`):** signed-in container 1320px (`.shell--member --wrap`), event card "Área da atuação" with Discussão / Contactos only, `/events/my-enrollments` removed (modal "Onde estiveste" on `/events`), menu: no "Recursos" (Documentação + Instrumentos da Tuna under Gestão, Loja under Membro), Classificação / Hall of Fame no longer cross-linked, Naipes picker centred, `/messages` as an app screen (list → system → groups → people).

**044 batch 2 (uncommitted on `dev`, not pushed): authenticated shell.** Left sidebar removed; header = `[inbox + unread
badge] [profile chip → /profile] [menu]`, menu = right-hand drawer at every width without Perfil / Mensagens. Badge =
unread messages from new `GET /api/conversations/unread`, polled every 10 s (no hub exists; no schema change, no migration).
Detail: `docs/react-messages.md` → *Header unread badge*, `docs/react-portal-pilot.md` → *Member shell*.

**Open: `chore/043-db-cleanup` (task 043)**, from `dev` @ `593b1d63` (has 042). Not committed / pushed; **do not merge
into `dev` until a release without it (026-042) is live in PROD past its rollback window** (N-1: PROD 2.0.4 still reads
these tables and `FidelisBalance`). **043:** migrations `RemoveRetiredGamesLabelsSlideshowsSchema` (drops the 15 game
tables, `Labels`, `Slideshows`) and `RemoveFidelisBalanceFromApplicationUser` (EF rebuild of `AspNetUsers` without the
column), with their entities / enums / configs / DbSets / seed / display cases / audit entries removed. **PROD
rollback across 043 = full restore of the pre-migration backup** (EF Down is local / DEV schema testing only). Upgrade / rollback / reapply validated on a scratch copy (messaging and Identity
counts unchanged). `PostMedia` / `CommentImages` deferred (production row counts unknown). Not applied to DEV or PROD.
Release plan: `docs/db-cleanup-inventory.md` → *Task 043*.

**042 merged to `dev`** (@ `593b1d63`). **042 cleanup audit:** code with no caller removed, **no schema change, no migration, no data touched**: Labels /
Slideshows services + repositories (tables stay), `ILogistics{Board,List,Card}Service` + their repositories (the board
folder sanitizer moved unchanged into `LogisticsKanbanService`), Blazor-era `PaginationHelper`, `SortableTableHelper`,
`DateTimeExtensions`, `DatabaseFacadeExtensions`, `NaipeContentFormModel`, `ReservationModel`,
`ApplicationUser.SelectedCategory` (`[NotMapped]`), and their tests; CSP `script-src` / `style-src` now `'self'` only
(cdnjs / unpkg unused since 032); the `Microsoft.AspNetCore.Identity.UI` package (only its `IEmailSender` was used, now
`RTUB.Application.Interfaces.IEmailSender`), which shipped 187 unused Bootstrap / jQuery files under `/Identity/*`, so the
package guard floor is 200 wwwroot files (was 400; a healthy publish has 260); unused package refs (`AutoFixture`, test
`AWSSDK.Core` / `MockQueryable.Moq` / `Moq`), npm `cropperjs` / `sharp`; `wwwroot/images/hierarchy/*`,
`wwwroot/uploads/events/.gitkeep`. Database leftovers for **task 043**: `docs/db-cleanup-inventory.md`. Messaging tables
untouched.

**041 merged to `dev`** (@ `55dbf279`). **041 Messaging v2:** React `/messages` (list + chat, `?c=<id>`, "Mensagens" in the member menu) over
`/api/conversations*` + `/api/messages/{id}/reactions` (`MessagingService`) on the **preserved v1 tables, no schema change,
no migration**: participants only (Admin / Owner no special access), text only (1-2000, no attachments), replies (v1
`ReplyToMessageId` + snippet), one reaction per member from 👍 ❤️ 😂 😮 😢 🙏, mute (`ConversationUserSettings`, no push
while muted), unread / read (`ReadBy`), "Sistema RTUB" read-only, v1 announcement-sender rule, archived hidden, chats with
a deleted / expelled member closed. Push "Nova mensagem" without the text. Tracing: `MessageReaction` added to the audit
exclusions + targeted `RedactedProperties` in `AuditLogAppender`. `/messages/{id}` and `/hubs/messages` stay 404.
Polling (10 s chat / 30 s list), no SignalR. Detail and follow-ups: `docs/react-messages.md`.

**040 merged to `dev`** (@ `467d30d3`). **040:** members map basemap CARTO `dark_all` (now "API KEY REQUIRED") → OpenStreetMap standard
tiles (no key; CSS-darkened tile pane); CSP `img-src` `*.basemaps.cartocdn.com` → `tile.openstreetmap.org`. Config
audit: DEV `WEBSITE_RUN_FROM_PACKAGE` optional (intentionally absent on rtub-dev), PROD still required `1`; runbook
notes `.local/config-audit/` is generated / never committed. No schema or data change. Detail: `docs/react-member-area.md`.

**039 merged to `dev`** (@ `0200f1b1`).

**039 Post-Blazor cleanup + configuration audit:** docs and agent instructions rewritten for the React-only stack
(`README.md`, `.github/copilot-instructions.md`, `.github/agents/*`, `docs/frontend-practices.md`; Blazor / `RTUB.Shared`
/ IDrive leftovers fixed elsewhere). **Messaging tables are preserved for Messaging v2: the planned
`DropMessagesConversations` contract task is cancelled** (`docs/messages-removal.md`); the Games / Bets / MyTuno
contract task stays, without them. Dead code removed (build-proven, no caller): the `Microsoft.AspNetCore.SignalR.Client`
package; the `IBrowserFile` upload parameters and helpers of `IPostService` / `ICommentService` (`RTUB.Application` has
no Blazor type left); `INaipeAuthorizationService` (`AuthenticationState`), `INaipeConfigService`,
`ITransactionFilterService`, `IDebtService`, `IMemberPositionService`, `IEnrollmentFilterService`,
`IUserRoleQueryService` (+ tests, two DTOs, DI lines); dead `IDrive:*` test settings. **Configuration audit:**
`scripts/config/required-settings.json` (every setting per environment), `audit-local-user-secrets.ps1`,
`audit-azure-app-settings.ps1` (read-only, redacted: length + SHA-256 prefix only; DEV/PROD match / differ checks),
`generate-vapid-keys.ps1`; outputs in the git-ignored `.local/config-audit/`, templates placeholder-only. Runbook:
`docs/configuration-runbook.md`; tests `ConfigurationAuditTests`. No behaviour change, no schema change, no migration.

**038 merged to `dev`** (@ `1885d507`). **038 Push v2:** `/profile` owns push: section "Notificações push" (after the email one), per device, capability
detection only, no prompt on load, permission and subscription only from a click; states unsupported (with the
iPhone / iPad Home Screen hint outside an installed app), disabled, not configured, worker not ready, blocked, off, on,
unknown to the server ("Corrigir subscrição"), stale key / expired ("Ativar novamente"), error; "Desativar em todos os
dispositivos". `PushController` is replaced by `PushEndpoints` (`/api/push/{status,check,subscribe,unsubscribe}`):
signed-in only, no-store, `X-CSRF-TOKEN` on every POST, **own subscriptions only** (the old unsubscribe deleted any
endpoint), endpoint / key validation (`PushSubscriptionRules`: https on a browser push service, no IP; P-256 key, 16-byte
auth), `replaces` for a same-device endpoint swap. The dead `/api/push/broadcast` and `/api/push/send-to-selected`
(still open to Admin) are gone; custom push stays on the Owner's `/notifications`. Service worker: push handler without
the iOS user-agent check and the vibrate pattern, same-origin `notificationTarget()`, no app-badge clearing, rotation
re-sends with `replaces` + token instead of calling unsubscribe (which opted the member out); `sw-register.js` stays
the only registration. Removed: `push-notifications.js`, `pwa-helper.js`, the `rtub-push-*` localStorage flags.
Backend storage kept: `PushSubscriptions`, `PushNotificationsOptedOut` (now: set when the last device is turned off),
every sender. No schema change. **Real devices:** Edge desktop checked end to end locally (subscribe, an Owner push
delivered and shown, unsubscribe, reload); **Android installed app, iPhone / iPad Home Screen app and DEV not checked:
required before a production release.** Detail: `docs/react-push-v2.md`.

**037 merged to `dev`** (@ `5e147945`). **037 Blazor host retired:** 0 Blazor pages (036) and now no host: `App.razor`, `_Imports.razor`, `MainLayout`,
`Components/*` (incl. `AnnouncementBanner`, `ReconnectModal`, `VersionedAsset`, the dead `Portal/*`),
`MapRazorComponents` / `AddRazorComponents` / `AddInteractiveServerComponents`, the circuit log filter, the SignalR
`HubOptions`, `AuthenticationStateProvider`, the `/_blazor/initializers` shim, `Interop/*`, `AdminRefreshService`,
`ProfilePictureUpdateService`, every Blazor stylesheet (`wwwroot/css` except `error.css` / `offline.css`), every Blazor
script (`wwwroot/js` except `sw-register.js`, `offline.js` and, for Push v2, `push-notifications.js` / `pwa-helper.js`,
removed by 038)
and `wwwroot/lib` (Bootstrap, Bootstrap Icons, Cropper.js) are gone. **`/_blazor` and `blazor.web.js` are 404.**
**`RTUB.Shared` and `RTUB.Shared.Tests` are removed** (only `RTUB.Web` referenced it; out of the solution; `bunit` and
the two `Microsoft.AspNetCore.Components.*` package versions dropped). CSP `connect-src` is `'self'` only (the per-request
ws:/wss: source was for the circuit). Service worker `v2.8.0`: no Blazor precache entry, no `/_blazor` exclusion;
offline, install and push unchanged; manifest still has no `share_target`. **System announcement restored as a React
listener** (option B): `AnnouncementService` keeps the latest one in memory for 5 minutes, `GET /api/announcement`
(anonymous, no-store), the shell polls every 30 s while visible and shows a dialog once per browser; the Owner page and
`POST /api/owner/tracing/announcement` unchanged. Intentional changes: up to 30 s late instead of instant, a page opened
within 5 minutes still sees it, no sound, no Bootstrap / Blazor CSS anywhere. No schema change, no migration. Detail:
`docs/react-portal-pilot.md` → *System announcement*; tests `BlazorHostRetirementTests`, `AnnouncementServiceTests`.

**036 merged to `dev`** (@ `3f7cc98d`). **036 Identity + /share:** `/forgot-password`, `/reset-password` and `/confirm-email` are React (public), over
`POST /api/account/{forgot-password,reset-password,confirm-email}` (`AccountRecoveryService`: same Identity calls,
token handling and messages; antiforgery; no-store). The pages are `no-store` and `no-referrer`, and the token is cleared
from the URL, never stored, never logged and never echoed. `/Error` is a **server-owned static page on purpose**
(`wwwroot/error.html`, any method, no script, no details). `/share` and the manifest `share_target` are **removed**
(404). Intentional changes: a failed reset email answers the same neutral sentence (it used to surface the error page),
and an invalid or expired reset token is now said in Portuguese. **0 Blazor pages left** (5 before); the host itself was
retired by 037. No schema change. Detail: `docs/react-identity.md`.

**035 merged to `dev`** (@ `05f1f55e`). **035 Owner tools:** `/emails`, `/notifications`, `/users`, `/owner/tracing` and `/owner/db` are React, over the
Owner-only `/api/owner/*` (`OwnerEndpoints` gate + `OwnerOperationsService` over the old email / push / audit / DB
services). **All five are Owner only: Admin lost `/emails` and `/notifications` (product decision).** Visitors 302 / 401,
every other member 403 (refusal page). New safeguards: the server checks `DELETE ALL`, the SELECT / DML rules, and a
confirmation flag for DB changes; the React page asks to confirm sends, role changes and DB changes. Retired: the five
Blazor pages, `EmailRecipientsPreview`, `UserCard`, `TracingCard`, `LoginStatisticsButton`, `SyncMemberStatusButton`,
`user-card.css`, `tracing-card.css` and their tests. **5 Blazor routes left** (`/share`, the three Identity pages,
`/Error`; 10 before). No schema change. Detail: `docs/react-owner-operations.md`.

**034 merged to `dev`** (PR #218, @ `ad1a3ba6`; written as an unverified cloud patch). **034 Reuniões:** `/meetings` is React (`/api/meetings*`, `MeetingBoardService` + `MeetingAccess` over the old meeting,
request, participation and ata services: push, email and the ata PDF into Documentação unchanged); signed-in members, a
Leitão gets "Acesso restrito". Who sees and does what is the old page's effective matrix, now enforced by the API (the
old services checked nothing). **The only permission change is A1:** a draft ata is only for whoever may write it (any
reader of the meeting could open it before). Also changed on purpose: validation and lengths enforced server-side (the
old forms never ran theirs), editing a cancelled meeting keeps it cancelled, a non-CV meeting keeps no Tuno
representative, duplicate email addresses are sent once, the email preview is sandboxed, search runs in memory.
Retired: `Meetings.razor`, `MeetingCard`, `MeetingRequestCard`, `MeetingParticipationModal`, `meeting-card.css` and their
bUnit tests; the Blazor "Gestão → Reuniões" link is a full navigation. **10 Blazor routes left** (11 before). No schema
change. Detail: `docs/react-meetings.md`.

**033 merged to `dev`** (PR #217, @ `38eff81`): `/naipes` and `/naipes/config` are React: `docs/react-naipes.md`.
**032 merged to `dev`** (PR #216): `/profile` is the member's own profile editor; `/members/map` and `/hall-of-fame` are
React; push opt-in / opt-out is back in `/profile` since 038: `docs/react-member-area.md`.

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
nobody edits it. **No migration, schema unchanged.** (042) The services, repositories and their tests are gone; the
`Labels` / `Slideshows` tables, entities, DbSets and seed go in task 043 (`docs/db-cleanup-inventory.md`).

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
Raised by #220 / #223: push-subscription log entries written before #220 still hold `Endpoint` / `Auth` / `P256dh` in
their changes (Owner-only Details and export; redacting them would rewrite history - a decision); names resolved at read
time are not searchable; `NaipeContent` writes both an automatic and a manual entry per change; the tracing delete
button is 36 px on phones (existing `icon-btn--sm`); the hierarchy path bar takes two rows at 320 px.
Raised by #221: a shop reservation snapshots no product name or price and is deleted with its product (cascade);
`ProductShopService.InFiscalYear` ends a year at 31 Aug 23:59:59 (sub-second gap) and compares UTC `CreatedAt` with
local-calendar bounds.
Raised by #229: iOS Safari native date / time controls and the `black-translucent` status-bar inset not checked on a
real iPhone (Playwright WebKit not installed; CSS is defensive); inline text links in lists (`.archive-row__name`,
`.door__mail`) are 24-26 px tall (WCAG inline exception); 12 leaderboard level rows on phones are long but readable.
Raised by #230: PROD App Service has no persisted application logs (no `*_default_docker.log`), so push warnings are visible only in the live log stream; `WebPush` 1.0.13 sends legacy `aesgcm` / `Authorization: WebPush` (move to RFC 8291/8292 only if an iPhone test push comes back `rejected`); 5xx from a push service is not retried; DEV (`rtub-dev`) has no VAPID settings, so push is "not-configured" there. STATE's #227 "uncommitted" line is stale (released as 2.0.8).
Raised by hotfix 2026-10-14 (agenda Portugal date): the same server-zone `DateTime.Today` remains in `EventAdminService.IsPast` (prizes / videos / cancel / notices; also `TakesPrizes` on the event page), `EventRepository`, `EnrollmentService`, `RehearsalRepository`, `FiscalYearHelper`, `PushNotificationFactory` — wrong only 00:00-01:00 Portugal in summer; move to `PortugalTime.Today`.
Raised by 045 (performance gate; detail in `docs/performance-gate.md` → *Deferred*): avatar thumbnails (600px WebP shown at 32-72px), favicon / default-avatar / icon PNG weight, contrast of three small labels (Home `.record img` aspect ratio fixed in 046).
Raised by 042 (cleanup audit; DB leftovers in `docs/db-cleanup-inventory.md`):
- (043, written, not applied) drop the 15 game tables + `AspNetUsers.FidelisBalance`, `Labels`, `Slideshows`. Still open:
  `PostMedia` / `CommentImages` row counts on production (write-dead since 013, read only by delete clean-up).
- Kept on purpose: the old-URL 302s in `Program.cs` (bookmarks, emails, installed apps; comments still say "while DEV is
  hybrid"); the service worker's `/hubs/` never-cache prefix (a worker change forces every client to update); npm
  `bootstrap-icons` (path-data source for `icons.tsx`, not bundled); config bound but unread (`LoginPopupOptions`,
  `Toggles`, `UseSqlServer`, `ImageCaching:*`, `Gallery:MaxVideoSizeMB`: flagged `unused` by the 039 audit, Azure
  settings first); method-level leftovers (`ApplicationUserExtensions.GetYearsAsTuno` / `HasBeenTunoForYears` /
  `IsTunossauro` / `IsVeterano`, `QueryableExtensions.Paginate`: tests only).
- `npm audit`: 5 high (vite 6 and its `nanoid` / `picomatch` / `postcss` / `source-map-js`), build-time only, pre-existing.
- `MessagesRollbackSafetyTests` drops the messaging tables only inside its throwaway test database (never a real one) to
  prove nothing else touches them; test-only, kept.

037 deleted every `RTUB.Shared` component, the Blazor CSS and JS and `wwwroot/lib`: older entries below that name one of
them (no-caller cards and badges, `kanban.css` and friends, `familyTree.js`, `home.js`, `scrollSpy.js`,
`profilePictureRefresh.js`, `roleBadge.js`, the Blazor prompts, `wwwroot/lib/cropperjs`, `.member-avatar-small`) are
resolved by that deletion.

Raised by 041 (Messaging v2; detail in `docs/react-messages.md` → *Follow-ups*):
- Group membership frozen at v1's last auto-sync (no group creation / member management, no default-group sync).
- No live updates (polling), no unread badge in the menu, no pin / archive UI; push fan-out inline and sequential.
- `/owner/db` can still SELECT the messaging tables (Owner raw DB tool, as before): decide whether to block them.

Raised by 039 (post-Blazor cleanup + configuration audit):
- Live Azure audit not run in 039 (the `az` MFA session had expired): run `scripts/config/audit-azure-app-settings.ps1`
  after `az login` and act on its warnings (VAPID on DEV/PROD, `IDrive__*` on `rtub`, DEV/PROD shared values).
- Local user-secrets still hold six dead `IDrive:*` keys (the local audit flags them); remove by hand.
- Committed but unread settings: `Gallery:MaxVideoSizeMB` (the code reads `GalleryMedia:MaxVideoSize`, bytes),
  `ImageCaching:*`, `UseSqlServer`, `LoginPopup:*` (`LoginPopupOptions` bound, nothing shows it). Left as is.
- (042) `ILogistics{Board,List,Card}Service`, `ILabelService` / `ISlideshowService` removed. Still no caller, kept
  (method-level): `MimeTypeHelper.GetMediaMimeType`, the unused `IQuestionService` / `IRequestService` methods (031).
- `wwwroot/images/icons/*` has no in-repo reference (old-design logo copies); kept, untouched by 046 (`apple-touch-icon-180x180.png` removed in 046). Was: kept (served by convention or to
  installed apps). (042) `wwwroot/images/hierarchy/*` (only the Blazor hierarchy page used them) removed.
- Branch name `feat/039-post-blazor-config-audit` reuses number 039 (an old `chore/039/ui-management-dense-screens`
  branch exists), as the task asked.

Raised by 038 (Push v2; detail in `docs/react-push-v2.md` → *Follow-ups*):
- **Before a production release:** check push on an Android installed app, an iPhone / iPad Home Screen app and DEV
  (real VAPID keys): turn on, receive one notification, turn off, reload. Only Edge desktop was checked (locally).
- A browser on a push service outside `PushSubscriptionRules.PushServiceHosts` is refused: add its host if one appears.
- `PushNotificationService` still news up `WebPushClient` (no send tests); no `CancellationToken` in the push API.
- Existing rows keep their old validation-free data; a bad one only fails to send (404/410 deletes it as before).

Raised by 037 (Blazor host retirement):
- (039, resolved) Copilot / agent instructions and `docs/frontend-practices.md` describe the React stack; the
  SignalR client package and the Blazor-era `RTUB.Application` signatures are gone. (042) `ILabelService` removed.
- (resolved in 038) `push-notifications.js` and `pwa-helper.js` were removed with the React push client.
- The system announcement lives in one app instance's memory (fine for one App Service instance; a shared store if it
  scales out) and plays no sound.
- `NoStylesheet_SetsOverflowHiddenOnTheViewportRoot` now guards only `error.css` / `offline.css`; the React
  `styles.css` is not in its sweep.
- Many code comments still say "was the Blazor …": history, left as is.

Raised by 036 (Identity + /share; detail in `docs/react-identity.md` → *Deferred*):
- (resolved in 037) The Blazor host is retired and the system announcement is a React listener.
- forgot-password and reset-password have no rate limit (as before). `/confirm-email` has no link generator in the app.

Raised by 035 (Owner tools; detail in `docs/react-owner-operations.md` → *Deferred*):
- (resolved in 037) The system announcement (`/owner/tracing`) is shown by the React shell (`GET /api/announcement`).
- No guard against the Owner demoting themselves / the last Owner (as before). `/owner/db` shows `AspNetUsers` secrets
  columns to the Owner (as before). Announcement emails send synchronously (no live progress).
- No caller left: `LockStatusBadge`, `LoginStatusBadge`, `RoleBadge` (+ tests, `roleBadge.js`), `MemberCardLite`,
  `.subscriber-list--h*` (dynamic-style-classes.css), `.member-avatar-small` (meetings.css).

Raised by 034 (Reuniões; detail in `docs/react-meetings.md` → *Deferred*):
- Kept as before, decide: the fiscal-year filter ends at midnight on 31 August (a meeting later that day shows only under
  "Todos os anos"); Owner does not see CV / Direção meetings or requests unless its own rule allows (A3); the requests
  section is the Veterano / Magister rule for every type, so some approvers and Direção proposers cannot see their
  requests (A4); the Direção branch of `CanCreateMeetingType` stays unreachable (A6).
- `Meeting.DelegatedAtaWriterMemberId` is never set; ata attachments and ata delete have no UI (as before).
- A published Direção ata PDF goes to Documentação `Atas {FY}`, which `DocumentationAuthorization.CanSeeFolder` leaves
  open to every non-Leitão (only `Atas CV` / `Atas AG` are restricted), as before 034: decide whether to restrict it.
- Notification links (meeting custom push, request reminder; also Events' notices and the old meeting services) are
  built from the request's Host header while `AllowedHosts` is `*`: a manager forging Host could point members' push
  links elsewhere. Same as before 034; fix app-wide (real `AllowedHosts`, or one configured base URL).
- Shared `.modal__head` / `.modal__title` (portal `styles.css`) now let a long dialog title wrap instead of squeezing the
  close button (found on `/meetings`; every React dialog benefits).
- `css/4-pages/meetings.css` stays: its `.member-avatar-small` (32px, imported last) still sizes the avatars in the
  `/users` dialogs; its other selectors (and all of `request-card.css`, `question-card.css`, `4-pages/questions.css`,
  `.request-admin-overlay`, the grid classes in `enrollment-card.css`, `.modal-info-section*` / `.modal-section-*` in
  `profile-components.css`) have no markup user left.
- No caller left: Shared `EnrollmentCard` and `StatusBadge` (+ bUnit tests); `DateBadge` only through the dead `EventCard`.

Raised by 033 (Naipes):
- "Remover imagem" on `/naipes/config` leaves the R2 object behind (as before).
- No caller left, still registered: `INaipeAuthorizationService` / `NaipeAuthorizationService`, `INaipeConfigService` /
  `NaipeConfigService`. `.modal-info-section--danger` (`dynamic-style-classes.css`) had no user even before.
- Naipes media are public-read R2 objects (as the Gallery's): members-only means "not listed", not "not reachable".

Raised by 032 (profile, members map, Hall of Fame):
- The map lists expelled accounts (and the Hall of Fame counts every account), as before: decide whether to leave them out.
- (resolved in 038) Push opt-in / opt-out is the `/profile` section "Notificações push".
- (042, resolved) CSP `script-src` / `style-src` are `'self'` only.
- No caller left: `wwwroot/js/profilePictureRefresh.js` + `ProfilePictureUpdateService` (still wired in `MainLayout`),
  `wwwroot/lib/cropperjs`; profile / rank / timeline selectors in the Blazor CSS.
- `CachedGeocodingService` creates a DbContext per lookup without disposing it.
- `UserProfileService.UpdateProfilePictureAsync` deletes the old image before the upload succeeds (kept as is).

Raised by 031 (Pedidos and Perguntas):
- Members (not only Admin) read requesters' email and phone, as before; decide whether to narrow it.
- Unused now, left in place: `IQuestionService.GetAllOrgaoSocialMembersAsync` / `HasOrgaoSocialPosition` /
  `GetMembersWithPositionAsync` / `CanAnswerAsync`, `IRequestService.GetPendingRequestsAsync`; most of
  `css/3-components/request-card.css`, `question-card.css` and `css/4-pages/questions.css` (the few selectors Meetings /
  Naipes used have no user since 034).

Raised by 030 (member shell):
- `/naipes/config` is not in the member menu (reached from `/naipes` → "Configurar" since 033); decide. (`/members/map` is, since 032.)
- The Blazor pages keep their own navbar (`MainLayout`), so a member moving between React and Blazor pages sees two
  different menus until those pages are React.
- No JS test runner in the repo: the shell's browser behaviour (drawer, rail, overflow) was checked by hand with a
  scratch Playwright script; `MemberShellTests` pins the source contract only.

Raised by 029A (Images / Labels admin removal, UI only):
- (043, written, not applied) `Labels`/`Slideshows` dropped by migration with their entities, DbSets and seed; services,
  repositories and their tests went in 042 (`docs/db-cleanup-inventory.md`).
  Any production edits to the `ranking_story` label are no longer shown; copy them out first if wanted.
- `ImageUploadManager` (Shared, + bUnit tests) has no caller since `Slideshows.razor` went.
- Slideshow images in R2 (upload category `slideshows`) are left in place, unused.

Raised by 027 (Messages / Conversas removal, Phase A):
- (039) **No contract task for messaging:** `Conversations`, `Messages`, `MessageReactions`, `ConversationUserSettings`
  and their entities/configs/DbSets are preserved, never dropped; Messaging v2 will audit and evolve them
  (`docs/messages-removal.md`).
- Now unused, left in place: `window.appBadge` (`pwa-helper.js`); the `/hubs/` never-cache prefix in `service-worker.js`
  (harmless, pinned by `ServiceWorkerReliabilityTests`).
- The Play Store (TWA) app may carry the old "Mensagens" shortcut until it is rebuilt from the manifest; it opens a 404.
- DEV after a refresh holds production's messages unsanitized (`DatabaseSanitizer` skips them); Messaging v2 decides.

Raised by 026 (Games / Bets / MyTuno removal):
- Task 043 (never the messaging tables, since 039), only after the release carrying 026 is live in production: drop
  the 15 game tables and `AspNetUsers.FidelisBalance` (written in 043, not applied: `RemoveRetiredGamesLabelsSlideshowsSchema`
  + `RemoveFidelisBalanceFromApplicationUser`), delete the kept
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
- (037 / 039 / 042, resolved) Dead code with no caller: Shared `ReportCard`, `TransactionCard`, `MbwayTransferCard`,
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
- (037 / 042, resolved) `BoardCard`, `kanban.css` (037) and `ILogistics{Board,List,Card}Service` + tests (042) are gone.

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
- (039, resolved) `README.md` rewritten: `isEmptyDb`, the stale IDrive keys and "Production Configuration" removed.
Carried (one line each; detail in git history):
- Security: `UseHttpsRedirection` skipped outside Development (now redundant, forwarded headers are
  on); login `OnRejected` is global; 429 on a form POST is a bare text page; `UpdateSecurityStampAsync`
  on role change is a forced logout; `ValidationInterval` default 30 min; COOP/COEP/CORP not set;
  unused `X-CSRF-TOKEN` antiforgery header; dead `POST /api/push/broadcast|send-to-selected`.
- Password-policy hardening skipped by owner decision (2026-09-21).
- `LastLoginDate` means "last authenticated activity"; `IsInRoleAsync` pair is 2 SELECTs.
- Dependencies: Microsoft 10.0.11 → 10.0.12 (unblocks `MockQueryable.Moq` 10.0.12); AWSSDK train;
  QuestPDF 2026.x major. (042) Dead `AWSSDK.Core` refs removed from Web.Tests / Integration.Tests (Application.Tests uses
  Amazon types directly, kept).
- `xUnit1051` suppressed; MTP telemetry (`TESTINGPLATFORM_TELEMETRY_OPTOUT=1` to opt out).
- Push: `WebPushClient` newed up (untestable sends); two service-worker registration paths;
  unbounded broadcast; `PushNotificationsManager` parked on `window`.
- Dead/orphaned front-end bits: `rtub.carousel.js`, rare-tab animation;
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

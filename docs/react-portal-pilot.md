# React public portal (React track, tasks 001–006)

A React public shell served by the existing ASP.NET Core host, next to the Blazor app, which keeps
every page React does not own yet. Since task 004 it is the **DEV public baseline**: React owns `/`.
Decision record:
`docs/architecture/adr/0002-react-public-portal-pilot.md`.

The React track numbers its own tasks from **001** (`feat/001/react-portal-pilot`), separately
from the repository's global unit sequence.

DEV runs hybrid; the `master`/PROD cutover happens later, once `dev` no longer depends on Blazor UI
pages. Task 004 is a DEV change only.

## Rules for every React module

**Replacement rule.** Once a module has a reviewed React version, its Blazor UI is retired: the page
is removed, its old route redirects to the React one when it differs, and Blazor UI tests that
pinned the old page are removed or rewritten. Backend services and entities stay while they are
still useful.

**Testing rule.** Old tests are a behavioural reference only; never copy-paste them. React and API
behaviour gets new tests written from scratch. Email, push, storage and other external effects run
against fakes/stubs. Every module includes negative and security cases (refused tokens, bad input,
anonymous callers, rate limits, what must *not* be exposed).

**Database/schema rule.** A React replacement does not by itself justify a database change. DTOs and
API contracts can be clean and new; persisted EF models and tables stay compatible with the real
`app.db`. If a schema change looks necessary, stop and report before making it.

**Wording rule.** "Migration" means EF Core/database migrations only, and developer-only technical
context (docs, tests). Never in UI, routes, feature names, page titles or user-facing copy.

## Audience

The portal is **public**: an overview of RTUB, public performances, music, the current Órgãos
Sociais, the gallery, performance requests and, later, a public Feed. The **member area is only for
RTUB members**: there are no public accounts - the tuna creates each member's login. Portal copy and
layout follow that: public actions ("Pedir atuação") lead; the visitors' way in is a quiet "Login" link to
`/login` (030; before it read "Membros" / "A minha conta" and opened `/profile`). Signed-in members get the
member shell instead (see *Member shell*). Never present login or registration as a public feature.

## Naming

User-facing text is Portuguese. Code, routes, URL fragments, files, components, CSS classes, APIs,
DTOs and branches are English: "Novidades" on screen, `/news` in the URL; "Pedidos" on screen,
`/request` and `#request`. (Task 002 renamed the earlier `/portal/privacidade`, `/portal/perfil` and
`/portal/pedidos`; task 004 dropped the `/portal` prefix. None had reached production.)

## Home page and navigation

Home order: hero and quick facts → "Quem somos" (short, facts only) → Atuações (a pointer to the full
agenda, with the FITAB highlight) → "Últimas novidades" (the 3 newest posts of `/news`, hidden when there are
none; 025) → Música → Galeria → Junta-te a nós → Pedidos + member area → install the app. Nothing about news
closes the page.

- **Top bar (visitors):** Atuações, Novidades (025), Música, Galeria, Órgãos Sociais; the **"Pedir atuação"**
  call to action (the hero's primary button also goes straight to `/request`); the quiet **"Login"** link (030).
  The phone menu has the same sections, "Pedir uma atuação" and "Login". FITAB, Junta-te and the app install
  are never in the top bar.
- **Top bar (signed-in members, 030):** no public sections and no "Pedir atuação"; who is signed in (avatar,
  name, first category) linking to `/profile`, and the member menu (*Member shell*).
- **Footer:** every home anchor (Quem somos, the five sections, Pedidos, FITAB, Junta-te a nós), then
  Fazer um pedido, Instalar a app, Área de membros, Política de Privacidade, the contact email, the
  social links and the build version.
- **FITAB:** a compact highlight inside Atuações (`/#fitab`), plus a footer link. Only what is
  certain: organised by the RTUB, yearly, tunas from home and abroad, dates announced on the RTUB's
  social networks. No editions, dates, line-ups or schedules.
- **Hierarquia:** not on the home page (the Leitão → Caloiro → Tuno → Magister grid is internal
  detail). One public line in Junta-te a nós ("Percurso"). The full explanation belongs, with the
  História, in a future public "Conhece a Tuna" page (English route `/about`); the member view stays
  on the Blazor `/hierarchy` (members only).
- **No invented data (005, 010):** the agenda shows the next three events from
  `GET /api/public/events/upcoming` (read-only; the same events, fields and order `/events` already
  shows visitors: name, date, time, location, type, cancelled) and an empty state when there are none.
  The full agenda is the React `/events` (011, `docs/react-events.md`), from the same service. The gallery preview
  shows the five latest **public** photos from `GET /api/gallery?public=true` (members-only photos
  never appear on the home, signed in or not).
- **No test or update banners (006):** the "Versão de testes" strip (005) and the service worker's
  "Nova versão disponível! · Atualizar · Depois" prompt are retired. Updates are silent; see
  Layout below.

## Public content coverage (task 005)

Every public section of the old Blazor site and app, and where it lives now. Pinned by
`PortalContentCoverageTests` (required sections present, one decision per row).

| Section | Source | Purpose | React now | Decision |
| --- | --- | --- | --- | --- |
| Quem somos / Sobre | old home: `AboutUsContent` (labels) | who the RTUB is | `/#about`, short, facts only | homepage now |
| História | old home: `HistoryContent` (`history_*` labels) | founding (1 Dec 1991), mission, identity | founding year in hero and Quem somos | future React page ("Conhece a Tuna", `/about`) |
| Atuações / agenda | React `/events`, `/events/{id}` (011) | upcoming and past performances | `/#events`: next three events (010) + the React agenda | homepage now + navbar now; done (011, `docs/react-events.md`) |
| Atuações anteriores, prémios, vídeos | old home: `AboutUsContent`; `/events` | track record | archive, "Prémios" and videos on `/events` (011) | navbar now; done (011); management in React (011.5-012F) |
| FITAB | old home: `FitabContent` (`fitab_*` labels) | the RTUB's festival | `/#fitab` highlight + footer | homepage now + footer only |
| Pedidos | React `/request` | performance requests | done (003) | homepage now + navbar now (CTA) |
| Música | React `/music`, `/music/albums/{id}` (006) | discography, lyrics, player, videos, statistics | done (006, `docs/react-music.md`); `/#music` preview | homepage now + navbar now |
| Galeria | React `/gallery` (009) | photos and videos | `/#gallery` latest public photos (010) + `/gallery` timeline | homepage now + navbar now; done (009, `docs/react-gallery.md`) |
| Órgãos Sociais | React `/roles` (008) | bodies and the holders of each mandate | `/roles` (the home block was removed in 011; top bar, menu and footer link there) | navbar now; done (008) |
| Junta-te a nós | old home: `JoinUsContent` (`join_us_*` labels) | recruiting: rehearsals, place, first step | `/#join` + footer | homepage now + footer only |
| Hierarquia / categorias | old home: `HierarchyContent`; `/hierarchy` (members) | Leitão → Caloiro → Tuno → Magister | one "Percurso" line in Junta-te | future React page ("Conhece a Tuna"); full grid excluded from home |
| Redes sociais | old home social grid | Facebook, Instagram, YouTube, Spotify | footer "Redes"; Spotify/YouTube in Música | footer only |
| Destaques (slideshow) | old home carousel, public slides (`/images` admin) | curated photos | none | exclude/defer: dropped; the `/images` admin was removed in 029A (no replacement); tables go in 029B |
| Contacto | Pedidos, footer | email | Pedidos card + footer | homepage now + footer only |
| Política de Privacidade | React `/privacy` | legal text | done (004) | footer only (+ request form link) |
| Área de membros / login | React `/profile`, React `/login` (007) | members-only entry | quiet "Login" header link (030), home card, footer; members: the member shell (030) | navbar now (quiet link); login done (007) |
| Password reset, email confirmation | `/forgot-password`, `/reset-password`, `/confirm-email` | account recovery from login and emails | reached from the React `/login` and emails | temporary Blazor bridge |
| Instalar a app | React `/#app`; old `PlayStorePrompt` popup | Play Store, Home Screen | `/#app` + footer | homepage now + footer only; popups exclude/defer (STATE) |
| Push opt-in, login popup | old home: `PushNotificationPrompt`, `LoginPopup` | member prompts | none | exclude/defer: member-facing, recorded in STATE |
| Novidades / News | React `/news` (025) | public posts by Admin/Owner | top bar, menu, footer; home preview after Atuações | navbar now + homepage now (latest 3); `docs/react-news.md` |
| Editable home copy | Labels admin (`/labels`, "Conteúdo") | admins edited the old home's text | React copy is static | exclude/defer: static copy kept; the `/labels` admin was removed in 029A; tables go in 029B |
| Partilhar | `/share` (manifest `share_target`) | receives shares from the OS | not linked | exclude/defer: technical PWA endpoint |
| Calotes, MBWAY, Nerba | `/calotes`, `/mbway`, `/nerba/{id}` | internal finance pages, reachable without login | not linked | exclude/defer: not public content; access to be reviewed (STATE) |

## Route ownership

Declared in one place, `src/RTUB.Web/Program.cs` (`MapFallbackToFile`, GET/HEAD only; the `/portal...`
redirects sit next to it). Pinned by `tests/RTUB.Integration.Tests/PortalRouteTests.cs`.

| Path | Class | Notes |
| --- | --- | --- |
| `/` | **React canonical** (004) | Public home: hero, agenda, discography, gallery, joining, Pedidos + member entry. The Blazor `Index.razor` was retired. |
| `/privacy` | **React canonical** (004) | Privacy Policy. `portal/src/Privacy.tsx` is now the legal source (verbatim from the retired `Privacy.razor`). |
| `/profile` | **React canonical** (004) | Members-only notice with public shortcuts; signed out → `/login?returnUrl=/profile` and back; signed in → the member's own profile and editor since 032 (Pessoal, Tuna, Instrumentos, Foto, Notificações por email, Segurança; was the Blazor `/member/profile`), with "As minhas inscrições" and "Terminar sessão" (`POST /auth/logout`, 012F). It is not the members' menu: the member shell is. `docs/react-member-area.md`. |
| `/request` | **React canonical** (004) | The only public performance request form (see Request). `POST /request` → 405. |
| `/portal` | **Redirect** → `/` | `302`, query string kept, GET/HEAD only (POST → 405). Pilot URL from tasks 001-003. |
| `/portal/privacy` | **Redirect** → `/privacy` | Same. |
| `/portal/profile` | **Redirect** → `/profile` | Same. |
| `/portal/request` | **Redirect** → `/request` | Same. |
| `/music`, `/music/albums/{id}` | **React canonical** (006) | Music area: albums, songs, player, lyrics, videos, statistics, management. `docs/react-music.md`. |
| `/music/songs/{id}` | **Redirect** → `/music/albums/{id}` | Retired Blazor album page; `302`, query kept, GET/HEAD only. |
| `/news` | **React canonical** (025) | Novidades: public wall of text posts, newest first; Admin/Owner write, publish and delete. `docs/react-news.md`. |
| `/gallery` | **React canonical** (009) | Photo timeline; `?item=` opens one. `docs/react-gallery.md`. |
| `/member/gallery` | **Redirect** → `/gallery` (015) | `302`, GET/HEAD only. Upload, tags, edit and delete are React on `/gallery`. |
| `/events`, `/events/{id}` | **React canonical** (011) | Agenda and one event; members answer in a modal (card quick reply or the event page; `?respond=1` opens it); Admin/Owner create, edit and delete in modals (011.5). Events say *enrollment*, never *attendance* (rehearsals). `docs/react-events.md`. |
| `/events/{id}/enrollment` | **Redirect** → `/events/{id}?respond=1` | `302`, GET/HEAD only: the answer page of the first 011 build, now a modal. The draft `/events/{id}/attendance` is 404. |
| `/member/events` | **Redirect** → `/events` (012F) | `302`, query kept, GET/HEAD only. The Blazor bridge (011-012E) is retired; everything it did is on the React agenda and event pages, statistics included. |
| `/events/my-enrollments` | **React** (012E) | The member's own answers ("As minhas inscrições"). |
| `/events/{id}/enrollments` | **Retired** (012E) | 302 to `/events/{id}#who-title`; Admin/Owner manage answers in the React event page. |
| `/events/{id}/discussion`, `/contacts` | **React** (013) | The event's conversation (members) and contact tracking (Mod and above); visitors get a 302 to `/login?returnUrl=…`. No Blazor event page is left. |
| `/roles` | **React canonical** (008, 016) | Órgãos Sociais; `?fy=` picks a mandate. RGI for members, fiscal years and assignments for Mod/Admin/Owner (016). `docs/react-governance.md`. |
| `/member/roles` | **Redirect** → `/roles` (016) | `302`, query kept, GET/HEAD only. The Blazor bridge (008-015) is retired. |
| `/login` | **React canonical** (007) | Members-only login; signed in → `302 /events`. See Login (007). `Login.razor` retired. |
| `POST /auth/login`, `POST /auth/logout` | **Auth endpoints** (unchanged) | Identity cookie sign-in/out, antiforgery, per-IP limit. See Login (007). |
| `/member/profile` | **Redirect** → `/profile` (032) | `302`, GET/HEAD only, for everyone. The Blazor profile editor (moved here in 004) is retired. |
| `/members`, `/members/hierarchy` | **React** (017) | Directory (details, active members, birthdays) and the Padrinho → Afilhado tree, signed-in members; visitors get a 302 to sign in. `docs/react-members.md`. |
| `/hierarchy` | **Redirect** → `/members/hierarchy` (017) | `302`, GET/HEAD only. |
| `/members/manage` | **Redirect** → `/members` (018) | `302`, GET/HEAD only. The admin tools are on the React `/members` (Admin/Owner); `Members.razor` retired. |
| `/leaderboard` | **React** (019) | Tabela de Classificação: XP table, details, comments; signed-in members, visitors get a 302 to sign in. `docs/react-leaderboard.md`. |
| `/inventory` | **React** (020) | Instrumentos: list, details, Mod/Admin/Owner management with image; signed-in members, visitors get a 302 to sign in. `docs/react-inventory.md`. |
| `/shop` | **React** (021) | Loja RTUB: products, member reservations, Mod/Admin/Owner management; signed-in members, visitors get a 302 to sign in. `docs/react-shop.md`. |
| `/documentation` | **React** (022) | Documentação: folders per fiscal year, download, member upload, Owner folder/document management; signed-in members (Leitões refused), visitors get a 302 to sign in. `docs/react-documentation.md`. |
| `/logistics`, `/logistics/{id}` | **React** (023) | Logística: boards, then a horizontal Kanban (lists, cards, status, labels, checklist, links, members, board files, reminders); signed-in members (Leitões refused), Mod/Admin/Owner manage; visitors get a 302 to sign in. `docs/react-logistics.md`. |
| `/treasury`, `/treasury/reports/{id}`, `/treasury/calotes`, `/treasury/mbway`, `/treasury/nerba[/{eventId}]` | **React** (024) | Tesouraria: annual reports (totals, activities, transactions, receipts, PDF, history), calotes, MBWay, Nerba orders; signed-in members (Caloiros and Leitões: own calotes only), visitors get a 302 to sign in; old `/finance`, `/calotes`, `/mbway`, `/nerba` URLs 302 here. `docs/react-treasury.md`. |
| `/rehearsals`, `/rehearsals/{id}` | **React** (014) | Ensaios: presenças (attendance), Admin/Owner management; visitors get a 302 to sign in. `docs/react-rehearsals.md`. |
| `/members/map` | **React** (032) | Mapa de membros: members by city (geocoding cache), without a city, cities waiting; signed-in members (Leitões included), visitors get a 302 to sign in. `/member/map` 302s here. `docs/react-member-area.md`. |
| `/hall-of-fame` | **React** (032) | The twelve records (ties, positive durations, past / not-cancelled activity); signed-in members, visitors get a 302 to sign in. `docs/react-member-area.md`. |
| `/naipes`, `/naipes/config` | **React** (033) | Naipes: each instrument's videos and images, search, comments, plays; any member adds and edits their own, Admin / Owner manage all and the instrument settings (`/naipes/config`, a clear refusal for others). Signed-in members, visitors get a 302 to sign in. `docs/react-naipes.md`. |
| `/requests`, `/questions` | **React** (031) | Gestão de Pedidos (members but Leitões read; Admin/Owner answer) and Perguntas aos Órgãos Sociais (members); visitors get a 302 to sign in. `docs/react-requests-questions.md`. |
| `/meetings` | **React** (034) | Reuniões: meetings (AG / AGE, CV, Direção) by visibility, Vou / Não vou, participants, atas (edit, publish, PDF, confirmation) and meeting requests; who sees and does what is the old page's matrix, enforced by the API (one change: a draft ata only for its writers). Signed-in members (a Leitão gets "Acesso restrito"), visitors get a 302 to sign in. The Blazor page is retired. `docs/react-meetings.md`. |
| every other member/admin page | **Blazor member/admin, pending** | `/emails`, `/notifications`, `/users`, the owner tools, etc. Unchanged. (`/messages` was removed in 027; `/images` and `/labels` in 029A, both plain 404s.) |
| `GET /api/account/me` | **API** (002) | `AccountController`: the caller's own session summary for React. |
| `GET /api/public/antiforgery-token` | **API** (003) | `Endpoints/PublicRequestEndpoints.cs`: token for the request form, the login and Music writes. |
| `POST /api/public/requests` | **API** (003) | The only public request submission path. |
| `GET /api/gallery`, `GET /api/gallery/items/{id}` | **API** (009) | Viewer-aware, read-only: visitors get public items only; `?public=true` gives anyone the visitors' view (home preview, 010). |
| `GET /api/public/events/upcoming` | **API** (010) | Next three events for the home; anonymous, read-only, public fields only. Since 011 from `IEventAgendaService`. |
| `/api/events/...` | **API** (011) | `Endpoints/EventEndpoints.cs`; reads viewer-aware and open; enrollment and video-play writes need the antiforgery header. |
| `GET /api/public/governance` | **API** (008) | `?fiscalYear=`; anonymous, read-only, public fields only. |
| `/api/music/...` | **API** (006) | `Endpoints/MusicEndpoints.cs`; reads open, every write needs the antiforgery header. |
| `/portal/assets/*` | static files | Content-hashed Vite output (the build's folder, not a page), normal static caching. |
| any other `/portal/*` | nobody | 404. |

The shell `index.html` is served `Cache-Control: no-cache` so a deploy is picked up at once. It
carries the enforced CSP like every HTML document. Links cross the React/Blazor boundary as plain
full-page loads; Blazor links into React routes carry `data-enhance-nav="false"` so Blazor's
enhanced navigation does not try to patch a React page. No app-facing link points to `/portal`
(guarded by `ReactSourcesAndBuild_LinkOnlyToCleanRoutes`).

**`/` side effects (task 004).** The Blazor home also showed the push-notification opt-in prompt,
the custom login popup and the Play Store prompt, and gave signed-in members their bottom nav. None of
that renders on the React `/`; the installed app (`start_url` `/?utm_source=pwa`) and push
notifications without a URL now open the React home. Members reach their area through the member shell
(030), or any Blazor page's own navigation.

## Before any production release

The React public shell ships with whatever `dev` holds at the next `dev → master` release. Since 010
the agenda and the gallery preview read real data; the **static (not admin-editable) copy must still
be accepted by the owner, or replaced, before that release.**

## Layout

| Path | What |
| --- | --- |
| `src/RTUB.Web/portal/` | Source: `index.html` (shell + launch splash), `src/*.tsx`, `styles.css`, `vite.config.ts`, `tsconfig.json`. Excluded from publish (`RTUB.csproj`). LF-only (`.gitattributes`). |
| `src/RTUB.Web/wwwroot/portal/` | **Committed** build output, published as-is. |
| `src/RTUB.Web/package.json` | `build:portal`, `check:portal`; React 19, `@fontsource-variable/fraunces` as devDependencies (bundled at build time). |

Stack choices, all deliberately minimal: React 19 + Vite 6 with esbuild's
JSX runtime (no `@vitejs/plugin-react`), no router (the server maps two paths; `main.tsx` picks the
page once), no state or CSS library, bootstrap-icons path data inlined as SVG. React is split into
`vendor-react-*.js` so it caches across portal releases.

**Why the build is committed and not rebuilt on publish:** file names are content-hashed, and the
Web SDK globs `wwwroot` before `BeforePublish` targets run, so a publish-time rebuild that produced
different hashes would ship a shell pointing at missing files. Committing keeps `dotnet run`,
the test suites and the publish zip independent of Node. The cost is drift, closed by the CI check
below. Sources are pinned to LF so the build is byte-identical on Windows and Linux.

## Constraints kept

- **CSP (unit 025) unchanged.** No inline script or style: the bundle is external module scripts,
  React writes no style attributes, and the Fraunces fonts are self-hosted under `/portal/assets/`
  (`font-src 'self'`; latin subset only, 67 + 82 KB). The CSP guard tests sweep the portal's own
  chunks; only `vendor-react-*.js` is excluded, as vendored code like `wwwroot/lib` (its
  `<style precedence>` path is never used).
- **PWA identity unchanged.** Same `manifest.webmanifest` (id `/`, `start_url` `/?utm_source=pwa`),
  same Apple meta, same single service-worker registration (`/js/sw-register.js`). React page
  navigations are network-only (and the shell is `no-cache`) with the offline fallback; the hashed
  assets stale-while-revalidate like any JS/CSS.
- **Cache busting, no update prompt (006).** The old "Nova versão disponível" prompt made users
  reload when a new service worker was waiting. It is gone: a new worker downloads in the background
  and takes over once every RTUB window is closed (no `skipWaiting`, no forced reload). Freshness
  comes from URLs, not from the worker: Vite names every script, stylesheet and font by content hash,
  Blazor assets carry `?v=` (`VersionedAsset`), so a deploy is picked up on the next navigation.
  Ceiling: the image cache is cache-first by URL, so an image replaced *at the same URL* stays stale
  until the worker's `CACHE_VERSION` changes and it activates; publish changed images under a new
  URL.
- **No business logic in React, no database change.** Task 001 called only `GET /api/version`
  (footer); 002 and 003 added the two APIs in the route table. Login is the React `/login` (007) over the
  unchanged `POST /auth/login`, which keeps its antiforgery and rate limiting.

## Copy rule

The current site and app are the reference for structure, tone, page inventory and facts. **Their
sentences are never copied into the portal**: portal copy is original Portuguese.
`PortalCopyOriginalityTests` fails if any six-word run of portal copy (text nodes and multi-word
strings; proper names removed) appears in the Blazor pages, components, seeded labels or static
HTML. The Privacy Policy is the one exception: it is legal text, carried verbatim from the retired
`Privacy.razor`; since task 004 `portal/src/Privacy.tsx` is the only copy and the legal source.

**Institution naming.** Present-day copy (current identity, location, CTAs) says **UPB** /
*Universidade Politécnica de Bragança*. Historical context (founding, old documents, songs, older
screenshots or sources) keeps **IPB**. Never mass-replace; bridge only when it helps ("fundada no
contexto do IPB, hoje UPB"). Legal text in the Privacy Policy is left as the policy states it.

Sources consulted for task 001: the public pages of the live site (home, `/music`, `/roles`), the
seeded labels (`SeedData.Labels.cs`), `Roles.razor`, `Request.razor` and `EventType`. The RGI and
the cancioneiro are member-only documents in R2 storage and were not accessed.

## Órgãos Sociais (008)

`/roles` is React (`portal/src/Governance.tsx`) over `GET /api/public/governance`
(`Endpoints/GovernanceEndpoints.cs` → `IGovernanceService`). The old Blazor `/roles` mixed three
audiences; only its public view was rebuilt in 008. The RGI and the management lived on the members'
Blazor `/member/roles` until **016 moved them to the React `/roles`** (`docs/react-governance.md`);
`/member/roles` now redirects there.

| Source | Field / data | Public | Private / internal | Old public UI | React | Schema change |
| --- | --- | --- | --- | --- | --- | --- |
| `RoleAssignments` | `Position`, `StartYear`, `EndYear` | yes | – | yes | yes | no |
| `RoleAssignments` | `Notes`, `CreatedBy`, audit dates, `Id`, `UserId` | no | yes | no | no | no |
| `AspNetUsers` | `Nickname`, `FirstName`, `LastName` | yes | – | yes (nickname + full name) | yes, same | no |
| `AspNetUsers` | `ImageUrl` (public R2 URL) | yes | – | yes (default avatar if empty) | https or same-site only, else default | no |
| `AspNetUsers` | email, phone, birth date, address, roles, categories, ids | no | yes | no | no | no |
| `FiscalYears` | years created by Mod/Admin | – | – | selector, all years | not read: the selector lists years with holders | no |
| `Position` enum | 13 positions in 5 groups | yes | – | fixed layout | `GovernanceService.Structure` | no |
| RGI (`docs/rtub_rgi.pdf`, R2) | members-only document | no | yes | members | `/roles`, members (016) | no |

- **Real data (read-only, counts):** 36 fiscal years (1991-2027), 16 with holders, 124 assignments,
  58 distinct holders; no orphan, duplicate position, note or expelled holder; nickname always set;
  every photo is on the public R2 host or empty (41 empty → default avatar). Positions are
  per fiscal year; there are no separate mandate/term dates.
- **Verdict:** no schema change, no migration.
- **Mandate shown:** `?fy=` when it has holders; otherwise the current fiscal year when it has
  holders, else the latest one that does (in September the new year has no holders yet). The page
  always says which (`Mandato 2025-2026`).
- **Changed on purpose:** every holder of a position is listed (the old page showed only the first,
  so a second Ensaiador was hidden); vacant positions read "Sem registo" instead of "N/D"; empty
  fiscal years are no longer offered.
- **Hierarquia:** the old `/roles` never showed it; still not shown (future "Conhece a Tuna").
- **Follow-ups:** done in 016 (management and RGI in React, `/member/roles` retired).

## Login (007)

`/login` is React (`portal/src/Login.tsx`); the Blazor `Pages/Identity/Login.razor` is retired. The
auth backend is unchanged: no Identity, hashing, schema or data change, no public registration.

| Route / endpoint | Owner | Behaviour | Decision |
| --- | --- | --- | --- |
| `GET /login` | Program.cs → React shell | Signed out: the form. Signed in: `302 /events`, `returnUrl` ignored on purpose (`/login` is also the cookie's `AccessDeniedPath`; honouring it would loop a member on a page their role cannot open). | React (changed) |
| `POST /auth/login` | Program.cs | Username, or email when it contains `@`; unconfirmed email → Invalid; lockout and expelled checked before the password; wrong password → `AccessFailedAsync` (5 → 5 min lockout); remember-me; `LastLoginDate`. Antiforgery form token, per-IP limit (10/window, 429). | Keep. Adds `Accept: application/json` answers: `200 { redirect }` or `401 { error: Invalid/Locked/Expelled }`; plain form posts still redirect to `/login?error=…`. Default landing `/` → `/events`. |
| local-only `returnUrl` | `UrlHelper.IsLocalUrl` | `/x` yes; `//x`, `/\x`, absolute or scheme URLs → `/events`. Read case-insensitively (`ReturnUrl` from the cookie handler, `returnUrl` from links). | Keep (server-side only) |
| `POST /auth/logout` | Program.cs + Blazor layout form | Antiforgery form, signs out, `302 /`. | Keep |
| `/forgot-password`, `/reset-password`, `/confirm-email` | Blazor `Pages/Identity` | Recovery and confirmation; link back to `/login`. | Keep as Blazor bridges; React login links to `/forgot-password` |
| Expelled member with a live cookie | cookie validator | Session rejected on every request, so `/login` shows the form again. | Keep |

The page reads `?error=` for the plain-form fallback, shows field errors, a busy button and one
banner per outcome (invalid, locked, expelled, throttled, expired token, failure). Copy is
members-only: no public registration, access created and managed by the tuna, the portal open to
everyone. Pinned by `ReactLoginTests`, `AuthAntiforgeryTests`, `LoginRateLimitTests` and
`PortalRouteTests`.

## Session and profile (task 002)

- **Signing in is the React `/login` (007)**, over the antiforgery-protected, rate-limited
  `POST /auth/login`; the portal sends visitors to `/login?returnUrl=/profile`. Sign-out is still
  a POST from the Blazor layout with its own token; the portal only points to it.
- **`GET /api/account/me`** (anonymous-allowed, `Cache-Control: no-store`, GET only) returns
  `{ authenticated: false }` or the caller's own `displayName` (nickname → first name → username),
  `fullName`, `avatarUrl`, category labels (`StatusHelper.GetCategoryDisplay`) and, since 030, `menu`: four
  booleans saying which member-menu groups to show (`MemberMenuAccess`). No email, phone, birth date, role
  list, IDs or other users. Expelled or deleted members arrive anonymous: the cookie validator rejects their
  session on every request. Pinned by `AccountEndpointTests`.
- React calls it once per page load (`getCurrentUser` in `portal/src/api.ts`); the header shows "Login" or the
  member shell, and `/profile` has loading, error (retry), signed-out and signed-in states. Unknown or failed
  session state falls back to the visitors' header ("Login").
- Authorization is unchanged: nothing in React grants access, and a hidden menu link is a convenience only;
  every page and API still enforces its own rules.

## Member shell (task 030)

Signed-in members navigate with one menu (`portal/src/MemberShell.tsx`), not with `/profile`:

- **Wide screens (≥ 1000px):** a rail under the header, left side; the page moves over for it. Expanded it shows
  icons and labels (groups can be folded); collapsed it shows icons only, with the labels kept for screen readers
  and as tooltips. The collapsed choice is kept per browser (`localStorage` `rtub-member-rail`).
- **Phones and the installed app:** the menu button opens the same menu in a right-hand drawer (native modal
  `<dialog>`: Esc, focus containment, backdrop tap), 48px rows, safe-area insets; it closes after every choice.
- **Groups:** Membro (Perfil, Atuações, As minhas inscrições, Ensaios, Música, Galeria, Novidades) · Tuna
  (Membros, Hierarquia, Órgãos Sociais, Classificação, Hall of Fame, Naipes) · Recursos (Instrumentos, Loja,
  Documentação) · Gestão (Reuniões, Questões, Logística, Tesouraria, Pedidos, Emails, Notificações) · Owner
  (Utilizadores, Auditoria, Base de dados). Blazor pages open as full page loads.
- **Who sees what** (`MemberMenuAccess`, the Blazor navbar's rules): Documentação, Reuniões, Questões → Admin,
  Owner, Mod or anyone not a Leitão; Logística, Tesouraria → Admin, Owner, Mod or Tuno; Pedidos, Emails,
  Notificações → Admin; the Owner group → Owner. The rest → every member.
- **Never listed:** the removed modules - Jogos, Bets, MyTuno (026), Mensagens (027), Imagens and Conteúdo /
  Labels (029A). `MemberShellTests` pins the list, the gating and that every link opens a live route.
- **No jump on load:** `localStorage` `rtub-member-shell` remembers that the last load was signed in, so a
  member's page opens in the shell while `/api/account/me` answers; the answer always wins, and signing out
  clears it. A visitor never waits.

## Request (task 003: React replacement; legacy Blazor page retired)

React `/request` is **the only public request form**. The legacy Blazor `/request` page
(`Pages/Public/Request.razor`) and its form were removed in 003; since 004 React serves `/request`
itself (`/portal/request` redirects there, query string kept, `302` while DEV is hybrid), and the
Blazor navbar's "Pedidos" link points at the React form directly. There is one submission path:
**`IPublicRequestService.SubmitAsync`** (Application layer), extracted verbatim from the retired
page's handler: the `Request` entity's annotations, the date rules of `RequestValidationService`
(same messages), `RequestService.CreateRequestAsync` (which still pushes to every Admin and Owner),
`SetRequestDateRangeAsync` when an end date is given, then the RTUB email. Only the API calls it.

**`POST /api/public/requests`** (anonymous, form-encoded; dates `yyyy-MM-dd`):
- **CSRF:** form-bound, so the framework enforces antiforgery (400 before the handler without a
  valid token); the React page first calls `GET /api/public/antiforgery-token` (no-store).
- **Rate limit:** policy `public-requests`, 5 per client IP per 60 min (`PublicRequestRateLimit`
  settings), fixed window, no queue; 429 as `application/problem+json` with its own message and
  `Retry-After`. The shared `OnRejected` now answers per policy; the login text is unchanged.
  Partitioned on `RemoteIpAddress` only - on Azure this relies on forwarded headers being enabled,
  otherwise every visitor shares one budget: **check on DEV**.
- **Honeypot:** hidden `website` field; if filled, the answer looks like success and nothing is
  stored or sent.
- **Validation:** server-side through the shared service; `400` validation problem with camelCase
  field keys. Unparseable dates are field errors too. Body limit 32 KB.
- **Failures:** logged; the client gets a generic `500` problem, never exception details.
- Answers: `200 {submitted:true}` · `400` field errors · `400` without errors = token refused ·
  `429` · `500`.

**React form** (`portal/src/Request.tsx`): the same fields and limits, client checks mirroring the
server messages, event-type suggestions via `<datalist>` (free text, as today), optional date range,
states for submitting, success (focused), field errors (focus to the first), refused token ("recarregue
a página"), throttled and generic failure. Labels, `aria-invalid`/`aria-describedby`, no login needed.

### Request field audit (EF entity = snapshot = real `app.db`, read-only check)

| Field | Type / limit | Blazor | React | Notes |
| --- | --- | --- | --- | --- |
| Id | int PK | - | - | internal |
| Name | text, required, ≤200 | yes | yes | |
| Email | text, required, email, ≤200 | yes | yes | |
| Phone | text, required, ≤20 | yes | yes | |
| EventType | text, required, ≤100 | yes | yes | free text; real rows are free descriptions, so no enum |
| PreferredDate | date, required, not past | yes | yes | |
| PreferredEndDate | date?, ≥ start, not past | yes | yes | only with the range option |
| IsDateRange | bool | toggle | toggle | stored `true` only through `SetDateRange`; the API sends an end date only with the range option |
| Location | text, required, ≤200 | yes | yes | |
| Message | text **NOT NULL**, ≤2000 | yes | yes | empty is stored as `""` |
| Status | int (Pending = 0) | - | - | internal, admin workflow |
| CreatedAt/By, UpdatedAt/By | audit | - | - | internal; `CreatedBy` is null for public requests |

**No database migration.** The existing `Requests` table and `Request` entity were reused as they
are; the React DTO maps onto them. Pinned by `MigrationChainTests` (no pending model changes; the
`Requests` columns unchanged). The real database was only read (schema, row count, max lengths): its
3 requests fit every limit, and all dated migrations in the repo are applied except the two known
never-run ones (STATE.md).

The admin side (list, approve, reject, delete, and turning an approved request into an event) is the React
`/requests` since task 031: `docs/react-requests-questions.md`.

## Install guidance (task 002)

The `#app` section on `/` (linked from the footer) shows four short routes. Android: the
Google Play listing `https://play.google.com/store/apps/details?id=ipb.pt.rtub.app`, taken from the
existing `PlayStorePrompt.razor`; its package matches `wwwroot/.well-known/assetlinks.json` (the
app is a TWA of this site). iPhone/iPad: Safari → Partilhar → Adicionar ao ecrã principal. Android
browser: Chrome menu → Instalar app / Adicionar ao ecrã principal, when offered. Computer: the
install icon in Chrome/Edge, when offered. No promise about notifications. Manifest, service worker
and TWA config are untouched; an installed app opens the manifest's `start_url` (`/`).

## News / "Novidades" (025)

Built in React track 025: `/news`, a public wall of text posts by Admin/Owner, with a latest-3 preview on the
home page. Rules, API, schema and follow-ups (images, comments, per-post pages with share previews):
`docs/react-news.md`.

## Next recommended slice

Done so far: Music 006, Login 007, Órgãos Sociais 008, Gallery 009, Events 011-013 (fully React, discussion and
contacts included). Rehearsals 014, Gallery management 015, Órgãos Sociais management 016, Members directory and hierarchy 017,
member admin 018, classification 019, instruments inventory 020, shop 021, documentation 022, logistics 023, treasury 024. Next:
025 Novidades (`/news`, in review: `docs/react-news.md`); the roadmap and task contract are in
`docs/RTUB_REACT_MIGRATION_GUIDE.md`.

## Next steps (outside this pilot)

- A read-only public API (upcoming public events, public albums, gallery highlights) to replace the
  illustrative content - built on the existing services, not duplicated in React.
- A client router, once the React routes stop being a short, fixed list. (`/` ownership was decided
  in 004; the PWA `start_url` stays `/?utm_source=pwa` and now opens the React home.)
- Authenticated pages need a React-usable auth model; today's authorization services depend on
  Blazor `AuthenticationState`.

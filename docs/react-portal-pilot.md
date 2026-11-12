# React portal shell

Every page is React, served by the ASP.NET Core host. The only other HTML page is the static `/Error`
(`wwwroot/error.html`). Decision record: `docs/architecture/adr/0002-react-public-portal-pilot.md`.

## Rules for every React module

- **Server enforces, React displays.** Every page and API enforces its own rule; a hidden button or menu link is a
  convenience only. No business logic in React.
- **Schema.** A UI change does not by itself justify a database change. DTOs can be new; EF models stay compatible with
  the real `app.db`. If a schema change looks necessary, stop and report.
- **Tests.** New API behaviour gets new tests, with negative and security cases (anonymous caller, refused token, bad
  input, rate limits, what must *not* be exposed). Email, push and storage run against fakes.
- **Wording.** "Migration" means EF Core migrations only, in developer context. Never in UI, routes, feature names or
  copy.
- **Naming.** UI text is Portuguese; code, routes, files, CSS classes, APIs and branches are English ("Novidades" on
  screen, `/news` in the URL).

## Audience

The portal is public: an overview of RTUB, the agenda, music, Órgãos Sociais, gallery, news and performance requests.
The member area is only for RTUB members: there are no public accounts, the tuna creates each login. Public actions
("Pedir atuação") lead; visitors get a quiet "Login" link to `/login`. Never present login or registration as a public
feature.

## Home page and navigation

Home order: hero and quick facts → Quem somos (facts only) → Atuações (next three events, FITAB highlight) → Últimas
novidades (3 newest `/news` posts, hidden when none) → Música → Galeria → Junta-te a nós → Pedidos + member area →
install the app.

- **Top bar, visitors:** Atuações, Novidades, Música, Galeria, Órgãos Sociais, the "Pedir atuação" call to action and
  "Login". FITAB, Junta-te and the app install are never in the top bar.
- **Top bar, members:** no public sections; who is signed in (avatar, name, first category, links to `/profile`) and the
  member menu.
- **FITAB:** only what is certain (organized by RTUB, yearly, tunas from home and abroad, dates on social networks). No
  editions, dates or line-ups.
- **No invented data:** the agenda reads `GET /api/public/events/upcoming`; the gallery preview shows the five latest
  public photos (`GET /api/gallery?public=true`; members-only photos never appear on the home).
- No test or update banners. Updates are silent (see Constraints).
- `PortalContentCoverageTests` checks that every `/#id` link in the shell exists on the home page.

## Launch splash and home opening

The splash is static markup in `portal/index.html` (logo on black while the bundle loads). Once React has committed,
`revealApp` (`App.tsx`) decides, on the first load of a browser session:

- `/` plays the opening (`styles.css`, *opening*; `.intro` on `<html>`, timings in `--intro-*`), about 3 s.
- Any other route gets the short curtain lift (a deep link never waits).
- Later loads in the session, or `prefers-reduced-motion: reduce`: the splash is removed at once.

A click, tap or key skips it. The splash is removed when its last animation ends, so nothing in it may loop. Transform
and opacity only, no new request (inline SVG, cached logo WebP), no inline style or script (CSP).

## Route ownership

Declared in one place, `src/RTUB.Web/Program.cs` (`MapFallbackToFile`, GET/HEAD only). Pinned by
`tests/RTUB.Integration.Tests/PortalRouteTests.cs`. Members-only pages send visitors to
`/login?returnUrl=…` (302). Each page's rules are in its `docs/react-*.md`.

| Path | Notes |
| --- | --- |
| `/`, `/privacy`, `/request`, `/music`, `/music/albums/{id}`, `/news`, `/gallery`, `/shop` (read-only catalogue), `/events`, `/events/{id}`, `/roles`, `/share`, `/forgot-password`, `/reset-password`, `/confirm-email`, `/login` | Public. `/privacy`: `portal/src/Privacy.tsx` is the legal source. `/share`: Web Share Target, GET only (`docs/react-identity.md`). |
| `/profile` | Signed out: notice + link to `/login?returnUrl=/profile`. Signed in: own profile editor (`docs/react-member-area.md`). |
| `/events/{id}/discussion`, `/events/{id}/contacts`, `/rehearsals[/{id}]`, `/members`, `/members/hierarchy`, `/members/map`, `/hall-of-fame`, `/leaderboard`, `/inventory`, `/music/play/{id}`, `/music/learn`, `/documentation`, `/logistics[/{id}]`, `/logistics/tasks`, `/treasury/...`, `/pending-actions`, `/requests`, `/questions`, `/naipes`, `/naipes/config`, `/meetings`, `/messages` | Members only. |
| `/emails`, `/notifications`, `/users`, `/owner/tracing`, `/owner/dashboard`, `/owner/db` | Owner only (`docs/react-owner-operations.md`). Other members get the shell with a 403 refusal. |
| `/Error` | Static server page for any method, no script, no details. |
| `/portal/assets/*` | Content-hashed Vite output, normal static caching. Any other `/portal/*` is 404. |

Old URLs kept as `302` redirects (GET/HEAD only): `/portal[/privacy\|/profile\|/request]` → same path without
`/portal` (query kept); `/music/songs/{id}` → `/music/albums/{id}`; `/events/{id}/enrollment` → `/events/{id}?respond=1`;
`/events/{id}/enrollments` → `/events/{id}#who-title`; `/member/events` → `/events`; `/member/gallery` → `/gallery`;
`/member/roles` → `/roles`; `/member/profile` → `/profile`; `/member/map` → `/members/map`; `/hierarchy` →
`/members/hierarchy`; `/members/manage` → `/members`; `/finance` → `/treasury`; `/finance/report/{id}` →
`/treasury/reports/{id}`; `/calotes` → `/treasury/calotes` (keeps `fy`); `/mbway[/{id}]` → `/treasury/mbway`;
`/nerba[/{id}]` → `/treasury/nerba`; `/nerba/event/{id}` → `/treasury/nerba/{id}`.

Shell-level APIs: `GET /api/account/me`, `GET /api/announcement`, `GET /api/public/antiforgery-token`,
`POST /api/public/requests`, `GET /api/public/events/upcoming`, `GET /api/public/governance`.

The shell `index.html` is served `Cache-Control: no-cache` and carries the enforced CSP (`connect-src 'self'`). Links
between pages are full page loads. No app-facing link points to `/portal` (`ReactSourcesAndBuild_LinkOnlyToCleanRoutes`).
Unknown URLs are a plain 404.

## Layout and build

| Path | What |
| --- | --- |
| `src/RTUB.Web/portal/` | Source: `index.html` (shell + splash), `src/*.tsx`, `styles.css`, `vite.config.ts`. Excluded from publish (`RTUB.csproj`). LF-only (`.gitattributes`). |
| `src/RTUB.Web/wwwroot/portal/` | **Committed** build output, published as-is. |
| `src/RTUB.Web/package.json` | `build:portal`, `check:portal`. |

Stack, deliberately minimal: React 19 + Vite 6 with esbuild's JSX runtime (no `@vitejs/plugin-react`), no router
(`main.tsx` picks the page once from the path), no state or CSS library, bootstrap-icons path data inlined as SVG.
React is split into `vendor-react-*.js` so it caches across releases. Fraunces woff2 fonts are static files in
`wwwroot/fonts/`, preloaded from `index.html`.

**Why the build is committed:** file names are content-hashed, and the Web SDK globs `wwwroot` before `BeforePublish`
targets run, so a publish-time rebuild would ship a shell pointing at missing files. Committing keeps `dotnet run`, the
tests and the publish zip independent of Node. CI checks for drift; LF sources keep the build byte-identical on Windows
and Linux.

## Constraints

- **CSP:** no inline script or style; React writes no style attributes; fonts are self-hosted (`font-src 'self'`). The
  CSP guard tests sweep the portal chunks except `vendor-react-*.js`.
- **PWA identity:** `manifest.webmanifest` (id `/`, `start_url` `/?utm_source=pwa`), one service-worker registration
  (`/js/sw-register.js`). Page navigations are network-only with the offline fallback; hashed assets
  stale-while-revalidate.
- **No update prompt:** a new service worker installs in the background and takes over once every RTUB window is
  closed (no `skipWaiting`, no forced reload). Freshness comes from content-hashed URLs.
- **Image cache ceiling:** images are cache-first by URL, so an image replaced at the same URL stays stale until the
  worker's `CACHE_VERSION` changes. Publish changed images under a new URL.

## Copy rule

Portal copy is original Portuguese; sentences from the old site are never copied. `PortalCopyOriginalityTests` fails if
any six-word run of portal copy appears in the seeded labels or static HTML. Exception: the Privacy Policy, legal text,
whose only copy is `portal/src/Privacy.tsx`.

**Institution naming.** Present-day copy says **UPB** (*Universidade Politécnica de Bragança*). Historical context
(founding, old documents, songs) keeps **IPB**. Never mass-replace; bridge only when it helps ("fundada no contexto do
IPB, hoje UPB"). The Privacy Policy stays as written.

## Login

`/login` is `portal/src/Login.tsx`. No public registration.

| Route | Behaviour |
| --- | --- |
| `GET /login` | Signed out: the form. Signed in: `302 /events`; `returnUrl` ignored on purpose (`/login` is also the cookie's `AccessDeniedPath`; honouring it would loop a member on a page their role cannot open). |
| `POST /auth/login` | Username, or email when it contains `@`. Unconfirmed email → Invalid; lockout and expelled checked before the password; 5 wrong passwords → 5 min lockout; remember-me; sets `LastLoginDate`. Antiforgery form token; per-IP limit (10/window, 429). `Accept: application/json` gets `200 { redirect }` or `401 { error: Invalid/Locked/Expelled }`; plain form posts redirect to `/login?error=…`. Landing: local `returnUrl`, else `/events` (`MemberLanding`). |
| `returnUrl` | Local only (`UrlHelper.IsLocalUrl`): `/x` yes; `//x`, `/\x`, absolute or scheme URLs → `/events`. Read case-insensitively. |
| `POST /auth/logout` | Antiforgery form, signs out, `302 /`. |
| Expelled member with a live cookie | The cookie validator rejects the session on every request. |

Tests: `ReactLoginTests`, `AuthAntiforgeryTests`, `LoginRateLimitTests`, `PortalRouteTests`.

## Session

`GET /api/account/me` (anonymous allowed, `no-store`) returns `{ authenticated: false }` or the caller's own
`displayName` (nickname → first name → username), `fullName`, `avatarUrl`, category labels and `menu` (booleans from
`MemberMenuAccess`). Never email, phone, birth date, roles, IDs or other users. React calls it once per page load
(`getCurrentUser` in `portal/src/api.ts`); unknown or failed state falls back to the visitors' header.

## Member shell

`portal/src/MemberShell.tsx`. The header ends with `[inbox] [profile chip] [menu]`; the menu opens one right-hand
drawer at every width (native modal `<dialog>`, 48px rows, safe-area insets) and closes after every choice. The profile
chip opens `/profile`; the inbox opens `/messages` with the unread badge (`portal/src/unread.ts`). Perfil and Mensagens
are not in the menu.

- **Groups:** Membro (Atuações, Ensaios, Música, Galeria, Novidades, Loja) · Tuna (Membros, Hierarquia, Órgãos
  Sociais, Classificação, Hall of Fame, Naipes) · Gestão (Reuniões, Documentação, Instrumentos da Tuna, Questões,
  Logística, Tesouraria, Pedidos) · Owner (Utilizadores, Auditoria, Base de dados).
- **Who sees what** (`MemberMenuAccess`, links only): see [permissions](permissions.md). Removed modules (Jogos, Bets,
  MyTuno, Imagens, Conteúdo) are never listed. `MemberShellTests` pins the list, the gating and that every link opens a
  live route.
- **No jump on load:** `localStorage` `rtub-member-shell` remembers a signed-in last load, so the page opens in the shell
  while `/api/account/me` answers; the answer always wins; signing out clears it.

## Request

`/request` is the only public request form (`portal/src/Request.tsx`). One submission path:
`IPublicRequestService.SubmitAsync` (entity annotations, `RequestValidationService` date rules,
`RequestService.CreateRequestAsync` which pushes to every Admin and Owner, optional date range, then the RTUB email).
Only the API calls it. Management is `/requests` (`docs/react-requests-questions.md`).

`POST /api/public/requests` (anonymous, form-encoded, dates `yyyy-MM-dd`):

- **CSRF:** antiforgery enforced (400 without a valid token); the page first calls `GET /api/public/antiforgery-token`.
- **Rate limit:** policy `public-requests`, 5 per client IP per 60 min (`PublicRequestRateLimit` settings), fixed
  window; 429 `application/problem+json` with `Retry-After`. Partitioned on `RemoteIpAddress`: on Azure this needs
  forwarded headers enabled (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`), otherwise every visitor shares one budget.
- **Honeypot:** hidden `website` field; if filled, the answer looks like success and nothing is stored or sent.
- **Validation:** server-side; `400` validation problem with camelCase field keys. Body limit 32 KB.
- **Answers:** `200 {submitted:true}` · `400` field errors · `400` without errors = token refused · `429` · `500`
  (generic, logged, no exception details).

## Install guidance

The `#app` section on `/`: Android via Google Play (`ipb.pt.rtub.app`, a TWA of this site; package matches
`wwwroot/.well-known/assetlinks.json`); iPhone/iPad via Safari → Partilhar → Adicionar ao ecrã principal; Android
browser and desktop via the browser's install option when offered. No promise about notifications. The installed app
opens `start_url` and is a share target (`docs/pwa-practices.md` → *PWABuilder report card*).

## System announcement

The Owner's "Anúncio de sistema" (`/owner/tracing`, `POST /api/owner/tracing/announcement`, ≤500 characters).
`AnnouncementService` keeps only the latest one, in memory, for 5 minutes; `GET /api/announcement` (anonymous,
`no-store`) answers `{id, message}` or 204. The shell (`SystemAnnouncement` in `App.tsx`) asks on load, every 30 s while
visible and on coming back, and shows a dialog. The dismissed id is kept in `localStorage` (`rtub-announcement-seen`).
Limits on purpose: up to 30 s delay, no sound (autoplay rules), one app instance holds it (scale-out would need a shared
store).

## Follow-ups

- A public "Conhece a Tuna" page (`/about`): História and the full Hierarquia explanation. Today the home has one
  "Percurso" line in Junta-te a nós.

# React public portal (React track, tasks 001–002)

A bounded pilot: a React public portal served by the existing ASP.NET Core host, next to the Blazor
app, which keeps every other page. Not the start of a rewrite. Decision record:
`docs/architecture/adr/0002-react-public-portal-pilot.md`.

The React migration track numbers its own tasks from **001** (`feat/001/react-portal-pilot`),
separately from the repository's global unit sequence.

## Audience

The portal is **public**: an overview of RTUB, public performances, music, the current Órgãos
Sociais, the gallery, performance requests and, later, a public Feed. The **member area is only for
RTUB members**: there are no public accounts - the tuna creates each member's login. Portal copy and
layout follow that: public actions ("Pedir atuação") lead; the members link is quiet ("Membros" /
"A minha conta") and always opens `/portal/perfil`, which says the area is reserved before offering
the login. Never present login or registration as a public feature.

## Route ownership

Declared in one place, `src/RTUB.Web/Program.cs` (`MapFallbackToFile`, GET/HEAD only). Pinned by
`tests/RTUB.Integration.Tests/PortalRouteTests.cs`.

| Path | Owner | Notes |
| --- | --- | --- |
| `/portal` | **React** | Public home: hero, agenda, discography, gallery, joining, Órgãos Sociais, Pedidos + member login entry. |
| `/portal/privacidade` | **React** | Privacy Policy, verbatim copy of `Privacy.razor` (see below). |
| `/portal/perfil` | **React** (002) | Members-only notice with public shortcuts; signed out → Blazor login and back; signed in → who you are and the way into the member area. |
| `/portal/pedidos` | **React** (002) | Request preparation; submission hands off to the Blazor `/request` (see Request migration). |
| `GET /api/account/me` | ASP.NET (002) | `AccountController`: the caller's own session summary for React. |
| `/portal/assets/*` | static files | Content-hashed Vite output, normal static caching. |
| any other `/portal/*` | nobody | 404. POST/PUT to the two pages → 405. |
| everything else | **Blazor** | Unchanged, including the public pages the portal links to: `/`, `/login`, `/request`, `/events`, `/music`, `/gallery`, `/roles`, `/privacy`, `/profile`, sign-out, and every member/admin page. |

The shell `index.html` is served `Cache-Control: no-cache` so a deploy is picked up at once. It
carries the enforced CSP like every HTML document. Nothing in Blazor links to `/portal` yet; the
portal links out to Blazor with plain full-page links.

## Before any production release

`/portal` ships with whatever `dev` holds at the next `dev → master` release. The **"Pré-visualização"
banner and the illustrative agenda/gallery must be removed, hidden, put behind a feature flag, or
explicitly accepted by the owner before that release.** Until then the pilot is a DEV preview.

## Layout

| Path | What |
| --- | --- |
| `src/RTUB.Web/portal/` | Source: `index.html` (shell + launch splash), `src/*.tsx`, `styles.css`, `vite.config.ts`, `tsconfig.json`. Excluded from publish (`RTUB.csproj`). LF-only (`.gitattributes`). |
| `src/RTUB.Web/wwwroot/portal/` | **Committed** build output, published as-is. |
| `src/RTUB.Web/package.json` | `build:portal`, `check:portal`; React 19, `@fontsource-variable/fraunces` as devDependencies (bundled at build time). |

Stack choices, all deliberately minimal: React 19 + Vite 6 (already used for PixiJS) with esbuild's
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
  same Apple meta, same single service-worker registration (`/js/sw-register.js`). The service
  worker is untouched: `/portal` navigations are network-only with the offline fallback, the hashed
  assets stale-while-revalidate like any JS/CSS.
- **No business logic, no new API, no database change.** The only live call is the existing
  `GET /api/version` (footer). Login and Pedidos are links to the Blazor `/login` and `/request`,
  which keep the antiforgery, rate limiting, validation and email rules.

## Copy rule

The current site and app are the reference for structure, tone, page inventory and facts. **Their
sentences are never copied into the portal**: portal copy is original Portuguese.
`PortalCopyOriginalityTests` fails if any six-word run of portal copy (text nodes and multi-word
strings; proper names removed) appears in the Blazor pages, components, seeded labels or static
HTML. The Privacy Policy is the one exception: it is legal text, kept verbatim, and
`PortalPrivacyParityTests` fails if it drifts from `Privacy.razor`, which stays the legal source.

**Institution naming.** Present-day copy (current identity, location, CTAs) says **UPB** /
*Universidade Politécnica de Bragança*. Historical context (founding, old documents, songs, older
screenshots or sources) keeps **IPB**. Never mass-replace; bridge only when it helps ("fundada no
contexto do IPB, hoje UPB"). Legal text in the Privacy Policy is left as the policy states it.

Sources consulted for task 001: the public pages of the live site (home, `/music`, `/roles`), the
seeded labels (`SeedData.Labels.cs`), `Roles.razor`, `Request.razor` and `EventType`. The RGI and
the cancioneiro are member-only documents in R2 storage and were not accessed.

## Session and profile (task 002)

- **Signing in and out stay Blazor.** `/login` posts to the antiforgery-protected, rate-limited
  `POST /auth/login`, which already honours a local `returnUrl`; the portal sends visitors to
  `/login?returnUrl=/portal/perfil`. Sign-out is a POST from the Blazor layout with its own token;
  the portal only points to it.
- **`GET /api/account/me`** (anonymous-allowed, `Cache-Control: no-store`, GET only) returns
  `{ authenticated: false }` or the caller's own `displayName` (nickname → first name → username),
  `fullName`, `avatarUrl` and category labels (`StatusHelper.GetCategoryDisplay`). No email, phone,
  birth date, roles, IDs or other users. Expelled or deleted members arrive anonymous: the cookie
  validator rejects their session on every request. Pinned by `AccountEndpointTests`.
- React calls it once per page load (`getCurrentUser` in `portal/src/api.ts`); the header and menu
  show "Entrar" or "A minha conta", and `/portal/perfil` has loading, error (retry), signed-out and
  signed-in states. Unknown or failed session state falls back to "Entrar".
- Authorization is unchanged: nothing in React grants or hides access; every member page still
  enforces its own rules in Blazor.

## Request migration (task 002: prepared, not migrated)

What `/request` does today, all inside `Pages/Public/Request.razor`: fields name, email, phone,
event type (free text), preferred date, optional end date, location, message (limits in
`RTUB.Core.Entities.Request`); date rules (not in the past, end ≥ start) inline and duplicated in
`RequestValidationService`; `RequestService.CreateRequestAsync` saves and pushes a notification to
every Admin and Owner; `SetRequestDateRangeAsync`; then `EmailNotificationService` emails RTUB.
Anonymous, over the Blazor circuit, with **no rate limit, honeypot or other anti-spam**.

Why React does not submit yet: an anonymous JSON endpoint is far easier to script than a circuit,
and each submission fans out to every admin's phone and the RTUB inbox. Doing it safely needs a
rate-limit policy, but the only one today (login) has a global rejection handler with a
login-specific message - changing it touches login. And the orchestration lives in the component,
so a second entry point would duplicate it instead of sharing it. `/portal/pedidos` therefore
prepares the visitor and links to `/request`; `RequestSubmission` in `api.ts` fixes the contract.

Plan for the slice that migrates it:
1. Move the orchestration into one application service (validation + create + date range + email),
   and point the Blazor page at it with behaviour-preserving tests.
2. `POST /api/requests`, form-bound so the framework enforces antiforgery (as `/auth/login` does),
   token issued to the React page; a per-IP fixed-window policy with per-policy rejection text.
3. Honeypot field and server-side limits; `400` with field errors, `429` when throttled.
4. React form on `/portal/pedidos` with the same rules; `/request` stays until the owner retires it.
5. Tests: missing token → 400, throttled → 429, invalid → 400, success → one row, one email, one push fan-out.

## Launch splash

Black screen with the RTUB emblem, as static markup in `index.html`, so it covers the only real wait
(the bundle) and adds none. After React's first commit it lifts like a stage curtain (≈0.7 s) on the
first load of a session - every PWA launch is a new session - and is removed instantly on later
loads or under `prefers-reduced-motion`. A CSS failsafe uncovers the static fallback text after 10 s
if the app never mounts. On phones the revealed hero shows only a small badge, since the splash has
just shown the full emblem.

## Real vs illustrative content

| Content | Status |
| --- | --- |
| Agenda dates | **Illustrative**, labelled on the page. Real agenda: `/events`. |
| Gallery tiles | **Illustrative artwork**, labelled. Real photos: `/gallery`. |
| Discography (4 albums, years, track counts) | Fact, from `/music`. |
| Rehearsal days and place, instruments, request event types | Fact, rewritten in the portal's own words. |
| Órgãos Sociais bodies and positions | Fact (`Roles.razor`); holders deliberately not shown. |
| Social links, contact email | Fact. |
| Privacy Policy | Verbatim legal text (see Copy rule). |

## Validate locally

```bash
cd src/RTUB.Web
npm ci --ignore-scripts
npm run check:portal
npm run build:portal
git status --porcelain wwwroot/portal   # must be empty after a rebuild of committed source
```

Then `dotnet test` for `RTUB.Integration.Tests` (route ownership) and `RTUB.Web.Tests` (privacy
parity, copy originality, CSP/inline guards over the bundle).

## CI change still needed (not done here)

**CI • Build & Test** does not run Node. Add a job step, before the .NET tests:

```bash
cd src/RTUB.Web && npm ci --ignore-scripts && npm run check:portal && npm run build:portal \
  && git diff --exit-code -- wwwroot/portal && test -z "$(git status --porcelain -- wwwroot/portal)"
```

That fails a PR whose committed bundle does not match its source. Deploy workflows need no change:
they publish the committed `wwwroot/portal`.

## Install guidance (task 002)

The `#app` section on `/portal` (linked from the footer) shows four short routes. Android: the
Google Play listing `https://play.google.com/store/apps/details?id=ipb.pt.rtub.app`, taken from the
existing `PlayStorePrompt.razor`; its package matches `wwwroot/.well-known/assetlinks.json` (the
app is a TWA of this site). iPhone/iPad: Safari → Partilhar → Adicionar ao ecrã principal. Android
browser: Chrome menu → Instalar app / Adicionar ao ecrã principal, when offered. Computer: the
install icon in Chrome/Edge, when offered. No promise about notifications. Manifest, service worker
and TWA config are untouched; an installed app opens the manifest's `start_url` (`/`).

## Feed (planning only - nothing built)

A future public Feed: posts/topics published by Admins, readable by anyone and shareable like
public social posts - announcements, event recaps, photos, news. Not in the UI yet; no table, API or
admin screen exists. To decide before building: moderation and edit history, visibility (public vs
members-only posts), attachments and image storage (R2), sharing metadata (OpenGraph/SEO, stable
URLs), the admin creation flow, and whether posts notify members.

## Next recommended slice

Task 003: migrate request submission per the plan above. Then Órgãos Sociais (read-only public
API), before Gallery, Music and Events.

## Next steps (outside this pilot)

- A read-only public API (upcoming public events, public albums, gallery highlights) to replace the
  illustrative content - built on the existing services, not duplicated in React.
- A client router and an explicit decision on `/` ownership and the PWA `start_url` before the
  portal replaces the Blazor home.
- Authenticated pages need a React-usable auth model; today's authorization services depend on
  Blazor `AuthenticationState`.

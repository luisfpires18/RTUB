# React public portal (React track, tasks 001–003)

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
"A minha conta") and always opens `/portal/profile`, which says the area is reserved before offering
the login. Never present login or registration as a public feature.

## Naming

User-facing text is Portuguese. Code, routes, URL fragments, files, components, CSS classes, APIs,
DTOs and branches are English: "Novidades" on screen, `/portal/news` in the URL; "Pedidos" on
screen, `/portal/request` and `#request`. (Task 002 renamed the earlier `/portal/privacidade`,
`/portal/perfil` and `/portal/pedidos`; none had reached production.)

## Home page and navigation

Home order: hero and quick facts → "Quem somos" (short, facts only) → Atuações (with the FITAB
highlight) → Música → Galeria → Junta-te a nós → Órgãos Sociais → Pedidos + member area → install
the app → a small "Novidades · Em breve" line. The top bar stays short: **Atuações, Música, Galeria,
Órgãos Sociais**, the **"Pedir atuação"** call to action, and the quiet members link. Junta-te,
FITAB, Novidades and the app install are deliberately not in the top bar (footer or home anchors).

Kept off the home page on purpose: the full Hierarquia (Leitão → Caloiro → Tuno → Magister) card
grid and the long "Sobre nós" text of the current site - too internal for a public front page. The
member categories may later get a dedicated public "Conhece a Tuna" page or live in the member area.

## Route ownership

Declared in one place, `src/RTUB.Web/Program.cs` (`MapFallbackToFile`, GET/HEAD only). Pinned by
`tests/RTUB.Integration.Tests/PortalRouteTests.cs`.

| Path | Owner | Notes |
| --- | --- | --- |
| `/portal` | **React** | Public home: hero, agenda, discography, gallery, joining, Órgãos Sociais, Pedidos + member login entry. |
| `/portal/privacy` | **React** | Privacy Policy, verbatim copy of `Privacy.razor` (see below). |
| `/portal/profile` | **React** (002) | Members-only notice with public shortcuts; signed out → Blazor login and back; signed in → who you are and the way into the member area. |
| `/portal/request` | **React** (003) | The public performance request form (see Request). |
| `GET /api/account/me` | ASP.NET (002) | `AccountController`: the caller's own session summary for React. |
| `GET /api/public/antiforgery-token`, `POST /api/public/requests` | ASP.NET (003) | `Endpoints/PublicRequestEndpoints.cs`: request submission for React. |
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
  `/login?returnUrl=/portal/profile`. Sign-out is a POST from the Blazor layout with its own token;
  the portal only points to it.
- **`GET /api/account/me`** (anonymous-allowed, `Cache-Control: no-store`, GET only) returns
  `{ authenticated: false }` or the caller's own `displayName` (nickname → first name → username),
  `fullName`, `avatarUrl` and category labels (`StatusHelper.GetCategoryDisplay`). No email, phone,
  birth date, roles, IDs or other users. Expelled or deleted members arrive anonymous: the cookie
  validator rejects their session on every request. Pinned by `AccountEndpointTests`.
- React calls it once per page load (`getCurrentUser` in `portal/src/api.ts`); the header and menu
  show "Entrar" or "A minha conta", and `/portal/profile` has loading, error (retry), signed-out and
  signed-in states. Unknown or failed session state falls back to "Entrar".
- Authorization is unchanged: nothing in React grants or hides access; every member page still
  enforces its own rules in Blazor.

## Request (task 003: migrated; Blazor `/request` kept)

One submission path: **`IPublicRequestService.SubmitAsync`** (Application layer), extracted verbatim
from the old inline handler in `Pages/Public/Request.razor`: the `Request` entity's annotations, the
date rules of `RequestValidationService` (same messages), `RequestService.CreateRequestAsync` (which
still pushes to every Admin and Owner), `SetRequestDateRangeAsync` when an end date is given, then
the RTUB email. Both the Blazor page (unchanged markup) and the API call it.

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
| IsDateRange | bool | toggle | toggle | stored `true` only through `SetDateRange`; Blazor stores an end date typed before switching the toggle off (kept as is) |
| Location | text, required, ≤200 | yes | yes | |
| Message | text **NOT NULL**, ≤2000 | yes | yes | empty is stored as `""` |
| Status | int (Pending = 0) | - | - | internal, admin workflow |
| CreatedAt/By, UpdatedAt/By | audit | - | - | internal; `CreatedBy` is null for public requests |

**No migration.** Every field the form needs already exists with the right limits; the React DTO
maps onto the existing entity. The real database was only read (schema, row count, max lengths): its
3 requests fit every limit, and all dated migrations in the repo are applied except the two known
never-run ones (STATE.md).

Still Blazor: `/request` itself (kept, same behaviour, now on the shared service), the admin
`/requests` management page, and turning a request into an event.

## Install guidance (task 002)

The `#app` section on `/portal` (linked from the footer) shows four short routes. Android: the
Google Play listing `https://play.google.com/store/apps/details?id=ipb.pt.rtub.app`, taken from the
existing `PlayStorePrompt.razor`; its package matches `wwwroot/.well-known/assetlinks.json` (the
app is a TWA of this site). iPhone/iPad: Safari → Partilhar → Adicionar ao ecrã principal. Android
browser: Chrome menu → Instalar app / Adicionar ao ecrã principal, when offered. Computer: the
install icon in Chrome/Edge, when offered. No promise about notifications. Manifest, service worker
and TWA config are untouched; an installed app opens the manifest's `start_url` (`/`).

## News / "Novidades" (planning only - nothing built)

Public label "Novidades"; code name **News** ("Feed" is the concept). Future routes:
`/portal/news` (list) and `/portal/news/{slug}` (one post, with its own shareable URL). Posts are
created by Admins and readable by anyone: announcements, event recaps, photos, news and relevant
topics, shareable like public Facebook/WhatsApp posts. To decide before building: moderation and edit
history, visibility (public vs members-only), images and attachments (R2), sharing metadata
(OpenGraph, SEO), notifications, and the admin publishing flow. Today there is only a small "Em
breve" line at the bottom of the home page: no route, table, migration, API or admin screen, and no
footer link until a route exists.

## Next recommended slice

Task 004: Órgãos Sociais from a read-only public API (current mandate, no personal contact data),
then Gallery, Music and Events. Retiring Blazor `/request` (a redirect to `/portal/request`) is a
separate, owner-approved step once the React form has run on DEV.

## Next steps (outside this pilot)

- A read-only public API (upcoming public events, public albums, gallery highlights) to replace the
  illustrative content - built on the existing services, not duplicated in React.
- A client router and an explicit decision on `/` ownership and the PWA `start_url` before the
  portal replaces the Blazor home.
- Authenticated pages need a React-usable auth model; today's authorization services depend on
  Blazor `AuthenticationState`.

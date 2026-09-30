# React public-portal pilot (React track, task 001)

A bounded pilot: a React public portal served by the existing ASP.NET Core host, next to the Blazor
app, which keeps every other page. Not the start of a rewrite. Decision record:
`docs/architecture/adr/0002-react-public-portal-pilot.md`.

The React migration track numbers its own tasks from **001** (`feat/001/react-portal-pilot`),
separately from the repository's global unit sequence.

## Route ownership

Declared in one place, `src/RTUB.Web/Program.cs` (`MapFallbackToFile`, GET/HEAD only). Pinned by
`tests/RTUB.Integration.Tests/PortalRouteTests.cs`.

| Path | Owner | Notes |
| --- | --- | --- |
| `/portal` | **React** | Public home: hero, agenda, discography, gallery, joining, Órgãos Sociais, Pedidos + member login entry. |
| `/portal/privacidade` | **React** | Privacy Policy, verbatim copy of `Privacy.razor` (see below). |
| `/portal/assets/*` | static files | Content-hashed Vite output, normal static caching. |
| any other `/portal/*` | nobody | 404. POST/PUT to the two pages → 405. |
| everything else | **Blazor** | Unchanged, including the public pages the portal links to: `/`, `/login`, `/request`, `/events`, `/music`, `/gallery`, `/roles`, `/privacy`, and every member/admin page. |

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

## Next steps (outside this pilot)

- A read-only public API (upcoming public events, public albums, gallery highlights) to replace the
  illustrative content - built on the existing services, not duplicated in React.
- A client router and an explicit decision on `/` ownership and the PWA `start_url` before the
  portal replaces the Blazor home.
- Authenticated pages need a React-usable auth model; today's authorization services depend on
  Blazor `AuthenticationState`.

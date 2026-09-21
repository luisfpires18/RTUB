# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-21_

## Phase
Modernization unit **024 (remove inline-style CSP blockers) - COMPLETE, uncommitted, awaiting
owner review.** Unit 023 is merged to `dev` at `00e8009f`. No CSP header enabled; that is unit 025.

## Branch
`fix/024/remove-inline-styles`, branched from `dev` (clean, in sync with `origin/dev` at
`00e8009f`). Uncommitted - no commit authorized.
`chore/001`-`chore/011`, `fix/012`-`fix/014`, `chore/015`, `fix/016`-`fix/018`, `perf/019`,
`fix/020`-`fix/023` still present; delete when convenient.

## Owner decision (2026-09-21)
**Password-policy hardening is SKIPPED**, by instruction. Identity's password requirements were
not read for change and not touched by 021. It remains available as a future unit.

## Last completed step
**Unit 024 - every inline `<style>` block and `style="..."` attribute removed from
browser-served application markup. Final counts: 0 and 0.**

### The measurement that shaped the unit
The brief assumed that moving `style="width:42%"` into `element.style.width = "42%"` does not help,
because JS writing the style attribute is still inline-style behaviour for CSP. **That was measured
against a real `style-src 'self'` policy and it is false.** Chromium results:

| Mechanism | Under `style-src 'self'` |
| --- | --- |
| markup `style="..."` | **BLOCKED** (`style-src-attr`) |
| `setAttribute('style', ...)` | **BLOCKED** (`style-src-attr`) |
| `<style>` element, authored or injected | **BLOCKED** (`style-src-elem`) |
| `innerHTML` string containing `style=` | **BLOCKED** (`style-src-attr`) |
| `el.style.prop = ...` | **ALLOWED**, no violation |
| `el.style.setProperty('--x', ...)` | **ALLOWED**, no violation |
| `el.style.cssText = ...` | **ALLOWED**, no violation |
| constructed `CSSStyleSheet` + `adoptedStyleSheets` | **ALLOWED**, no violation |

CSP governs the style **attribute** and `<style>` **elements**; it does not govern the CSSOM. That
distinction is what made the dynamic cases solvable without `'unsafe-inline'`, and it is why the
existing CSSOM writes in application JS were left alone rather than rewritten.

### Counts before / after
| Thing | Before 024 | After 024 |
| --- | --- | --- |
| Inline `<style>` blocks in browser-served markup | 12 | **0** |
| `style="..."` attributes in browser-served markup | 213 | **0** |
| `style=` inside JS-built HTML strings | 4 | **0** |
| `style.cssText` / injected `<style>` in app JS | 2 | **0** |
| `setAttribute('style')`, `insertRule` in app JS | 0 | 0 |
| Inline styles in **email** templates (not CSP-governed) | 177 attrs + 1 block | **unchanged, by design** |

### Style audit - classification
Everything the pattern sweep found, classified before any edit:

| Class | Count | Disposition |
| --- | --- | --- |
| **A. Static** | 170 attrs / 22 files | -> utility or composite classes |
| **B. Dynamic but finite-state** | 24 attrs | -> conditional / lookup classes |
| **C. Dynamic value-driven** | 10 attrs | -> validated `data-*` + CSSOM custom property |
| **D. Third-party generated** | 0 owned | Bootstrap/Leaflet runtime CSSOM writes - not CSP-governed |
| **E. False positive** | 177 email attrs + 20 app CSSOM writes | left alone, see below |

**A - static (170).** 109 distinct values, decomposing to 98 classes: atoms where the value is a
single declaration (`u-fs-70`, `u-c-gold`, `u-w-32`, `cursor-pointer`, ...) and one composite class
per distinct multi-declaration set (`u-tile-gold`, `u-grad-purple`, `u-btn-gold-xs`, ...).
Generated into `wwwroot/css/9-overrides/inline-style-utilities.css`. The transform was mechanical
and total: every declaration had to be present in the map or the run aborted, so nothing was
silently dropped.

**B - finite state (24).** `RoleBadge` (3 role branches), `Bets` option outcome (3), `InfoSection`
cancellation panel, `EmailRecipientsPreview` `MaxHeight` (3 used values), MyTuno cursor toggles (5),
MyTuno active forge/upgrades tab (4), the rare-set glow, plus the two **palette lookups**:
`RankHelper.GetRankColorClass` (10 tiers) and `BiomeDisplayHelper.GetBiomeColorClass` /
`GetSurviveBiomeColorClass` (21 + 12 biomes). The palette classes set `--rank-c` / `--bc`, which is
what the existing biome CSS already consumed - so the accent icon, badge and border now inherit one
value instead of repeating an interpolated hex four times.

**C - genuinely dynamic (10).** 8 progress-bar widths (`RankCard`, `Members`, `Profile`, and 5 in
`MyTunoHome`) and 2 Logistics label colours the user picks from `<input type="color">`. These cannot
be a fixed class and cannot be rounded into one without either a class explosion or a visual
regression. They are the only case that needed new machinery: see *dynamic-style bridge*.

**E - false positives, deliberately untouched.**
- **Email templates: 177 `style="..."` + 1 `<style>` across 14 `.cshtml`.** `EmailTemplateService`
  renders these to an HTML **string** delivered over SMTP. They are never an HTTP response from this
  app, so no CSP applies - and mail clients strip `<style>`, which makes inline styles a hard
  requirement. Changing them would break email rendering for zero security gain.
- **20 CSSOM writes in app JS** (`clipboardCopy.js` 11, `messageScroller.js` 6, `tomatoThrower.js`,
  `pixiSurviveMode.js`, `pixi/survive/InputManager.ts`). Measured above as **not** CSP-governed.

### `<style>` blocks - where the 12 went
11 `.razor` blocks became files in the existing `wwwroot/css` hierarchy, plus `offline.html`:

| Source | Destination |
| --- | --- |
| `MeetingCard.razor` (354 lines) | `css/3-components/meeting-card.css` |
| `QuestionCard.razor` (211) | `css/3-components/question-card.css` |
| `MonthYearPicker.razor` (58) | `css/3-components/month-year-picker.css` |
| `Meetings.razor` (72) | `css/4-pages/meetings.css` |
| `NaipesConfig.razor` (98) | `css/4-pages/naipes-config.css` |
| `PassaroMaluco.razor` (131) | `css/4-pages/passaro-maluco.css` |
| `Questions.razor` (57) | `css/4-pages/questions.css` |
| `Report.razor` (5) | `css/4-pages/report.css` |
| `AllCharacters.razor` (115) | `css/4-pages/my-tuno-all-characters.css` |
| `WeaponDrinkConfig.razor` (151) | `css/4-pages/weapon-drink-config.css` |
| `offline.html` (61) | `css/offline.css` (standalone - offline.html does not load site.css) |

**Global files, not `.razor.css`, and that is deliberate.** The repo uses both conventions (88
`.razor.css` files *and* a 63-file `wwwroot/css` hierarchy imported by `site.css`). A `<style>` block
in `.razor` markup is emitted verbatim and is **global**; moving it to `.razor.css` would scope it to
that component's own elements and silently stop it applying to anything a child component renders.
Global files preserve the cascade exactly, with no per-file judgement call to get wrong. `@@media`
was unescaped to `@media` on the way out.

**Cascade position preserved.** A body `<style>` block previously beat every stylesheet at equal
specificity because it came last in document order. The extracted files are therefore imported
**last** in `site.css`, after everything else, so ties resolve the same way - without `!important`.
Scoped rules in `RTUB.styles.css` are unaffected: their `[b-xxxxx]` attribute already gives them
higher specificity regardless of order.

**One block was deleted, not moved.** `Profile.razor` carried
`:global(html, body) { overflow-x: hidden; }` inside a plain `<style>` element. `:global()` is
Blazor scoped-CSS-only syntax; in a real stylesheet it is an invalid selector, so the rule has
**never applied**. Deleting an inert rule preserves behaviour exactly. Making it work would have
been a visual change and was not taken - see *Deferred*.

### Dynamic-style bridge - the only new machinery
`wwwroot/js/dynamicStyle.js` (~95 lines) carries the 10 category-C values. Markup emits a validated
`data-*` attribute; the script applies it as a CSS custom property through `el.style.setProperty`,
which the measurement above shows CSP does not block. A `MutationObserver` with an `attributeFilter`,
coalesced to one sweep per animation frame, reapplies after Blazor re-renders.

It is **deliberately not a generic "apply this CSS" sink** - that would hand back exactly the
capability `style-src` removes. Each attribute accepts one narrow shape and nothing else:
- `data-fill-pct` -> `^\d{1,3}(\.\d+)?$`, clamped to 0-100, applied as `--fill-pct`
- `data-swatch` -> `^#[0-9a-fA-F]{6}$`, applied as `--swatch`

Anything else is ignored. Verified in the browser: `data-swatch="red; position:fixed"` set no
property, left the element at its CSS default colour, and left `position: static`.

Cost: a bar renders at its CSS fallback (`width: var(--fill-pct, 0%)`) for one frame before the
script applies the real value. For a progress bar with an existing transition this is invisible; it
is recorded because it is a real, if small, behavioural difference.

### Service-worker update toast - classes, no injected stylesheet
`sw-register.js`'s `showUpdateToast` built its markup with `innerHTML` carrying 4 `style="..."`
attributes, assigned `toast.style.cssText`, and **injected a `<style>` element** for the slide-up
keyframes. It now builds DOM nodes with `className` only; all of it, keyframes included, lives in
`css/3-components/sw-update-toast.css`. The `cssText` assignment was not itself a CSP violation
(CSSOM), but the innerHTML attributes and the injected `<style>` were.

Verified in the browser by constructing the toast: `position: fixed`, `z-index: 999999`, the `135deg`
gradient, `14px 20px` padding, `2px solid #e94560` top border, flex row with `10px` gap, `8px` button
radii, and `animation: rtub-toast-slide-up 0.3s ease-out` resolving against the stylesheet's
`@keyframes`. No inline style attribute on the toast and zero `<style>` elements in the document.
Appearance and behaviour unchanged.

### Leaflet map markers
`memberMap.js` built its `divIcon` HTML with 2 inline `style` attributes plus a third on the
"+N mais membros" row - markup strings, so genuinely blocked. Moved to `.marker-pin`, `.marker-count`
and `.popup-member-item--more` in the existing `css/4-pages/member-map.css`. The vendored
Leaflet / Bootstrap / Cropper / Pixi files were **not** touched.

### Offline page - proven with the origin actually down
`offline.html` now links `/css/offline.css`, which was added to `STATIC_ASSETS` in
`service-worker.js`. The shared script/style fetch branch already had 023's
`.catch(() => cached || caches.match(request))` cross-cache fallback, so the precached copy is
reachable; no further service-worker change was needed and `CACHE_VERSION` was **not** bumped.

**Proven end-to-end in headless Chromium**, not reasoned about: register the worker, confirm
`/css/offline.css` sits in `rtub-static-rtub-v2.6.0` **only** (the `DYNAMIC_CACHE` sweep found
nothing to delete), kill the server, confirm the port is closed with an out-of-band socket check,
then load the page. Result: stylesheet served **200, 1551 bytes**, gradient rule present; the page
renders with the `135deg` gradient background, white text, flex centering, `8px` / `12px 30px`
button, `120px` icon and the heading "Sem Conexão" - **0 `<style>` elements, 0 inline style
attributes, 1 linked stylesheet, 9 rules loaded.** 023's `offline.js` work is intact and still
precached.

An in-page "is the origin up?" probe is useless here and was discarded: the service worker answers
from cache with **status 200**, so the page cannot tell a live server from a cached response. The
out-of-band socket check is what makes this result trustworthy.

### Static policy test
`tests/RTUB.Web.Tests/Security/InlineStylePolicyTests.cs`, **10 aggregated Facts** following 023's
pattern - repository-wide sweeps that collect every violation and report them in a single failure,
rather than hundreds of per-file cases. Pinned:
1. no `style="..."` in browser-served markup
2. no `<style>` elements in browser-served markup
3. no `style.cssText`, `setAttribute('style')` or `createElement('style')` in app JS
4. no `style=` inside JS-built markup strings
5. email templates are the **only** excluded markup, and the exclusion is still load-bearing
6. `offline.html` links its external stylesheet; the service worker precaches it
7. the update toast is styled by classes from the external stylesheet, keyframes included
8. the dynamic bridge validates its inputs and never uses `cssText`
9. the migrated values (`#007bff`, the three `max-height`s) survive in the stylesheet

`el.style.prop = ...` and `setProperty` are **deliberately not banned** - they are not CSP violations
and `dynamicStyle.js` depends on them. `cssText` is banned despite being legal: it has no remaining
use and is an inline-style-shaped bulk write worth keeping shut.

### Two tests were passing for the wrong reason
Moving CSS out of markup exposed **two false-positive assertions** that were matching text inside the
component's own inline `<style>` block rather than rendered markup:
- `MeetingCardTests.MeetingCard_ShowsHojeBadge_WhenMeetingIsToday` asserted
  `Contain("meeting-today-badge")`. **No element has ever carried that class** - it exists only as a
  CSS rule. The "HOJE" text it also asserts is real and comes from `<DateBadge>`; that assertion is
  kept, the bogus one removed. `.meeting-today-badge` is now **orphaned CSS** (see *Deferred*).
- `MonthYearPickerTests.MonthYearPicker_HidesPlaceholderOption` asserted
  `Contain(".month-year-picker option[disabled]")` and `Contain("display: none")` - both pure CSS
  selector text. Rewritten to assert the component renders the hook the rule targets (a disabled
  placeholder `<option>` inside `.month-year-picker`) and that the stylesheet still carries the rule.

Three further tests needed updating because the markup contract genuinely changed: `RoleBadgeTests`
(x2) now assert `role-badge--member` instead of `#007bff`, and `EmailRecipientsPreviewTests` asserts
`subscriber-list--h400` instead of `max-height: 400px`. The values themselves are pinned in
`InlineStylePolicyTests`, so that coverage is not lost.

### Third-party CSP style constraint: none found
No vendored library requires inline styles under a strict `style-src`. Bootstrap's offcanvas and
modal transforms, Leaflet's positioning and Cropper's sizing all write through the **CSSOM**
(`el.style.transform = ...`), which the measurement above confirms CSP does not govern. `insertRule`
and `setAttribute('style')` appear nowhere in the vendored bundles the app loads. **`style-src` has
no third-party exception.**

### Other behaviour notes, all preserving current behaviour
- `InstrumentCounter.GetHeaderStyle()` returned `cursor: pointer` while `GetHeaderClass()` already
  returned a `cursor-pointer` class that **was never defined anywhere**. The method is deleted and
  the class is now defined, so the component keeps working and the duplication is gone.
- `InfoSection`'s public `Style` parameter is **removed**. Both callers passed the identical
  cancellation-reason panel, now `modal-info-section--danger`. Leaving the parameter would have left
  an inline-style escape hatch the policy test could not ban.
- `MyTunoHome`'s per-checkpoint `style="--bc: ..."` on `.biome-cp` was **redundant** - the button is
  inside `.biome-card` and custom properties inherit. Removed, not reimplemented.
- The rare-tab badge's `animation: rare-tab-pulse` **never ran and still does not.** The keyframes
  are defined only in `MyTunoHome.razor.css`, which Blazor rewrites to `rare-tab-pulse-b-t56smwc8j2`;
  the old inline attribute referenced the un-rewritten name, and so does the new class. Identical
  behaviour, deliberately not "fixed" - see *Deferred*.

### CSP inventory - the style side is now clear (plan for 025)
**Blocker 1 - `eval`: CLEARED by 022.** `'unsafe-eval'` not needed.

**Blocker 2 - inline `<script>` and inline `on*` handlers: CLEARED by 023.** `script-src` needs no
`'unsafe-inline'`.

**Blocker 3 - inline styles: CLEARED by 024.** 12 `<style>` blocks and 213 `style="..."` attributes
gone; JS-built markup and injected stylesheets gone. No nonce, no hash, no `'unsafe-hashes'`.
**`style-src` is now `'self'` plus the two pinned CDN origins below.** Note `'unsafe-hashes'` would
NOT have been a way out even if attributes had remained: hashes do not apply to style attributes
without it, and it re-opens the whole class.

**Blocker 4 - the R2 origin is runtime configuration, not a constant. STILL OPEN, and now the only
one.** `img-src`, `media-src` and `frame-src` need the Cloudflare R2 public origin from
`Cloudflare:R2:PublicUrl` (e.g. `https://pub-xxx.r2.dev`), **stored absolute in the database**. The
policy string must be built from `IConfiguration` at startup, not written as a literal.

**Not blockers - the external origins, verified in 021 by reading what the browser loads:**
- `script-src`: `https://cdnjs.cloudflare.com` (cropper.js), `https://unpkg.com` (leaflet, SRI
  pinned), `https://cdn.jsdelivr.net` (pixi.js)
- `style-src`: `https://cdnjs.cloudflare.com` (cropper.css), `https://unpkg.com` (leaflet.css, SRI
  pinned)
- `img-src`: `'self' data:` plus `https://*.basemaps.cartocdn.com` (`wwwroot/js/memberMap.js` tile
  layer) plus the R2 origin
- `connect-src`: `'self'` - the `_blazor` WebSocket is **same-origin**, and CSP3 `'self'` matches
  `wss:` to the same host. The service worker fetches **nothing** cross-origin.
- `frame-src`: the R2 origin only (the two PDF viewers)
- `font-src`: `'self'` - `https://fonts.googleapis.com` is a **dead** `preconnect`/`dns-prefetch` in
  `App.razor` with no matching stylesheet and no `@font-face`. Do not add it to a policy; delete the
  hint instead (carried in *Deferred*).
- YouTube / Spotify / Instagram / Facebook appear only as `<a href target="_blank">` **navigations**,
  never embeds - so they need **no** directive.

The four security headers shipped by 021 (`X-Content-Type-Options: nosniff`,
`Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY`,
`Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=()`) and the HSTS decision
(already live at `max-age=2592000`) are unchanged by 024.

**`Program.cs` comment updated.** It now records that 022 + 023 + 024 cleared both the script-side
and the style-side blockers, and that CSP itself is unit 025. Comment only - the security-header
middleware is byte-identical and still emits **no** CSP.

### Browser validation
Headless Chromium plus the in-app pane, against a local Development build.
- Homepage and `/login`, desktop (1280) and mobile (375): render correctly, **no horizontal
  overflow**, and **0 inline style attributes / 0 `<style>` elements in the live DOM** on every one.
- All **14** new or changed stylesheets fetch **200**; no 404s. Computed styles confirm each migrated
  value resolves: `u-fs-70` -> `11.2px`, `u-c-gold` -> `rgb(255,215,0)`, `role-badge--member` ->
  `rgb(0,123,255)`, `rank-c-gold` -> `--rank-c: #ffd700`, `biome-c-forest` -> `--bc: #4caf50`.
- Dynamic bridge: `data-fill-pct="42.5"` in a 200px container -> **85px**; `data-swatch="#8a2be2"` ->
  `rgb(138,43,226)`; `999` clamped to `100%`; the injection attempt rejected.
- Update toast and offline page: see their sections above.
- Console: the only errors are pre-existing `401`s from auth-gated API calls on the anonymous
  homepage. **None are style- or CSS-related.**

**Service workers do not register in the in-app browser pane** - a one-line `install`-only worker
fails there too, so it is an environment limitation, not a regression. Every service-worker result
above therefore comes from headless Chromium, where they work.

### Frontend build
**None required.** `wwwroot/css` and `wwwroot/js` are plain hand-written assets served directly and
versioned by `<VersionedAsset>`; no npm/bundler step governs them. The only build that matters is
`dotnet build`, which regenerates the scoped-CSS bundle - unchanged by this unit, since no
`.razor.css` file was touched.

## Deployment requirement — `AdminUser__Password` on a fresh database (unit 016)

**No immediate Azure action is required for this deploy.** Production `rtub` has an existing
database with users, so `InitializeAsync` returns before the bootstrap check is ever evaluated.
Deploying unit 016 to current production changes nothing at startup, and the existing Owner
account is unaffected — its password is whatever it was set to, not the removed default.

**Any fresh/empty database will now refuse to start without it.** Before the future Azure **dev**
environment (or any new App Service, container, or local database created from scratch) is first
started, `AdminUser__Password` must be supplied as an App Service application setting or
equivalent secure configuration source, with a real value — not a placeholder. Without it,
seeding throws and no privileged account is created. Like
`ASPNETCORE_FORWARDEDHEADERS_ENABLED` below, this is portal/CLI configuration, is **not in the
repo**, is not applied by a code redeploy, and nothing in CI will warn if it is missing.

**`SeedData__MemberPassword`** is needed only by a developer who flips `isEmptyDb` to `false` for a
full local seed. It is never required in Azure unless that switch is used there.

## Deployment requirement — Azure forwarded headers (CONFIRMED 2026-09-21, no longer blocking)

**Resolved.** `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` **is set** on App Service `rtub`
(resource group `rtub_group`), verified by the owner via Azure CLI on **2026-09-21**. Unit 014's
login rate limiter therefore partitions on the real client address in production, as designed.

**Production `rtub` is Azure App Service on Linux.** `curl -I https://rtub.azurewebsites.net/health`
returns `Server: Kestrel` — no IIS layer, so there is no `UseIISIntegration` auto-wiring of
forwarded headers. The setting above is Microsoft's documented switch for App Service Linux /
containers: the host wires `ForwardedHeadersMiddleware` itself, ahead of the app pipeline, with
cloud-appropriate settings. Nothing is in RTUB's own code for it — deliberately: no
`UseForwardedHeaders` call, no `ForwardedHeadersOptions`, no clearing of
`KnownProxies`/`KnownNetworks`, and no manual `X-Forwarded-For` parsing.

It is portal/CLI configuration and is **not in the repo** — `.github/workflows/ci.yml` only
publishes and deploys, and sets no app settings. So it is not reproduced by a redeploy of code
alone, and **a new App Service (including the future Azure dev environment) needs it set again.**
Nothing in CI will warn if it is missing.

What `RemoteIpAddress` is, per environment:
- **Local / `dotnet run`** — the real client address; correct with no extra configuration.
- **Integration tests (`TestServer`)** — `null` for every request (no transport), so all callers
  share the `"unknown"` partition. `RemoteIpTestStartupFilter` (test host only) works around this
  by setting the address from an `X-Test-Remote-Ip` header; see *Previous validation (unit 014)*.
- **Azure App Service (current prod, Linux/Kestrel)** — **the real client address**, as of the
  2026-09-21 confirmation above.
- **Future Azure dev environment** — platform front end until the same setting is applied there.

### Consequence now live — `UseHttpsRedirection`
Turning the setting on makes `Request.IsHttps` true behind the proxy. The skip of
`UseHttpsRedirection` outside Development at `Program.cs:325` (comment: "HTTPS is handled at the
load balancer level") was the workaround for exactly the case that no longer applies. The skip is
now redundant rather than load-bearing. Still **not changed** — it is its own decision, and
removing it is a behavior change to the production request pipeline. Carried in *Deferred* below.

## Current task
None active. Unit 024 is complete and awaiting owner review.

## Next unit
**025 - enable CSP.** Both the script-side and the style-side blockers are now cleared, so the
policy can be written without `'unsafe-eval'`, `'unsafe-inline'`, `'unsafe-hashes'`, a nonce or a
hash. Remaining work is the policy string itself, built from the directive inventory above, with
`img-src` / `media-src` / `frame-src` taking the Cloudflare R2 public origin from
`Cloudflare:R2:PublicUrl` at startup rather than a literal - that is Blocker 4 and the only open
one. Ship it `Content-Security-Policy-Report-Only` first if the owner wants a safety margin, then a
browser smoke run across the authenticated pages.

Also still available, deliberately not taken: **password-policy review / hardening** (Identity is
`RequiredLength = 4` with every complexity rule off, `AddIdentityServices`,
`ServiceCollectionExtensions.cs`) - the owner skipped it in 021.
Still queued, not security: **Microsoft 10.0.11 -> 10.0.12 servicing train** across `src/` + tests,
which also unblocks `MockQueryable.Moq 10.0.12`.

## Blockers
**None.**

**Owner action, not a blocker:** the historical GitGuardian incidents stay historical. The
literals remain in old commits, and unit 015 deliberately did **not** rewrite git history to clear
them. Mark those incidents "false positive / test credential" in GitGuardian by hand. 015 only
stops *future* commits from raising new ones. No GitGuardian ignore comment was added either.

## Deferred / owner decisions

### Security — raised by 015, deliberately not changed
- ~~**Two hardcoded production password defaults in `src/`**~~ — **fixed by unit 016.** Both now
  fail closed; see *Last completed step* above.
- ~~**`ResetDevDataAsync` runs in EVERY environment except Production**~~ - **fixed by unit 017.**
  Now opt-in (`DevelopmentDataReset:Enabled`), local-Development-only, disabled by default, no
  hardcoded hash, validated before any mutation. Staging and Production can no longer run it at
  all. See *Last completed step*.
- ~~"the bulk member seed is dead code"~~ — **that earlier claim was wrong and is retracted.** It
  is an intentional manual full-seed developer path (`isEmptyDb`), now documented in place and
  covered by tests. Nothing to clean up.
- **Old commits still contain the removed literals.** History was not rewritten, by instruction.
- The `src/` scan was run for completeness only; **no production file was touched by 015** and
  production `appsettings*.json` contain no credential keys at all.

### Security — raised by 014, deliberately not changed
- **`RateLimiterOptions.OnRejected` is global, not per-policy.** Today only the `login` policy
  exists, so the Portuguese rejection body can only be reached by a login rejection. **A second
  policy added later must either share that message or move the callback onto its own policy.**
- **A 429 on a browser form POST renders as a bare text page**, not the styled `/login?error=...`
  page the endpoint's other failures use. Redirecting instead would mean answering 302, which
  contradicts the required 429. Left as-is; a status-code page for 429 is a UX decision.
- **Existing login tests share the `"unknown"` partition.** `AuthAntiforgeryTests` (5 login POSTs)
  and `CookieApiCsrfTests` (4) are under the limit of 10 and each class gets its own factory, so
  they are safe today. Adding a sixth login POST to one of those classes would start hitting 429 —
  use the `X-Test-Remote-Ip` header for a distinct partition if that happens.
- **`UseHttpsRedirection` is skipped outside Development** (`Program.cs:325`). **Now actionable:**
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is confirmed on as of 2026-09-21, so `Request.IsHttps` is
  true behind the proxy and the skip no longer works around anything. Removing it is a production
  request-pipeline behavior change, so it is its own unit, not a ride-along.

### Security — reviewed by 013, deliberately not changed
- **`AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN")`** is still configured and still unused by
  every endpoint and client. 013 confirmed no endpoint needs it. Harmless; drop it, or keep it as
  the hook for a future header-token client.
- **`POST /api/push/broadcast` and `POST /api/push/send-to-selected` have no browser caller.**
  Dead HTTP surface, but role-gated and not CSRF-reachable — deletion is cleanup, not security.
- **`Set-Cookie` carries no `Secure` flag under the test host**, because the default is
  `CookieSecurePolicy.SameAsRequest` and the test client speaks http. Over https in production the
  flag is emitted. Not changed; an explicit `Always` is a separate hardening decision.
- ~~**Pre-existing integration-test flake, not caused by 013**~~ — **fixed by unit 018.** The
  shared `SqliteConnection` is gone and the seed now runs in `IHostedLifecycleService.StartingAsync`.
  Measured before/after in the same harness: **12 failures in 40 isolated runs → 0 in 40.** See
  *Last completed step*.
- ~~Out of scope by instruction and untouched: ... cookie validation / SQLite pressure ...~~ -
  **done by unit 019** (Option A, 5-minute write throttle). Still out of scope and untouched:
  password policy, MFA, security headers / CSP, PWA cache strategy, push architecture refactor,
  `Program.cs` cleanup.

### Raised by 019, deliberately not changed
- **`LastLoginDate` is named "last login" but means "last authenticated activity".** Unit 019 kept
  the name by owner decision. Renaming it (say to `LastActivityAt`) needs a migration plus an edit
  to `LoginStatusBadge`, `AvatarCard`, `UserCard`, `UserRoles`, `UserProfileService`,
  `LoginStatisticsButton` and `AuditLogAppender`. Its own unit if wanted.
- ~~**RTUB's `OnValidatePrincipal` replaces Identity's `SecurityStampValidator` entirely**~~ -
  **fixed by unit 020.** The handler now calls the framework validator after its own checks. The
  stricter per-request stamp check was kept, `ValidationInterval` stays at the framework default,
  and the principal refresh / `ShouldRenew` behaviour is back. See *Last completed step*.
- **The `SQLITE_LOCKED` retry loop was kept**, now correctly commented. The `IsInRoleAsync` pair is
  still 2 SELECTs and could be one join - not done, out of 019's scope.
- Known and accepted framework limitation (from 012): antiforgery tokens are bound to the user
  identity, so multiple tabs signed in as different users are unsupported. Documented ASP.NET Core
  behavior, not an RTUB defect.

### Raised by 020, deliberately not changed
- **`UserRoles.razor:613` and `RoleManagementService.cs:121` call `UpdateSecurityStampAsync` on a
  role change**, commented "force fresh cookies and token refresh". That is not what it does in
  stock Identity either: bumping the stamp makes `VerifySecurityStamp` fail, so it is a **forced
  logout**, not a claims refresh. With 020's composition a plain role change would now propagate
  to a live session within `ValidationInterval` without logging anyone out. Deciding whether
  either call site should drop the stamp bump is a UX/security decision, not a ride-along.
- **`ValidationInterval` is still the framework default (30 min) and still unconfigured.** Nothing
  in the repo sets `SecurityStampValidatorOptions`. Lowering it would only speed up claim refresh -
  every security check already runs per request - at 2 extra SELECTs per user per interval.
- **The `IsInRoleAsync` pair is still 2 SELECTs** and could be one join. Out of 020's scope, as it
  was out of 019's; the framework composition did not eliminate it.

### Raised by 021, deliberately not changed
- **CSP is not enabled.** Of the four blockers enumerated in *Last completed step*, the first -
  the 15 `eval` interop sites - is **cleared by 022**. Three remain: 3 inline `<script>` blocks
  (023), 11 `<style>` blocks + 213 `style=` attributes (024), and the R2 origin only being known
  at runtime (025). No `Content-Security-Policy-Report-Only` was shipped either - a report-only
  policy is worth adding once the blockers are down and it can report something actionable, not
  while it would report every page load.
- **`Cross-Origin-Opener-Policy` / `Cross-Origin-Embedder-Policy` / `Cross-Origin-Resource-Policy`
  were not added.** Outside the brief, and COOP in particular needs its own check of the
  `LoginPopup` flow and anything relying on `window.opener` before it can be called safe. Cheap to
  add later; not free to add blind.
- **`X-XSS-Protection` was not added.** It is removed from Chrome and Edge, ignored by Firefox,
  and its legacy filter mode was itself an exploitable primitive. Adding it is pure theatre.
- **`https://fonts.googleapis.com` is a dead `preconnect` + `dns-prefetch`** in `App.razor:24-25`:
  no stylesheet link, no `@font-face`, nothing loads from it. Two wasted connection hints. Delete
  them in whichever frontend unit is next in that file - not worth a unit of their own, and 021
  had no reason to touch `App.razor`.
- **`UseHttpsRedirection` is still skipped outside Development** (`Program.cs`). Unchanged for the
  third unit running; see the 014 entry above. Now that forwarded headers are confirmed on, this
  is a one-line change gated only on someone accepting a production pipeline behaviour change.

### Carried forward (unchanged)
- **`xUnit1051` suppressed, not adopted** (1634 sites). Its own unit if wanted: mechanical, but it
  touches nearly every test file, so it must not ride along with anything else.
- **`Microsoft.Testing.Extensions.Telemetry/2.4.0`** arrives transitively via `xunit.v3` and
  reports usage metrics to Microsoft. Opt out with `TESTINGPLATFORM_TELEMETRY_OPTOUT=1`.
- **`global.json` raises the SDK floor to 10.0.100** for running tests. No pin added.
- Running a test `.exe` directly gives xUnit's native console runner, not the MTP CLI, because
  `UseMicrosoftTestingPlatformRunner` was not set. `dotnet test` is unaffected.
- **Remaining dependency findings:**
  - Microsoft 10.0.11 → **10.0.12** train — one servicing bump across src + tests. Not started.
  - `MockQueryable.Moq 10.0.12` — held at 10.0.8; gated on the EF Core 10.0.12 bump above.
  - `AWSSDK.Core 4.0.3.8` → 4.0.102.6 and `AWSSDK.S3 4.0.10` → 4.0.103.3 — independent train.
  - `QuestPDF 2024.10.3` → 2026.9.0 — major train, likely breaking. Not started.
  - `AWSSDK.Core` is referenced directly by `RTUB.Integration.Tests`, `RTUB.Shared.Tests` and
    `RTUB.Web.Tests` but **used by none of them**. Three dead direct references — AWS train.
  - `Portable.BouncyCastle 1.9.0` — transitive via `WebPush 1.0.13`. Legacy id, not deprecated,
    no advisories. Note it if an advisory lands.
- **Test-suite hygiene:** `RTUB.Web.Tests` skips **56 of 791** tests (mostly `*PageTests` modal /
  button cases). Pre-existing.
- **Push subsystem debt:** `WebPushClient` is still newed up inside `PushNotificationService`, so
  it cannot be mocked and no test covers an actual send, a retry, or 404/410 cleanup. Also: two
  service-worker registration paths, unbounded `BroadcastAsync` fan-out, no `CancellationToken`,
  no delivery metrics.
### Raised by 022, deliberately not changed
- **`src/RTUB.Web/wwwroot/js/rtub.carousel.js` is dead.** A minified Bootstrap-Carousel fallback
  shim that self-registers on `DOMContentLoaded` and immediately bails when `bootstrap.Carousel`
  exists. It is referenced by **no** `<VersionedAsset>`, no `<script>` and no interop call, so it
  is never loaded at all. Delete it, or wire it up deliberately - 022 left it exactly as it was.
- **`PushNotificationsManager` is still constructed inside `pwaHelper.initializePushManager` and
  parked on `window.rtubPushManager`.** 022 kept that arrangement on purpose (it is the push
  architecture, not the eval cleanup). The push-modernization unit owns it.

- **Phase 1C:** remaining optional custom skills — deliberately not created.
- Work-branch cleanup (`chore/001`–`chore/011`) — delete when convenient.
- Two `graphifyy 0.9.56 + MCP` installs (isolated venv, Microsoft-Store Python user site). The
  Store one wins PATH and works; both are compatible, so neither needs removing.
- Pending feature work — unchanged, not part of any phase.

### Raised by 023, deliberately not changed
- **Two service-worker registration paths still exist.** 023 removed the `App.razor` duplicate, so
  page-load registration is `wwwroot/js/sw-register.js` alone - but
  `wwwroot/js/push-notifications.js:91` still calls `navigator.serviceWorker.register()` on demand
  during push opt-in, with **different options** (no `scope`, no `updateViaCache`). Pre-existing,
  and already flagged by the `rtub-push` skill. The push unit owns consolidating it.
- ~~`sw-register.js` toast styles~~ and ~~`offline.html` inline `<style>`~~ - **both done by 024.**
- **`App.razor` still has the dead `fonts.googleapis.com` `preconnect` / `dns-prefetch`.** No
  matching stylesheet and no `@font-face` anywhere. 024 did not take it either - it is not an inline
  style and removing it is unrelated to this unit. Delete it in 025, where `font-src` is decided.
- **`rtub.carousel.js` is still dead** (raised by 022, unchanged).

### Raised by 024, deliberately not changed
- **`memberMap.js` still builds an inline `onerror="this.src=..."` handler** inside its popup HTML
  string (the member avatar). That is a **`script-src`** blocker, not `style-src`, so it is outside
  024's scope - but it means 023's "0 inline handlers" count missed markup built in JavaScript.
  023's `avatarFallback.js` already provides the delegated replacement, so the fix is swapping the
  attribute for `data-avatar-fallback`. **025 must take this or the policy will break the map
  popups' avatar fallback.** Consider extending `InlineScriptPolicyTests` to sweep JS strings the
  way `InlineStylePolicyTests` now does.
- **`.meeting-today-badge` is orphaned CSS.** Now in `css/3-components/meeting-card.css`; no element
  has ever carried the class. Kept because deleting it is unrelated cleanup, and its absence from the
  markup is now recorded in `MeetingCardTests`.
- **The rare-tab badge animation is dead.** `animation: rare-tab-pulse` cannot resolve, because the
  keyframes exist only in scoped CSS where Blazor renames them. Broken before 024 and identically
  broken after. Fixing it would start an animation that has never run - a visual change, so it is the
  owner's call.
- **`Profile.razor`'s `overflow-x: hidden` intent is unimplemented.** The `:global(...)` rule was
  inert and was deleted. If horizontal overflow on the profile page was a real concern, it needs a
  real rule - that is a visual change, not a CSP one.
- **`dotnet test` does not work in this repo.** It reports "Zero tests ran" / exit 5 for every
  project, with or without a filter, so the test executables have to be run directly
  (`tests/<proj>/bin/Debug/net10.0/<proj>.exe`). Worth fixing, since CI and the recorded baselines
  depend on it.
- **`BetServiceTests.PlaceBetAsync_WithInsufficientBalance_ThrowsException` fails in a full-project
  run and passes in isolation**, at `00e8009f` as well as on this branch - shared-fixture ordering.
  Pre-existing; its own unit.

## Relevant files (unit 024)

**New CSS (14 files, all plain `wwwroot/css`, no bundler, no package):**
- `css/9-overrides/inline-style-utilities.css` - 98 generated classes replacing the 170 static
  attributes. Imported **last** in `site.css`, on purpose.
- `css/9-overrides/dynamic-style-classes.css` - hand-written: finite-state modifiers, the rank and
  biome palettes, and the `--fill-pct` / `--swatch` hooks.
- `css/offline.css` - standalone, precached; `offline.html` does not load `site.css`.
- `css/3-components/sw-update-toast.css` - the update toast, keyframes included.
- `css/3-components/{meeting-card,question-card,month-year-picker}.css`
- `css/4-pages/{meetings,naipes-config,passaro-maluco,questions,report,my-tuno-all-characters,weapon-drink-config}.css`

**New JS (1 file):**
- `wwwroot/js/dynamicStyle.js` (~95 lines) - the validated `data-*` -> CSSOM bridge. Registered in
  `MainLayout.razor` next to `scrollSpy.js`.

**Changed (52 files):**
- 41 `.razor` - inline style attributes replaced by classes; 11 of them also lost a `<style>` block.
- `src/RTUB.Core/Helpers/RankHelper.cs` - `+GetRankColorClass`.
- `src/RTUB.Core/Helpers/BiomeDisplayHelper.cs` - `+GetBiomeColorClass`, `+GetSurviveBiomeColorClass`.
- `wwwroot/css/site.css` - 14 new imports, the last two of which must stay last.
- `wwwroot/css/4-pages/member-map.css` - marker and popup classes.
- `wwwroot/js/sw-register.js` - toast built from DOM nodes and classes.
- `wwwroot/js/memberMap.js` - `divIcon` markup uses classes.
- `wwwroot/offline.html` - `<style>` block -> `<link>`.
- `wwwroot/service-worker.js` - `/css/offline.css` added to `STATIC_ASSETS`. `CACHE_VERSION`
  **not** bumped: the file's content changed, so a new worker installs and `cache.addAll` writes
  the entry into the same `STATIC_CACHE`, with no cache churn for users.
- `src/RTUB.Web/Program.cs` - the CSP comment only; the middleware is byte-identical.

**Deleted:** `InfoSection.Style` parameter, `InstrumentCounter.GetHeaderStyle()`, `MyTunoHome`'s
now-unused `GetBiomeColor` / `GetSurviveBiomeColor` wrappers, and `Profile.razor`'s inert
`:global()` rule. No migrations; no entity, service, repository or DI change.

## Tests (1 new file, +10; 4 existing test files updated)
**New:** `tests/RTUB.Web.Tests/Security/InlineStylePolicyTests.cs` - 10 aggregated Facts, listed
under *Static policy test* above.

**Updated, because the markup contract changed or the assertion was bogus:**
- `RoleBadgeTests` (2 cases) - assert `role-badge--member`, not `#007bff`.
- `EmailRecipientsPreviewTests` - asserts `subscriber-list--h400`, not `max-height: 400px`.
- `MeetingCardTests` - the `meeting-today-badge` assertion was a false positive and is removed.
- `MonthYearPickerTests` - re-targeted from `<style>` text to the rendered hook plus the stylesheet.

No test was deleted and no assertion was weakened without its value being re-pinned elsewhere.

## Latest validation (unit 024)
| Check | Result |
| --- | --- |
| `dotnet build RTUB.sln -c Release` | **0 warnings, 0 errors** |
| `dotnet build RTUB.sln` (Debug) | **0 warnings, 0 errors** |
| `RTUB.Core.Tests` | 791 total, **0 failed** |
| `RTUB.Application.Tests` | 1997 total, 1 failed - **pre-existing, see below** |
| `RTUB.Shared.Tests` | 768 total, **0 failed**, 2 skipped |
| `RTUB.Web.Tests` | 811 total, **0 failed**, 56 skipped |
| `RTUB.Integration.Tests` | 274 total, **0 failed**, 2 skipped |
| **Suite total** | **4641 run, 4580 passed, 1 failed (pre-existing), 60 skipped** |
| `node --check` on all 4 changed/new JS files | pass |
| `git diff --check` | clean (3 trailing-whitespace and 4 EOF-blank-line issues found and fixed) |
| Secret scan over the full diff | **no matches** |
| Migrations added | **none** |

**Test delta: +10, exactly the new policy Facts.** 4631 -> 4641 total.

**The one failure is pre-existing and unrelated - this was proven, not assumed.**
`BetServiceTests.PlaceBetAsync_WithInsufficientBalance_ThrowsException` fails in a full-project run
and passes in isolation, which is the signature of shared-fixture ordering. A **clean detached
worktree at `00e8009f`** (dev, before any 024 change) was built and run: it fails **the same test,
1 of 1997**. 024 touches no Application-layer code at all - only `RTUB.Core/Helpers` additions,
markup, `wwwroot` assets and tests.

Note the recorded 023 baseline of "4571 passed / 0 failed / 60 skipped" does **not** reproduce on
this machine. `dotnet test` cannot drive this repo's xUnit v3 / Microsoft.Testing.Platform runners
here ("Zero tests ran", exit 5) and the test executables must be run directly; under that runner
the Bets test fails at `00e8009f` too. Worth reconciling before the next unit - see *Deferred*.

## Static scan (unit 024) - the acceptance gate
Tracked application source, excluding `wwwroot/lib`, `node_modules`, `bin`, `obj`:

| Pattern | Before | After |
| --- | --- | --- |
| `style="` in `.razor` | 213 | **0** |
| `<style` in `.razor` / `.html` | 12 | **0** |
| `style=` in app JS strings | 4 | **0** |
| `.style.cssText` in app JS | 1 | **0** |
| `createElement('style')` in app JS | 1 | **0** |
| `setAttribute('style'` in app JS | 0 | 0 |
| `insertRule` in app JS | 0 | 0 |
| Razor-interpolated `style=` | 43 | **0** |
| `style="` in `.cshtml` (email only, not CSP-governed) | 177 | 177 |

## Browser validation (unit 024) - RUN
Recorded under *Browser validation* above. Summary: homepage and `/login` at 1280 and 375 with zero
inline styles and zero `<style>` elements in the live DOM and no horizontal overflow; all 14
stylesheets 200; every migrated value verified by computed style; the update toast reconstructed and
matched against its old appearance; the dynamic bridge verified including a rejected injection; and
`offline.html` proven to render fully styled with the server killed and the port confirmed closed.

## Blockers found by 024
**None outstanding.** Three things were found and dealt with inside the unit:
1. The brief's CSP premise about `element.style` was wrong; measured and corrected, which is what
   made the dynamic cases solvable.
2. Two bUnit assertions were passing by matching CSS text inside a `<style>` block rather than
   rendered markup. Both corrected; one of them exposed orphaned CSS (see *Deferred*).
3. A first cut of the mechanical transform corrupted 26 tags by applying an insert and a delete at
   the same offset. Caught immediately by inspecting the diff, all `.razor` files reverted, the
   transform fixed with an overlap assertion, and re-run from clean. The final diff is verified free
   of malformed tags and duplicate `class` attributes.

**Remaining CSP blockers after 024: none on the script or style side.** The only open item for 025
is Blocker 4 - building the R2 origin into the policy from `IConfiguration`.

# PWA practices

RTUB-specific PWA rules: manifest, service worker, brand assets, install targets. Push: [react-push-v2](react-push-v2.md),
[push-notifications](push-notifications.md). Offline fallback: `wwwroot/offline.html`; server error page: `wwwroot/error.html`.

## Manifest (`wwwroot/manifest.webmanifest`)

- Linked from the React shell `src/RTUB.Web/portal/index.html` (`<link rel="manifest" href="/manifest.webmanifest">`,
  served as `application/manifest+json`). Keep the `.webmanifest` extension.
- Icons: 192 and 512 PNG (`any`) plus one maskable 512 PNG (artwork inside the 80% safe zone on a full-bleed
  background; a separate file because the normal icon has transparent rounded corners). More sizes are optional and
  RTUB does not ship them. Shortcut: Atuações (`/events`). `start_url` is `/?utm_source=pwa`, `scope` `/`, `theme_color`
  `#3F2A86`.
- Keep `"id": "/"` unchanged. It is the installed app's identity and resolves against the origin, so it is already
  RTUB-specific. Changing or removing it (removal falls back to `start_url`) gives a different identity, which the
  manifest spec treats as a distinct application rather than a replacement for the RTUB already installed. Pinned by
  `PwaManifestTests`.

### PWABuilder report card

`https://www.pwabuilder.com/reportcard?site=https://rtub.pt/` lists manifest members RTUB could add. The ones that are
true for RTUB are declared (pinned by `PwaManifestTests.Manifest_DeclaresTheInstallExtras_Task053`):

| Member | Value | Why |
|---|---|---|
| `display_override` | `["standalone", "minimal-ui"]` | The same intent as `display`, with the fallback order stated. |
| `prefer_related_applications` | `false` | The installed PWA is the product; the store app is a TWA of it. |
| `related_applications` | Play Store `ipb.pt.rtub.app` | The real listing; the id must equal `.well-known/assetlinks.json` → `package_name` (the test reads both). |
| `edge_side_panel` | `preferred_width: 420` | Edge can pin RTUB to its side panel; the portal is laid out for 320 px and up. |
| `share_target` | GET `/share` with `title`, `text`, `url` | The installed app receives shares (React `/share`, [react-identity](react-identity.md)). |

Skipped on purpose, so the report card is not chased again. Add one only with a real value and a decision:

- `iarc_rating_id`: needs the IARC certificate from Play Console (App content → Content rating). Owner action.
- `scope_extensions`: no second origin we want inside the app (`www.rtub.pt` serves the same app with no redirect to
  `rtub.pt`: a deferred finding, not a scope extension).
- `window-controls-overlay` / `tabbed` in `display_override`: desktop title-bar work with no value for a phone-first app.
- `file_handlers`, `protocol_handlers`, `widgets`, `note_taking`: RTUB opens no files, handles no protocol, has no
  Windows widget and is not a notes app. A stub would lie to the OS.
- Periodic background sync / background sync in the service worker: an empty listener only games the score; `/api/*`
  is never cached by design and nothing queues writes offline.

## Service worker (`wwwroot/service-worker.js`)

- Registered once, by `wwwroot/js/sw-register.js`, after the page has loaded (`load`; at once if already complete):
  installing precaches ~150 KB and claims the page, which competed with the bundle and fonts for the first paint.
  `ServiceWorkerReliabilityTests` counts the registrations. Everything else adopts it through
  `navigator.serviceWorker.ready` / `getRegistration()`.
- Navigations: network first, `/offline.html` as the fallback; navigation preload is enabled on activate (the document
  branch takes `event.preloadResponse` and fetches only when there is none).
- Never cached, and left to the browser without `respondWith`: `/api/`, `/auth/`, `/hubs/`, `/health`, non-GET
  requests, non-http schemes, and everything the last branch used to pass through (fonts, the manifest, cross-origin
  scripts).
- Images are cache-first and never revalidated. Hashed portal assets (`/portal/assets/<Name>-<hash>.js|css`) are
  cache-first in the dynamic cache (a content-hashed file never changes under its name; the host serves them
  `immutable`). The first time a build of a chunk is stored, the worker deletes the other builds of the same chunk (same
  name and type). A Vite build never has two chunks with one name and type (`FrontendRemediation226SourceTests`).
  Other scripts and stylesheets are stale-while-revalidate.
- `CACHE_VERSION` (`rtub-v…` in `service-worker.js`) must be bumped whenever a cached image is replaced under the same
  URL, or installed apps keep the old picture.
- Push handlers: [react-push-v2](react-push-v2.md) → *Service worker*.

## RTUB brand assets

One source: the square artwork `wwwroot/images/rtub_new_logo.png` (1254 px, not shipped as such). Everything else is
resized from it, never cropped or stretched:

| File | Size | Used by |
|---|---|---|
| `/images/rtub_logo.webp` | 1024px WebP | canonical full logo: Home hero (desktop), email header, server `GroupAvatar` |
| `/icons/rtub-logo-352.webp` | 352px WebP, ~34 KB | splash, header brand, Home badge / record / hero on phones, News, Messages "Sistema RTUB" and groups, `error.html`, `offline.html` |
| `/icons/rtub-logo-192.png`, `-512.png` | 256-colour PNG | manifest `any`, push `icon`, media-session artwork, owner push preview |
| `/icons/rtub-logo-maskable-512.png` | PNG, opaque | manifest `maskable` (artwork at 80% on the artwork's own border colour `#1E0B1A`) |
| `/icons/rtub-badge-96.png` | PNG | push `badge` |
| `/apple-touch-icon.png` | 180px PNG, opaque | iOS home screen (`<link rel="apple-touch-icon">`) |
| `/favicon.ico` | 16 / 32 / 48 | browser tab. The full artwork, not a crop: a tower-only crop is a grey blob at 16px, while the full badge keeps its dark-purple shape and colours |

## Install targets

- Mobile meta tags live in `portal/index.html`: `apple-mobile-web-app-capable`, `apple-mobile-web-app-title`,
  `apple-mobile-web-app-status-bar-style` (`black-translucent`), `theme-color`, the 180x180 `apple-touch-icon`.
  The viewport allows user scaling (accessibility).
- Android (TWA): Digital Asset Links in `.well-known/assetlinks.json`; its `package_name` equals the manifest's
  `related_applications` id. Follow Google Play guidelines.
- iOS: Safari "Add to Home Screen"; iPhone/iPad offer push only from the installed app.
- Lock-screen Now Playing: the page supplies only `MediaMetadata` (title, album, artwork; it has no URL) and the
  play/pause/next/previous/seek handlers; no Media Session action fires when the card itself is tapped. With several
  Home Screen web apps installed, a tap has opened another one. RTUB-side identity and navigation causes were checked and
  excluded, so this is most likely iOS/WebKit Home Screen routing.
- External links from the installed app: the Google Calendar offer opens Google with a plain link (`target=_blank`,
  `noopener noreferrer`) from the member's click, never after an async save (popup blockers, unexpected navigation). An
  installed app may hand it to the browser or block it: "Copiar o link" is the fallback. RTUB's state is untouched either
  way. Real-device check outstanding.

## Release check

Manifest valid and reachable; service worker registers; installs on Android and iOS; offline page shows; icons and
shortcut work; share target works (share a link from another Android app to the installed RTUB: `/share` shows it,
"Copiar" copies it); push works (`/profile` → "Notificações push": turn on, test push arrives, off sticks after reload;
check the Android installed app and the iPhone/iPad Home Screen app too); app works in standalone mode.

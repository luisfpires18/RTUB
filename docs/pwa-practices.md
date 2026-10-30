# PWA Best Practices

This document outlines Progressive Web App (PWA) development best practices for the RTUB project, focusing on mobile-first design, offline support, and app store deployment.

## Table of Contents
- [Manifest Configuration](#manifest-configuration)
- [Service Worker](#service-worker)
- [Mobile Optimization](#mobile-optimization)
- [Offline Support](#offline-support)
- [Performance](#performance)
- [App Store Deployment](#app-store-deployment)

## Manifest Configuration

### manifest.webmanifest

✅ **Always:**
- Use `.webmanifest` extension (not `.json`)
- Include all required fields: `name`, `short_name`, `icons`, `start_url`, `display`
- Provide 192 and 512 PNG icons (`any`) plus one maskable 512 PNG (artwork inside the 80% safe zone on a full-bleed
  background); more sizes are optional and RTUB does not ship them
- Include maskable icons for Android (a separate file: the normal icon has transparent rounded corners)
- Add shortcuts for common actions
- Set proper `scope` and `start_url`
- Keep `"id": "/"` unchanged. It is the installed app's identity and resolves against the origin
  (`https://<host>/`), so it is already RTUB-specific. Changing or removing it (removal falls back
  to `start_url`) gives a different identity, which the manifest spec treats as a distinct
  application rather than a replacement for the RTUB already installed. Pinned by `PwaManifestTests`.

### PWABuilder report card (053)

`https://www.pwabuilder.com/reportcard?site=https://rtub.pt/` lists manifest members RTUB could add. Task 053 added
the ones that are true for RTUB (pinned by `PwaManifestTests.Manifest_DeclaresTheInstallExtras_Task053`):

| Member | Value | Why |
|---|---|---|
| `display_override` | `["standalone", "minimal-ui"]` | The same intent as `display`, with the fallback order stated. |
| `prefer_related_applications` | `false` | The installed PWA is the product; the store app is a TWA of it. |
| `related_applications` | Play Store `ipb.pt.rtub.app` | The real listing; the id must equal `.well-known/assetlinks.json` → `package_name` (the test reads both). |
| `edge_side_panel` | `preferred_width: 420` | Edge can pin RTUB to its side panel; the portal is laid out for 320 px and up (#229). |
| `share_target` | GET `/share` with `title`, `text`, `url` | The installed app receives shares again (React `/share`, `docs/react-identity.md`). Removed in 036, back by the owner's decision. |

Skipped on purpose, so the report card is not chased again. Add one only with a real value and a decision:

- `iarc_rating_id`: needs the IARC certificate from Play Console (App content → Content rating). Owner action, one line.
- `scope_extensions`: no second origin we want inside the app (`www.rtub.pt` serves the same app with no redirect to
  `rtub.pt`: a deferred finding, not a scope extension).
- `window-controls-overlay` / `tabbed` in `display_override`: desktop title-bar work with no value for a phone-first app.
- `file_handlers`, `protocol_handlers`, `widgets`, `note_taking`: RTUB opens no files, handles no protocol, has no
  Windows widget and is not a notes app. A stub would lie to the OS.
- Periodic background sync / background sync in the service worker: an empty listener only games the score; `/api/*`
  is never cached by design and nothing queues writes offline.

```json
{
  "id": "/",
  "name": "RTUB - Real Tuna Universitária de Bragança",
  "short_name": "RTUB",
  "start_url": "/?utm_source=pwa",
  "display": "standalone",
  "scope": "/",
  "icons": [
    {
      "src": "/icons/rtub-logo-192.png",
      "sizes": "192x192",
      "type": "image/png",
      "purpose": "any"
    },
    {
      "src": "/icons/rtub-logo-maskable-512.png",
      "sizes": "512x512",
      "type": "image/png",
      "purpose": "maskable"
    }
  ],
  "shortcuts": [
    {
      "name": "Atuações",
      "url": "/events",
      "icons": [{ "src": "/icons/rtub-logo-192.png", "sizes": "192x192" }]
    }
  ]
}
```

### HTML Linking

✅ **Always:**
- Link manifest in the React shell `src/RTUB.Web/portal/index.html` (static HTML; `App.razor` until 037)
- Use correct MIME type: `application/manifest+json`
- Ensure manifest is accessible at root level

```html
<link rel="manifest" href="/manifest.webmanifest" />
```

## Service Worker

### Registration

✅ **Always:**
- Register the service worker once the page has loaded (`load`; at once if the page is already complete). Installing it
  precaches ~150 KB and claims the page, which used to compete with the bundle and fonts for the first paint (task 054;
  the earlier immediate registration served PWABuilder's one-time detection)
- Register with explicit scope
- Handle registration errors gracefully
- Check for updates periodically

```javascript
function registerServiceWorker() {
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('/service-worker.js', {
            scope: '/'
        })
            .then(registration => {
                console.log('Service Worker registered:', registration.scope);
            })
            .catch(error => {
                console.error('Service Worker registration failed:', error);
            });
    }
}

// Once the page has loaded (or at once, if it already has)
if (document.readyState === 'complete') registerServiceWorker();
else window.addEventListener('load', registerServiceWorker);
```

In RTUB this is `wwwroot/js/sw-register.js`, the **only** registration (`ServiceWorkerReliabilityTests` counts it).
Everything else adopts it through `navigator.serviceWorker.ready` / `getRegistration()`, as the React push client does.

### Web Push (task 038)

✅ **Always:**
- Detect capabilities (`serviceWorker`, `PushManager`, `Notification`), never the user agent
- Ask for notification permission only from a click, first thing in the handler
- Send `subscription.toJSON()` to `/api/push/subscribe` with the antiforgery token; never keep push state in `localStorage`
- Show the notification inside `event.waitUntil` and do nothing else in the `push` handler
- Open only same-origin URLs from `notificationclick`

Detail: `docs/react-push-v2.md`.

### Caching Strategy

✅ **Always:**
- Cache static assets on install
- Use cache-first for images
- Use network-first for HTML pages
- Provide offline fallback page
- Version cache names

```javascript
const CACHE_VERSION = 'rtub-v17';
const STATIC_CACHE = `rtub-static-${CACHE_VERSION}`;

// Cache static assets on install
self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(STATIC_CACHE)
            .then(cache => cache.addAll(STATIC_ASSETS))
            .then(() => self.skipWaiting())
    );
});
```

## Mobile Optimization

### Viewport Configuration

✅ **Always:**
- Set proper viewport meta tag
- Allow user scaling (accessibility)
- Set maximum scale appropriately

```html
<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=5.0, user-scalable=yes" />
```

### RTUB brand assets (task 046)

One source: the square artwork `wwwroot/images/rtub_new_logo.png` (1254px, not shipped as such). Everything else is
resized from it, never cropped or stretched:

| File | Size | Used by |
|---|---|---|
| `/images/rtub_logo.webp` | 1024px WebP | canonical full logo: Home hero (desktop), email header, server `GroupAvatar` |
| `/icons/rtub-logo-352.webp` | 352px WebP, ~34 KB | splash, header brand, Home badge / record / hero on phones, News, Messages "Sistema RTUB" and groups, `error.html`, `offline.html` |
| `/icons/rtub-logo-192.png`, `-512.png` | PNG | manifest `any`, push `icon`, media-session artwork, owner push preview |
| `/icons/rtub-logo-maskable-512.png` | PNG, opaque | manifest `maskable` (artwork at 80% on the artwork's own border colour `#1E0B1A`) |
| `/icons/rtub-badge-96.png` | PNG | push `badge` |
| `/apple-touch-icon.png` | 180px PNG, opaque | iOS home screen (`<link rel="apple-touch-icon">`) |
| `/favicon.ico` | 16 / 32 / 48 | browser tab. The full artwork, not a crop: a tower-only crop was tested and is a grey blob at 16px, while the full badge keeps its dark-purple shape and colours |

Images are cache-first in the service worker and never revalidated: replacing any of these under the same URL needs a
`CACHE_VERSION` bump, or installed apps keep the old picture. `wwwroot/images/icons/*` (Nov 2025 copies of the old
logo, no reference anywhere) was removed in #226. Also #226: the 512 and 192 PNGs are 256-colour PNGs (same size and
picture, 427 → 61 KB and 79 → 15 KB; the maskable 512 is untouched), with the `CACHE_VERSION` bump to `rtub-v2.10.0`.

Hashed portal assets (`/portal/assets/<Name>-<hash>.js|css`) are cache-first in the dynamic cache since task 054
(`rtub-v2.11.0`): a content-hashed file never changes under its name, so a cached copy is served with no revalidation
fetch and no cache write per page load (they were stale-while-revalidate before, which re-fetched and re-wrote ~0.5 MB
per navigation); the host also serves them `immutable`. Other scripts and stylesheets stay stale-while-revalidate. Since
#226 F15, the first time a build of a chunk is stored, the worker deletes the other builds of the same chunk (same name
and type), so a release no longer leaves the old copies until the next `CACHE_VERSION`. A Vite build never has two
chunks with one name and type (guarded by `FrontendRemediation226SourceTests`).

Also since task 054: navigations use navigation preload (enabled on activate; the document branch takes
`event.preloadResponse` and fetches only when there is none), and the never-cached paths (`/api/`, `/auth/`, `/hubs/`,
`/health`), non-GET requests, non-http schemes and everything the last branch used to pass through (fonts, the manifest,
cross-origin scripts) are left to the browser without `respondWith`, so the worker is out of their path entirely.

### Touch Icons

✅ **Always:**
- Provide Apple touch icons (180x180)
- Set proper sizes attribute
- Use PNG format

```html
<link rel="apple-touch-icon" sizes="180x180" href="/apple-touch-icon.png" type="image/png" />
```

### Mobile Meta Tags

✅ **Always:**
- Include `apple-mobile-web-app-capable`
- Set `apple-mobile-web-app-status-bar-style`
- Include `theme-color` meta tag
- Add `format-detection` to prevent phone number auto-linking

```html
<meta name="apple-mobile-web-app-capable" content="yes" />
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
<meta name="theme-color" content="#3F2A86" />
<meta name="format-detection" content="telephone=no" />
```

## Offline Support

### Offline Page

✅ **Always:**
- Create `/offline.html` fallback page
- Show connection status
- Provide retry mechanism
- Auto-reload when connection restored

```html
<!DOCTYPE html>
<html lang="pt">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Offline - RTUB</title>
</head>
<body>
    <div class="container">
        <h1>Sem Conexão</h1>
        <p>Parece que estás offline. Verifica a tua ligação à internet.</p>
        <button onclick="window.location.reload()">Tentar Novamente</button>
    </div>
    <script>
        window.addEventListener('online', () => {
            window.location.href = '/';
        });
    </script>
</body>
</html>
```

### Service Worker Offline Handling

✅ **Always:**
- Return offline page for navigation requests
- Cache critical assets
- Handle fetch errors gracefully

```javascript
self.addEventListener('fetch', (event) => {
    if (event.request.mode === 'navigate') {
        event.respondWith(
            fetch(event.request)
                .catch(() => caches.match('/offline.html'))
        );
    }
});
```

## Performance

### Asset Optimization

✅ **Always:**
- Use `VersionedAsset` component for cache busting
- Compress images (WebP when possible)
- Minimize JavaScript and CSS
- Use CDN for external libraries

### Loading Strategy

✅ **Always:**
- Preload critical resources
- Use `preconnect` for external domains
- Lazy load non-critical components

```html
<link rel="preconnect" href="https://fonts.googleapis.com" crossorigin />
<link rel="dns-prefetch" href="https://fonts.googleapis.com" />
```

## App Store Deployment

### PWABuilder

✅ **Always:**
- Test with PWABuilder before deployment
- Ensure service worker is detected
- Verify manifest is accessible
- Check all icons are present

### Android (TWA)

✅ **Always:**
- Configure Digital Asset Links (`.well-known/assetlinks.json`)
- Set proper `id` field in manifest
- Test in Trusted Web Activity mode
- Follow Google Play Store guidelines

### iOS

✅ **Always:**
- Provide Apple touch icons
- Set proper meta tags
- Test in Safari
- Follow App Store guidelines

ℹ️ **Lock-screen Now Playing:** the page supplies only `MediaMetadata` (title, album, artwork; it has
no URL) and the play/pause/next/previous/seek handlers; no Media Session action fires when the card
itself is tapped. With several Home Screen web apps installed, a tap has opened another one (fix/030).
RTUB-side identity and navigation causes were checked and excluded, so this is most likely iOS/WebKit
Home Screen routing behaviour.

## Testing

### PWA Testing Checklist

- [ ] Manifest is accessible and valid
- [ ] Service worker registers successfully
- [ ] App installs on Android
- [ ] App installs on iOS
- [ ] Offline page displays correctly
- [ ] Icons display properly
- [ ] Shortcuts work
- [ ] Share target works: share a link from another Android app to the installed RTUB → `/share` shows it, "Copiar" copies it (053)
- [ ] Push notifications work: `/profile` → "Notificações push" turns this device on, a test push arrives, turning it
  off sticks after a reload (`docs/react-push-v2.md`; check Android installed app and iPhone / iPad Home Screen app too)
- [ ] App works in standalone mode

## Code Quality

### PWA Code Review Checklist

- [ ] Manifest includes all required fields
- [ ] Service worker handles offline scenarios
- [ ] Icons are provided in all required sizes
- [ ] Mobile meta tags are present
- [ ] Viewport is configured correctly
- [ ] Offline page is implemented
- [ ] Caching strategy is appropriate
- [ ] Performance is optimized

## References

- [Web App Manifest](https://developer.mozilla.org/en-US/docs/Web/Manifest)
- [Service Worker API](https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API)
- [PWABuilder](https://www.pwabuilder.com/)
- [PWA Best Practices](https://web.dev/progressive-web-apps/)
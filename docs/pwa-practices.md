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
- Provide multiple icon sizes (120, 152, 167, 180, 192, 256, 384, 512)
- Include maskable icons for Android
- Add shortcuts for common actions
- Set proper `scope` and `start_url`

```json
{
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
      "src": "/icons/rtub-logo-192.png",
      "sizes": "192x192",
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
- Link manifest in `App.razor` (static HTML)
- Use correct MIME type: `application/manifest+json`
- Ensure manifest is accessible at root level

```razor
<link rel="manifest" href="/manifest.webmanifest" />
```

## Service Worker

### Registration

✅ **Always:**
- Register service worker immediately (not waiting for load)
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

// Register immediately for PWABuilder detection
registerServiceWorker();
```

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

```razor
<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=5.0, user-scalable=yes" />
```

### Touch Icons

✅ **Always:**
- Provide Apple touch icons (180x180)
- Set proper sizes attribute
- Use PNG format

```razor
<link rel="apple-touch-icon" sizes="180x180" href="/icons/rtub-logo-180.png" type="image/png" />
```

### Mobile Meta Tags

✅ **Always:**
- Include `apple-mobile-web-app-capable`
- Set `apple-mobile-web-app-status-bar-style`
- Include `theme-color` meta tag
- Add `format-detection` to prevent phone number auto-linking

```razor
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

```razor
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

## Testing

### PWA Testing Checklist

- [ ] Manifest is accessible and valid
- [ ] Service worker registers successfully
- [ ] App installs on Android
- [ ] App installs on iOS
- [ ] Offline page displays correctly
- [ ] Icons display properly
- [ ] Shortcuts work
- [ ] Share target works (if implemented)
- [ ] Push notifications work (implemented — see `.claude/skills/rtub-push/SKILL.md`)
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
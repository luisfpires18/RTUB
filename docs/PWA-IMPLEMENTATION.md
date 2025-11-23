# PWA Implementation for RTUB

This document describes the Progressive Web App (PWA) implementation for the RTUB platform, specifically optimized for Android installation.

## Overview

The RTUB platform implements PWA functionality to allow users to install the application on their devices, particularly focusing on Android devices using Chrome, Edge, and Samsung Internet browsers.

## Architecture

### Manifest Configuration

**File**: `/wwwroot/manifest.webmanifest`

The Web App Manifest provides metadata about the application:

```json
{
  "name": "RTUB - Real Tuna Universitária de Bragança",
  "short_name": "RTUB",
  "description": "Real Tuna Universitária de Bragança - Plataforma de Gestão",
  "theme_color": "#3F2A86",
  "background_color": "#ffffff",
  "display": "standalone",
  "scope": "/",
  "start_url": "/",
  "orientation": "portrait-primary",
  "icons": [...]
}
```

**Key Configuration Points:**

- **name**: Full application name displayed during installation
- **short_name**: Name shown on the home screen (limited space)
- **display**: "standalone" hides browser UI for app-like experience
- **scope**: "/" allows all app routes to run in standalone mode
- **start_url**: "/" ensures app opens at root when launched
- **theme_color**: Purple (#3F2A86) matching RTUB brand colors

### Icon Configuration

The manifest includes multiple icon sizes to support various Android devices:

**192x192 pixels** (Required):
- Used for home screen icon
- Two entries: "any" and "maskable" purposes
- "any": Default icon display
- "maskable": Adaptive icon support (allows Android to apply custom shapes)

**512x512 pixels** (Required):
- Used for splash screen
- Two entries: "any" and "maskable" purposes
- Provides high-resolution display on larger devices

**Icon Files**:
- `/icons/rtub-logo-192.png`
- `/icons/rtub-logo-512.png`

### Service Worker

**File**: `/wwwroot/sw.js`

The service worker enables:
1. PWA installability (required by Chrome for "Install app" prompt)
2. Offline functionality (basic caching)
3. Background sync capabilities (future enhancement)

**Caching Strategy**: Network-first with cache fallback
- Attempts to fetch from network first
- Falls back to cache if network unavailable
- Updates cache with fresh content when available

**Cached Resources**:
- Root page (/)
- Manifest file
- Icon files
- Favicon

### Service Worker Registration

**File**: `/wwwroot/js/pwa.js`

Registers the service worker when the page loads:

```javascript
if ('serviceWorker' in navigator) {
  window.addEventListener('load', function() {
    navigator.serviceWorker.register('/sw.js')
      .then(function(registration) {
        console.log('Service Worker registered:', registration.scope);
      })
      .catch(function(error) {
        console.error('Service Worker registration failed:', error);
      });
  });
}
```

**Registration Flow**:
1. Checks if service workers are supported
2. Waits for page load event
3. Registers `/sw.js`
4. Logs success or error to console

### HTML Integration

**File**: `/Shared/MainLayout.razor`

The manifest is linked in the HTML `<head>` section:

```html
<HeadContent>
  <link rel="manifest" href="/manifest.webmanifest" />
  <meta name="theme-color" content="#3F2A86" />
  <meta name="apple-mobile-web-app-capable" content="yes" />
</HeadContent>
```

The PWA script is loaded at the end of the page:

```html
<VersionedAsset Path="/js/pwa.js" Type="VersionedAsset.AssetType.Js" />
```

**Important Notes**:
- Manifest link uses absolute path (`/manifest.webmanifest`)
- Theme color meta tag must match manifest
- Apple-specific meta tags support iOS installation

## PWA Installability Criteria

For Chrome on Android to show "Install app" prompt:

1. ✅ **HTTPS**: Site served over secure connection
2. ✅ **Valid Manifest**: 
   - Includes name and short_name
   - Includes icons (192x192 and 512x512 minimum)
   - Includes start_url
   - Includes display mode
3. ✅ **Service Worker**: Registered and activated
4. ✅ **User Engagement**: User has visited site at least once (30 seconds minimum in some browsers)

## Browser Compatibility

### Chrome for Android
- ✅ Full PWA support
- ✅ "Install app" prompt
- ✅ WebAPK installation (native-like app)
- ✅ Standalone mode

### Samsung Internet
- ✅ PWA support
- ✅ "Add to Home screen"
- ✅ Standalone mode

### Microsoft Edge for Android
- ✅ Full PWA support
- ✅ "Install app" prompt
- ✅ Standalone mode

### Safari on iOS
- ⚠️ Limited PWA support
- ✅ "Add to Home Screen" works
- ✅ Uses `apple-touch-icon` for icon
- ❌ No service worker install prompt
- ❌ Limited background functionality

## Testing

See [PWA-ANDROID-TESTING.md](./PWA-ANDROID-TESTING.md) for detailed testing procedures.

Quick validation:
1. Open DevTools → Application → Manifest
2. Verify all fields are populated correctly
3. Check for warnings or errors
4. Verify service worker is registered and running

## Maintenance

### Updating Icons

When updating PWA icons:

1. Replace files in `/wwwroot/icons/`:
   - `rtub-logo-192.png`
   - `rtub-logo-512.png`
2. Ensure icons follow Android guidelines:
   - No white margins or borders
   - Solid background or transparency
   - Logo centered with padding for maskable support
3. Clear browser cache after deployment
4. Ask users to reinstall app to see new icon

### Updating Manifest

When modifying `manifest.webmanifest`:

1. Validate JSON syntax
2. Test in Chrome DevTools before deploying
3. Increment any cache versions in service worker if needed
4. Consider impact on already-installed users

### Service Worker Updates

When updating `sw.js`:

1. Increment `CACHE_NAME` version (e.g., 'rtub-v2')
2. Test thoroughly as service workers can cause caching issues
3. Consider using `skipWaiting()` for immediate activation
4. Use DevTools → Application → Service Workers to test update flow

## Security Considerations

1. **HTTPS Only**: PWA features require secure origin
2. **Service Worker Scope**: Limited to origin for security
3. **Cache Management**: Regular cache cleanup prevents bloat
4. **Content Security Policy**: Ensure service worker allowed by CSP

## Performance

The service worker provides:
- **Offline Access**: Basic pages available without network
- **Faster Load Times**: Cached resources load instantly
- **Reduced Network Usage**: Less data transferred for repeat visits

## Future Enhancements

Potential PWA improvements:

1. **Push Notifications**: Leverage service worker for notifications
2. **Background Sync**: Sync data when connection restored
3. **Offline Forms**: Queue form submissions when offline
4. **App Shortcuts**: Add common actions to home screen icon menu
5. **Install Prompt**: Custom install banner with app benefits

## Troubleshooting

### Service Worker Not Registering

**Check**:
1. HTTPS enabled
2. Service worker file at `/sw.js`
3. No JavaScript errors blocking registration
4. Browser console for error messages

**Solution**:
- Verify file path and MIME type
- Check for syntax errors in sw.js
- Ensure pwa.js is loaded

### Manifest Not Loading

**Check**:
1. Manifest link in HTML head
2. File exists at `/manifest.webmanifest`
3. Correct MIME type: `application/manifest+json`
4. No JSON syntax errors

**Solution**:
- View page source to verify link tag
- Access manifest URL directly in browser
- Validate JSON syntax
- Check server MIME type configuration

### Icons Not Appearing

**Check**:
1. Icon files exist at specified paths
2. Paths are absolute in manifest
3. Icon dimensions match manifest
4. PNG format and valid image files

**Solution**:
- Verify icon URLs directly in browser
- Check file permissions on server
- Regenerate icons if corrupted
- Clear browser cache and reinstall

## References

- [MDN Web App Manifests](https://developer.mozilla.org/en-US/docs/Web/Manifest)
- [Service Worker API](https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API)
- [Chrome PWA Install Criteria](https://web.dev/install-criteria/)
- [Maskable Icons](https://web.dev/maskable-icon/)

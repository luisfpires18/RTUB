# PWA Setup and Configuration

This document explains the Progressive Web App (PWA) setup for RTUB and how to package it as a mobile application for Android and iOS.

> **Note**: For iOS App Store submission, see the dedicated [iOS App Store Guide](ios-app-store-guide.md).

## Table of Contents

- [Overview](#overview)
- [PWA Components](#pwa-components)
- [Local Development and Testing](#local-development-and-testing)
- [Production Deployment](#production-deployment)
- [Android Packaging (TWA via PWABuilder)](#android-packaging-twa-via-pwabuilder)
- [Troubleshooting](#troubleshooting)

## Overview

RTUB is configured as a production-ready Progressive Web App with the following capabilities:

- **Offline Support**: Service Worker caches core assets for offline functionality
- **Installable**: Users can install RTUB as a standalone app on their devices
- **App-like Experience**: Runs in standalone mode without browser UI
- **Push Notifications**: Integrated push notification support
- **Responsive Design**: Optimized for mobile, tablet, and desktop

## PWA Components

### 1. Web App Manifest

**Location**: `/wwwroot/manifest.webmanifest`

The manifest defines how the app appears when installed:

```json
{
  "id": "/",
  "name": "RTUB - Real Tuna Universitária de Bragança",
  "short_name": "RTUB",
  "description": "Real Tuna Universitária de Bragança - Plataforma de Gestão",
  "theme_color": "#3F2A86",
  "background_color": "#ffffff",
  "display": "standalone",
  "scope": "/",
  "start_url": "/",
  "categories": ["music", "education", "lifestyle", "entertainment"],
  "icons": [...]
}
```

**Key Fields**:
- `id`: Unique identifier for the PWA (required for TWA)
- `name`: Full app name displayed during installation
- `short_name`: Name shown on home screen (limited space)
- `start_url`: URL opened when app is launched
- `display: standalone`: Runs without browser UI
- `scope`: Defines navigation boundaries
- `theme_color`: App header/toolbar color
- `background_color`: Splash screen background
- `icons`: Multiple sizes (192x192 and 512x512 required)

### 2. Service Worker

**Location**: `/wwwroot/service-worker.js`

The service worker handles:

- **Install Event**: Caches critical assets on first install
- **Activate Event**: Cleans up old cache versions
- **Fetch Event**: Implements caching strategies
  - Network-first for HTML/documents (with cache fallback)
  - Cache-first for images (with network fallback)
  - Stale-while-revalidate for CSS/JS
- **Push Notifications**: Handles incoming push messages
- **Notification Clicks**: Routes users to relevant pages

**Caching Strategy**:
```javascript
// Static assets cached on install
const STATIC_ASSETS = [
    '/',
    '/icons/rtub-logo-192.png',
    '/icons/rtub-logo-512.png',
    '/images/default-avatar.webp',
    '/manifest.webmanifest'
];
```

**Cache Versioning**: Increment `CACHE_VERSION` when updating assets:
```javascript
const CACHE_VERSION = 'rtub-v3';
```

### 3. Service Worker Registration

**Location**: `/wwwroot/js/sw-register.js`

Universal service worker registration that:
- Checks browser support for Service Workers
- Registers `/service-worker.js` on page load
- Listens for service worker updates
- Checks for updates periodically (every hour)

**Included in**: `Shared/MainLayout.razor`

### 4. App Icons

**Location**: `/wwwroot/icons/`

Icon sizes available:
- 16x16, 32x32 (favicons)
- 120x120, 152x152, 167x167, 180x180 (iOS)
- 192x192 (Android standard, maskable)
- 256x256, 384x384, 512x512 (Android, maskable)

All icons are PNG format with transparent backgrounds.

### 5. Static File Configuration

**Location**: `Program.cs`

Configures proper MIME types and caching:
```csharp
// Configure content type provider to serve .webmanifest with correct MIME type
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".webmanifest"] = "application/manifest+json";

// PWA icons and manifest: 1 hour cache with must-revalidate
// Other static files: 30 days cache in production
```

## Local Development and Testing

### Running the Application

1. **Build the project**:
   ```bash
   cd src/RTUB.Web
   dotnet build
   ```

2. **Run in development mode**:
   ```bash
   dotnet run
   ```

3. **Access the application**:
   - HTTPS: `https://localhost:5001`
   - HTTP: `http://localhost:5000` (will redirect to HTTPS)

### Testing PWA Features

#### Chrome DevTools

1. **Open DevTools**: Press `F12` or right-click → Inspect
2. **Navigate to Application Tab**
3. **Check PWA Status**:
   - **Manifest**: View parsed manifest and icons
   - **Service Workers**: View registration status and cache
   - **Storage**: Inspect cached assets
   - **Lighthouse**: Run PWA audit

#### Lighthouse PWA Audit

1. Open Chrome DevTools → Lighthouse tab
2. Select "Progressive Web App" category
3. Click "Analyze page load"
4. Review PWA checklist and scores

**PWA Checklist**:
- ✅ Registers a service worker
- ✅ Responds with 200 when offline
- ✅ Has a manifest with required fields
- ✅ Has valid icons (192x192, 512x512)
- ✅ Themed for mobile
- ✅ Sets viewport meta tag

#### Testing Offline Functionality

1. Open DevTools → Application → Service Workers
2. Check "Offline" checkbox
3. Reload the page
4. Verify cached pages and assets load correctly
5. Test navigation between cached pages

#### Testing Installation

**Desktop (Chrome/Edge)**:
1. Look for install icon in address bar (⊕ Install)
2. Click to install
3. App opens in standalone window
4. Find installed app in Start Menu / Applications

**Mobile (Chrome/Safari)**:
1. Open browser menu
2. Select "Add to Home Screen" or "Install App"
3. Customize name if desired
4. Tap "Add" or "Install"
5. Find app icon on home screen

#### Simulating Mobile Device

1. Open DevTools → Device Toolbar (Ctrl+Shift+M)
2. Select device or use Responsive mode
3. Test touch interactions and viewport behavior
4. Test installation flow on mobile

### Common Development Issues

**Service Worker Not Updating**:
- Go to DevTools → Application → Service Workers
- Click "Unregister" to remove old service worker
- Hard reload: Ctrl+Shift+R (Windows/Linux) or Cmd+Shift+R (Mac)
- Clear cache: DevTools → Application → Clear Storage → Clear site data

**Manifest Not Recognized**:
- Verify manifest is accessible: `https://localhost:5001/manifest.webmanifest`
- Check DevTools → Application → Manifest for parsing errors
- Ensure manifest link in `App.razor`: `<link rel="manifest" href="/manifest.webmanifest" />`

**Icons Not Loading**:
- Verify icon paths in manifest match actual file locations
- Check icon files exist in `/wwwroot/icons/`
- Ensure icons are PNG format with correct sizes

## Production Deployment

### Requirements

1. **HTTPS is MANDATORY**: PWAs require secure context (HTTPS)
   - Service Workers only work over HTTPS (or localhost for development)
   - Push notifications require HTTPS
   - Install prompts require HTTPS

2. **Valid SSL Certificate**:
   - Use Let's Encrypt, Cloudflare, or commercial SSL provider
   - Ensure certificate is trusted by browsers

3. **Proper Hosting Configuration**:
   - Serve manifest with correct MIME type: `application/manifest+json`
   - Set appropriate cache headers
   - Enable HTTPS redirection

### Deployment Checklist

- [ ] Deploy application to production server
- [ ] Configure HTTPS with valid SSL certificate
- [ ] Verify manifest is accessible: `https://yourdomain.com/manifest.webmanifest`
- [ ] Verify service worker is accessible: `https://yourdomain.com/service-worker.js`
- [ ] Test PWA installation on production URL
- [ ] Run Lighthouse PWA audit on production URL
- [ ] Verify offline functionality works in production

### Azure Deployment (Example)

1. **Publish the application**:
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **Deploy to Azure App Service**:
   - Create App Service with HTTPS enabled
   - Deploy files to App Service
   - Configure custom domain (optional)
   - Configure SSL certificate

3. **Configure App Service**:
   - Enable "HTTPS Only" in Configuration → General settings
   - Set minimum TLS version to 1.2
   - Configure connection strings and app settings

4. **Verify Deployment**:
   - Access `https://yourdomain.com`
   - Test PWA features
   - Run Lighthouse audit

### Configuration for Production

**appsettings.Production.json**:
```json
{
  "ConnectionStrings": {
    "SqliteConnection": "Data Source=/path/to/production/app.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "Cloudflare": {
    "R2": {
      "AccountId": "your-account-id",
      "BucketName": "your-bucket"
    }
  }
}
```

**Environment Variables** (recommended for secrets):
- `Cloudflare__R2__AccessKeyId`
- `Cloudflare__R2__SecretAccessKey`
- `AdminUser__Email`
- `AdminUser__Password`
- `EmailSettings__SmtpUsername`
- `EmailSettings__SmtpPassword`

## Android Packaging (TWA via PWABuilder)

Trusted Web Activities (TWA) allow you to package your PWA as a native Android app that can be published on Google Play Store.

### Prerequisites

1. **Production PWA URL**: App must be deployed and accessible over HTTPS
2. **Google Developer Account**: Required to publish on Play Store ($25 one-time fee)
3. **PWA Requirements Met**: Pass Lighthouse PWA audit

### Step-by-Step Guide

#### 1. Prepare Your PWA

1. Deploy RTUB to production server with HTTPS
2. Verify PWA works correctly:
   - Run Lighthouse audit (score > 80 recommended)
   - Test installation on mobile device
   - Verify offline functionality
3. Note your production URL (e.g., `https://rtub.azurewebsites.net`)

#### 2. Use PWABuilder

1. **Go to PWABuilder**: Visit [https://www.pwabuilder.com](https://www.pwabuilder.com)

2. **Enter Your URL**: Input your production URL (e.g., `https://rtub.azurewebsites.net`)

3. **Generate Report**: PWABuilder will analyze your PWA and generate a report
   - Review any warnings or errors
   - Ensure manifest and service worker are detected
   - Check PWA score and recommendations

4. **Configure Android Package**:
   - Click "Package for Stores" → "Android"
   - Configure app details:
     - **Package ID**: `com.rtub.app` (or `com.braganca.rtub`)
     - **App Name**: "RTUB"
     - **App Version**: `1.0.0` (follows semantic versioning)
     - **Version Code**: `1` (increment with each release)
     - **Host**: Your production domain
     - **Start URL**: `/` (or specific start path)

5. **Advanced Options** (optional):
   - Splash screen settings
   - Theme color customization
   - Status bar appearance
   - Navigation bar color
   - Full screen mode
   - Notification settings
   - App shortcuts

6. **Generate Package**:
   - Click "Generate"
   - PWABuilder generates Android App Bundle (.aab)
   - Download the generated package
   - Download signing key (keep secure!)

#### 3. Package Configuration Details

**Recommended Package ID Structure**:
- Format: `com.company.appname`
- Example: `com.rtub.app` or `com.braganca.rtub`
- Must be unique in Google Play Store
- Use reverse domain notation

**App Metadata**:
- **Name**: "RTUB" or "Real Tuna Universitária de Bragança"
- **Short Name**: "RTUB"
- **Description**: "Plataforma de Gestão da Real Tuna Universitária de Bragança"
- **Category**: Music, Education, or Lifestyle

#### 4. Digital Asset Links (Important!)

TWA requires a Digital Asset Link file to verify ownership of your domain.

1. PWABuilder generates an `assetlinks.json` file
2. Upload this file to your server at: `https://yourdomain.com/.well-known/assetlinks.json`
3. Verify the file is publicly accessible
4. The file associates your Android app with your website

**Example assetlinks.json**:
```json
[{
  "relation": ["delegate_permission/common.handle_all_urls"],
  "target": {
    "namespace": "android_app",
    "package_name": "com.rtub.app",
    "sha256_cert_fingerprints": ["YOUR_APP_FINGERPRINT"]
  }
}]
```

#### 5. Signing Your App

PWABuilder generates a signing key for you:

1. **Download and secure the key**: Store in a safe location (you'll need it for updates)
2. **Key Information**:
   - Keystore file: `signing-key.keystore`
   - Key alias: Usually `my-key`
   - Passwords: Generated by PWABuilder (store securely)

**Important**: If you lose your signing key, you cannot update your app on Play Store!

#### 6. Publishing to Google Play Store

1. **Create Developer Account**:
   - Go to [Google Play Console](https://play.google.com/console)
   - Pay one-time registration fee ($25)
   - Complete account setup

2. **Create New App**:
   - Click "Create app"
   - Fill in app details:
     - App name: "RTUB"
     - Default language: Portuguese (Portugal)
     - App type: App
     - Category: Music or Education
     - Is it free or paid: Free

3. **Upload App Bundle**:
   - Go to Release → Production → Create new release
   - Upload the `.aab` file from PWABuilder
   - Fill in release notes (what's new)

4. **Configure Store Listing**:
   - **App name**: "RTUB"
   - **Short description**: Brief description (up to 80 characters)
   - **Full description**: Detailed description
   - **Screenshots**: Required for phone, tablet (7-inch and 10-inch)
   - **Feature graphic**: 1024 x 500 px banner image
   - **App icon**: 512 x 512 px (use existing RTUB logo)
   - **Privacy policy**: URL to privacy policy
   - **Contact details**: Email, website, phone

5. **Content Rating**:
   - Complete questionnaire
   - Get appropriate age rating

6. **App Content**:
   - Declare if app contains ads
   - Target audience
   - Content guidelines compliance

7. **Submit for Review**:
   - Review all sections
   - Submit app for review
   - Review typically takes 1-7 days

#### 7. Testing Before Release

1. **Internal Testing Track**:
   - Upload app to internal testing track
   - Invite team members to test
   - Verify app works correctly

2. **Closed Testing (Beta)**:
   - Create closed testing track
   - Invite testers via email
   - Gather feedback

3. **Open Testing (Public Beta)**:
   - Optional public beta testing
   - Collect user feedback before full launch

### Updating Your Android App

When you update your PWA:

1. **Update your website**: Deploy updated PWA to production
2. **Test changes**: Verify PWA works correctly
3. **Generate new package**:
   - Use PWABuilder with updated URL
   - Use same package ID and signing key
   - Increment version code (e.g., 1 → 2)
4. **Upload to Play Store**: Create new release with updated .aab
5. **Submit for review**: New version goes through review process

### TWA Advantages

- **True Native App**: Appears as native app in Play Store and device
- **Automatic Updates**: App updates when website is updated
- **No App Store Review for Content**: Only initial app review required
- **Shared Storage**: Shares cookies, local storage with PWA
- **Full Screen Support**: True full-screen experience
- **Push Notifications**: Native Android notifications

### TWA Considerations

- **Requires HTTPS**: Production PWA must use HTTPS
- **Digital Asset Links**: Must be properly configured
- **Internet Required**: Offline functionality depends on service worker caching
- **Review Process**: Initial submission requires Google review
- **Size**: App package is small (~1-2 MB) since it loads web content

## Troubleshooting

### Service Worker Issues

**Problem**: Service worker not registering
- **Solution**: Check browser console for errors
- Verify service worker file is accessible: `/service-worker.js`
- Ensure HTTPS is enabled (or using localhost)
- Clear browser cache and hard reload

**Problem**: Service worker not updating
- **Solution**: Increment `CACHE_VERSION` in `service-worker.js`
- Unregister old service worker in DevTools
- Force update with "Update on reload" in DevTools

### Manifest Issues

**Problem**: Install prompt not showing
- **Solution**: Verify manifest is valid
- Check all required fields are present
- Ensure icons are correct sizes and format
- Run Lighthouse audit to identify issues

**Problem**: Icons not displaying correctly
- **Solution**: Verify icon paths in manifest
- Ensure icons are PNG format
- Check file sizes match declared sizes
- Test on actual device (not just simulator)

### Offline Issues

**Problem**: App doesn't work offline
- **Solution**: Check service worker is active
- Verify fetch event handlers are working
- Inspect cached assets in DevTools → Application → Cache Storage
- Test with "Offline" mode in DevTools

### Push Notification Issues

**Problem**: Push notifications not working
- **Solution**: Verify push notification configuration
- Check VAPID keys are configured
- Ensure notification permission is granted
- Test on actual device (some features don't work in simulators)

### Android TWA Issues

**Problem**: App not verified by Play Store
- **Solution**: Verify Digital Asset Links file is accessible
- Check `assetlinks.json` has correct package name and fingerprint
- Ensure file is at `/.well-known/assetlinks.json`
- Verify HTTPS certificate is valid

**Problem**: App opens in browser instead of TWA
- **Solution**: Verify Digital Asset Links configuration
- Check package ID matches in manifest and assetlinks.json
- Wait 15-30 minutes after uploading assetlinks.json
- Clear app data and reinstall

## Additional Resources

- [PWA Documentation (MDN)](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps)
- [Service Worker API](https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API)
- [Web App Manifest](https://developer.mozilla.org/en-US/docs/Web/Manifest)
- [PWABuilder](https://www.pwabuilder.com)
- [Google Play Console](https://play.google.com/console)
- [Lighthouse](https://developers.google.com/web/tools/lighthouse)
- [Trusted Web Activities](https://developers.google.com/web/android/trusted-web-activity)

## Support

For issues or questions:
- Check project documentation in `/docs`
- Review closed issues on GitHub
- Open a new issue with detailed description and steps to reproduce
- Contact project maintainer: [@luisfpires18](https://github.com/luisfpires18)

---

**Last Updated**: December 2025
**Version**: 1.0.0

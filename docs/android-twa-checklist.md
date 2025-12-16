# Android TWA Deployment Checklist

Quick reference checklist for deploying RTUB as an Android app via Trusted Web Activities (TWA).

## Pre-Deployment Checklist

### 1. Production Environment Setup
- [ ] App deployed to production server
- [ ] HTTPS enabled with valid SSL certificate
- [ ] Custom domain configured (if using)
- [ ] App accessible at production URL (e.g., `https://rtub.azurewebsites.net`)

### 2. PWA Validation
- [ ] Run Lighthouse PWA audit on production URL
  - Score should be > 80
  - All PWA criteria should be met
- [ ] Test manifest is accessible: `https://yourdomain.com/manifest.webmanifest`
- [ ] Test service worker is accessible: `https://yourdomain.com/service-worker.js`
- [ ] Test app installation on mobile device
- [ ] Test offline functionality

### 3. PWA Components Verification
- [ ] Manifest contains `id` field: ✅ (Set to "/")
- [ ] Manifest has 192x192 icon: ✅
- [ ] Manifest has 512x512 icon: ✅
- [ ] Service worker registers successfully: ✅
- [ ] Caching strategies working: ✅
- [ ] Theme color matches app: ✅ (#3F2A86)

## PWABuilder Setup

### 1. Generate Android Package
1. Go to [https://www.pwabuilder.com](https://www.pwabuilder.com)
2. Enter production URL: `https://rtub.azurewebsites.net` (or your domain)
3. Click "Start"
4. Review PWA report
5. Click "Package for Stores" → "Android"

### 2. Configure Package Details
Fill in these details:

#### Package Information
- **Package ID**: `com.braganca.rtub` (or `com.rtub.app`)
- **App Name**: `RTUB`
- **App Version**: `1.0.0`
- **Version Code**: `1`

#### URLs
- **Host**: `rtub.azurewebsites.net` (your domain without https://)
- **Start URL**: `/`

#### Display Options
- **Theme Color**: `#3F2A86` (matches manifest)
- **Background Color**: `#ffffff`
- **Display Mode**: `standalone`
- **Orientation**: `portrait-primary`

#### Advanced Options (Optional)
- **Splash Screen**: Auto-generated from icons
- **Status Bar**: Black translucent
- **Navigation Bar Color**: Match theme color
- **Full Screen**: Off (keep for better UX)
- **App Shortcuts**: Can add later

### 3. Generate Package
1. Click "Generate"
2. Wait for package generation
3. Download the `.aab` file (Android App Bundle)
4. Download the signing key (keep this secure!)
5. Download `assetlinks.json` file

## Digital Asset Links Setup

### 1. Upload Asset Links File
1. Place `assetlinks.json` in your web server at:
   ```
   https://yourdomain.com/.well-known/assetlinks.json
   ```

2. Ensure the file is publicly accessible (no authentication required)

3. Verify the file is accessible:
   ```bash
   curl https://yourdomain.com/.well-known/assetlinks.json
   ```

4. File should contain something like:
   ```json
   [{
     "relation": ["delegate_permission/common.handle_all_urls"],
     "target": {
       "namespace": "android_app",
       "package_name": "com.braganca.rtub",
       "sha256_cert_fingerprints": ["XX:XX:XX:..."]
     }
   }]
   ```

### 2. Configure Web Server
If using ASP.NET Core, ensure `.well-known` directory is served:

```csharp
// Program.cs - already configured
app.UseStaticFiles();
```

Create directory structure:
```
wwwroot/
└── .well-known/
    └── assetlinks.json
```

## Google Play Console Setup

### 1. Create Developer Account
- [ ] Register at [Google Play Console](https://play.google.com/console)
- [ ] Pay one-time registration fee ($25)
- [ ] Complete account setup

### 2. Create New App
1. Click "Create app"
2. Fill in details:
   - **App name**: RTUB
   - **Default language**: Portuguese (Portugal)
   - **App or game**: App
   - **Free or paid**: Free
3. Accept declarations
4. Click "Create app"

### 3. Upload App Bundle
1. Go to **Release** → **Production** → **Create new release**
2. Upload `.aab` file from PWABuilder
3. Add release notes:
   ```
   Initial release of RTUB - Real Tuna Universitária de Bragança mobile app.
   Manage events, rehearsals, members, and more on the go.
   ```

### 4. Configure Store Listing

#### App Details
- **App name**: RTUB - Real Tuna Universitária de Bragança
- **Short description** (max 80 chars):
  ```
  Gestão da Real Tuna Universitária de Bragança
  ```
- **Full description** (max 4000 chars):
  ```
  RTUB é a aplicação oficial da Real Tuna Universitária de Bragança.
  
  Funcionalidades:
  • Gestão de eventos e atuações
  • Calendário de ensaios
  • Galeria de fotos
  • Repertório musical
  • Gestão de membros
  • Sistema de mensagens
  • Inventário de instrumentos
  • E muito mais!
  
  Aplicação desenvolvida especialmente para membros da tuna universitária.
  ```

#### Graphics
Required assets (create before submission):

- [ ] **App icon**: 512 x 512 px (use existing RTUB logo)
- [ ] **Feature graphic**: 1024 x 500 px
- [ ] **Phone screenshots**: At least 2 (max 8)
  - Size: 1080 x 1920 px (portrait) or 1920 x 1080 px (landscape)
- [ ] **7-inch tablet screenshots**: At least 2 (optional but recommended)
- [ ] **10-inch tablet screenshots**: At least 2 (optional but recommended)

#### Contact Details
- **Email**: (your support email)
- **Website**: `https://rtub.azurewebsites.net`
- **Phone**: (optional)

#### Privacy Policy
- **Privacy policy URL**: (required - create a simple privacy policy page)

#### Category & Tags
- **App category**: Music
- **Tags**: music, university, tuna, events, management

### 5. Content Rating
1. Click **Content rating** → **Start questionnaire**
2. Select **Other**
3. Answer questions honestly:
   - No violence
   - No user-generated content (unless you have moderation)
   - No sharing location
   - etc.
4. Submit questionnaire
5. Receive rating certificate

### 6. App Content
Complete these sections:

#### Privacy Policy
- [ ] Add privacy policy URL

#### Ads
- [ ] Declare if app contains ads (No)

#### Target Audience
- [ ] Select age groups (13+)

#### News Apps
- [ ] Not a news app

#### COVID-19 Contact Tracing
- [ ] Not a contact tracing app

#### Data Safety
- [ ] Complete data safety form
  - Data collection: Minimal (email, name if required)
  - Data sharing: None
  - Security practices: Data encrypted in transit

### 7. Submit for Review
1. Review all sections (must be complete)
2. Click **Submit for review**
3. Wait for review (typically 1-7 days)

## Post-Submission

### Monitor Review Status
- [ ] Check Play Console for review status
- [ ] Respond to any review feedback within 7 days
- [ ] Address any issues raised by reviewers

### After Approval
- [ ] App appears in Google Play Store
- [ ] Test installation from Play Store
- [ ] Monitor crash reports and ratings
- [ ] Respond to user reviews

## Updating Your App

When you update the website:

1. **Update PWA**:
   - Deploy updated website to production
   - Increment `CACHE_VERSION` in service-worker.js if needed
   - Test PWA functionality

2. **Update Android App** (if needed):
   - Generate new package from PWABuilder
   - Increment version code (e.g., 1 → 2)
   - Use same signing key
   - Upload new release to Play Console
   - Submit for review

3. **Content Updates**:
   - Most content updates are automatic (TWA loads web content)
   - Only rebuild Android app for:
     - Manifest changes (name, icons, theme)
     - URL changes
     - Major feature additions requiring testing

## Important Notes

### Signing Key Management
⚠️ **CRITICAL**: Keep your signing key secure!
- Store in multiple safe locations
- Never share publicly
- Losing it means you can't update your app

### Digital Asset Links
- Must be accessible without authentication
- Changes take 15-30 minutes to propagate
- Test verification before submitting to Play Store

### Package ID
- Must be unique across Google Play
- Cannot be changed after first release
- Use reverse domain notation: `com.domain.app`

### Version Numbers
- Version Name: User-visible (e.g., "1.0.0")
- Version Code: Internal counter (1, 2, 3...)
- Must increment Version Code for each release

## Recommended Package Settings

```
Package ID: com.braganca.rtub
App Name: RTUB
Version: 1.0.0
Version Code: 1
Host: rtub.azurewebsites.net
Start URL: /
Theme Color: #3F2A86
Display: standalone
Orientation: portrait-primary
```

## Support Resources

- **PWABuilder Docs**: https://docs.pwabuilder.com/
- **Google Play Console**: https://play.google.com/console
- **TWA Documentation**: https://developers.google.com/web/android/trusted-web-activity
- **Asset Links Tester**: https://developers.google.com/digital-asset-links/tools/generator

## Troubleshooting

### App Opens in Browser Instead of TWA

**This is the most common issue!** See the complete troubleshooting guide: **[TWA Configuration Guide](twa-configuration-guide.md)**

Quick checklist:
- [ ] Digital Asset Links file exists at `/.well-known/assetlinks.json`
- [ ] File returns HTTP 200 with `Content-Type: application/json`
- [ ] Package name matches exactly between app and assetlinks.json
- [ ] SHA-256 fingerprint is from "App signing key certificate" (NOT upload key)
- [ ] Waited 15-30 minutes after uploading assetlinks.json
- [ ] Cleared app data AND Chrome cache
- [ ] No domain redirects (check with `curl -L -I https://rtub.azurewebsites.net`)

**Detailed Solution**: Follow the step-by-step guide in [twa-configuration-guide.md](twa-configuration-guide.md)

### Review Rejected
- Read rejection reason carefully
- Address all issues mentioned
- Resubmit with explanation of changes
- If unclear, contact Play Console support

### Installation Issues
- Verify HTTPS is working
- Check manifest is accessible
- Test PWA in Chrome mobile first
- Verify service worker registers successfully

---

**For detailed technical information**, see:
- **[TWA Configuration Guide](twa-configuration-guide.md)** - Digital Asset Links setup and troubleshooting
- **[PWA Setup Guide](pwa-setup.md)** - PWA development and testing

**Last Updated**: December 2025

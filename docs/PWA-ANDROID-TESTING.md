# PWA Android Testing Guide

This guide provides step-by-step instructions for testing the RTUB PWA (Progressive Web App) installation on Android devices.

## Prerequisites

- Android device with Chrome, Edge, or Samsung Internet browser
- Access to https://rtub.azurewebsites.net
- (Optional) USB cable for remote debugging

## Testing on Chrome for Android

### 1. Basic Installation Test

1. Open **Chrome** on your Android device
2. Navigate to `https://rtub.azurewebsites.net`
3. Wait for the page to fully load
4. Tap the three-dot menu (⋮) in the top-right corner
5. Look for one of these options:
   - **"Install app"** (preferred - indicates full PWA support)
   - **"Add to Home screen"** (fallback option)
6. Tap the install option
7. Confirm the installation when prompted
8. Check your home screen for the RTUB icon

**Expected Result:**
- The icon should display the RTUB logo (not a generic browser icon)
- The icon should have a clean appearance without white margins
- The icon should be properly sized and not pixelated

### 2. Launch Behavior Test

1. Tap the RTUB icon on your home screen
2. The app should launch in standalone mode (no browser UI visible)
3. The status bar should match the app's theme color (#3F2A86 - purple)

### 3. Remote Debugging (Optional but Recommended)

If the icon doesn't appear correctly, use Chrome DevTools for debugging:

1. On your computer, open Chrome and navigate to `chrome://inspect`
2. Connect your Android device via USB
3. Enable USB debugging on your Android device
4. Find your device in the Chrome inspect list
5. Click **"Inspect"** next to the RTUB tab
6. In DevTools, navigate to **Application** → **Manifest**
7. Review the manifest details:
   - Verify `start_url` is `/`
   - Verify `scope` is `/`
   - Verify `display` is `standalone`
   - Check that icons show 192x192 and 512x512 entries
   - Look for any warnings or errors

**Common Issues:**
- If icons show as broken: check icon file paths
- If manifest shows errors: verify manifest.webmanifest is served with correct MIME type
- If "Install app" doesn't appear: ensure HTTPS and service worker are active

## Testing on Samsung Internet

### 1. Basic Installation Test

1. Open **Samsung Internet** browser
2. Navigate to `https://rtub.azurewebsites.net`
3. Wait for the page to fully load
4. Tap the menu button (three lines or three dots)
5. Select **"Add page to"** → **"Home screen"**
6. Confirm the installation
7. Check your home screen for the RTUB icon

**Expected Result:**
- Same as Chrome testing - RTUB logo should appear clearly

## Testing on Microsoft Edge for Android

### 1. Basic Installation Test

1. Open **Microsoft Edge** on your Android device
2. Navigate to `https://rtub.azurewebsites.net`
3. Wait for the page to fully load
4. Tap the three-dot menu
5. Look for **"Add to phone"** or **"Install app"**
6. Follow the installation prompts
7. Check your home screen for the RTUB icon

## Manifest Validation

### Online Validation Tools

Use these tools to validate the manifest before testing on a device:

1. **Chrome DevTools** (on desktop):
   - Open https://rtub.azurewebsites.net in Chrome
   - Open DevTools (F12)
   - Go to Application → Manifest
   - Review all fields and check for warnings

2. **Lighthouse** (in Chrome DevTools):
   - Run a Lighthouse audit
   - Check the PWA section
   - Should pass "installable" criteria

3. **Web.dev Measure**:
   - Visit https://web.dev/measure/
   - Enter the URL
   - Review PWA score

### Manual Manifest Check

View the manifest directly:
- Navigate to: https://rtub.azurewebsites.net/manifest.webmanifest
- Verify the JSON structure is valid
- Check icon paths are absolute and correct

## Service Worker Verification

### Check Service Worker Status

1. Open Chrome DevTools (desktop or remote debugging)
2. Go to **Application** → **Service Workers**
3. Verify that a service worker is registered for the origin
4. Status should show "activated and running"
5. Check that `/sw.js` is the source

### Console Check

1. Open the browser console on the device (via remote debugging)
2. Look for the service worker registration message:
   ```
   Service Worker registered successfully: https://rtub.azurewebsites.net/
   ```
3. Verify no errors related to service worker registration

## Troubleshooting

### Icon Not Showing Correctly

**Symptoms:**
- Generic browser icon appears instead of RTUB logo
- Icon has white background or margins
- Icon looks pixelated

**Solutions:**
1. Check icon files exist:
   - https://rtub.azurewebsites.net/icons/rtub-logo-192.png
   - https://rtub.azurewebsites.net/icons/rtub-logo-512.png
2. Verify icons have transparent or solid background (no white margins)
3. Clear browser cache and try again
4. Uninstall the app from home screen and reinstall

### "Install App" Option Not Appearing

**Possible Causes:**
- Site not served over HTTPS (required for PWA)
- Manifest not linked correctly in HTML
- Service worker not registered
- Manifest contains errors
- Site already installed

**Solutions:**
1. Verify HTTPS is working
2. Check manifest link in page source: `<link rel="manifest" href="/manifest.webmanifest">`
3. Verify service worker is registered (check DevTools)
4. Check for manifest errors in DevTools Application tab
5. Try uninstalling if already installed

### App Not Opening in Standalone Mode

**Symptoms:**
- Browser UI still visible when opening from home screen
- App opens in a regular browser tab

**Solutions:**
1. Verify `display: "standalone"` in manifest
2. Check `start_url` is correct
3. Reinstall the app

## Icon Specifications

The RTUB PWA uses the following icon configuration:

- **192x192 pixels**: Required for Android home screen
  - Purpose: "any" (default display)
  - Purpose: "maskable" (adaptive icon support)
- **512x512 pixels**: Required for splash screen
  - Purpose: "any" (default display)
  - Purpose: "maskable" (adaptive icon support)

### Icon Design Guidelines

For optimal display on Android:
- Use solid background color (avoid transparency for maskable icons)
- Center the logo with adequate padding for maskable support
- Ensure logo is clearly visible at small sizes
- No white margins or borders
- High resolution (no pixelation at 512x512)

## Expected PWA Capabilities

After successful installation, the RTUB PWA should:

1. ✅ Install to home screen with RTUB logo
2. ✅ Launch in standalone mode (no browser UI)
3. ✅ Display purple theme color in status bar
4. ✅ Work offline (basic functionality via service worker cache)
5. ✅ Update automatically when new versions are deployed

## Reporting Issues

If testing reveals issues, report them with:

1. Device model and Android version
2. Browser name and version
3. Screenshot of the icon on home screen
4. Screenshot of any error messages in DevTools
5. Description of unexpected behavior
6. Steps to reproduce

## References

- [Web.dev PWA Guidelines](https://web.dev/progressive-web-apps/)
- [Android PWA Documentation](https://developer.chrome.com/docs/android/trusted-web-activity/)
- [MDN Web Manifest](https://developer.mozilla.org/en-US/docs/Web/Manifest)

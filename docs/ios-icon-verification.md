# iOS Home-Screen Icon Verification Steps

## Overview
This document describes how to verify that the RTUB PWA home-screen icon displays correctly on iOS devices (iPhone/iPad) when using Safari or Chrome's "Add to Home Screen" feature.

## Prerequisites
- iPhone or iPad with iOS 13 or later
- Access to https://rtub.azurewebsites.net
- Safari browser (built-in)
- Chrome browser for iOS (optional, for Chrome testing)

## Verification Steps

### Step 1: Clear Previous Data
1. **Remove any existing RTUB home-screen icon:**
   - Long-press the RTUB app icon on your home screen
   - Tap "Remove App" → "Delete App" to completely remove it

2. **Clear website data in Safari:**
   - Open iOS **Settings** app
   - Scroll down and tap **Safari**
   - Scroll down and tap **Advanced**
   - Tap **Website Data**
   - Search for "rtub.azurewebsites.net"
   - Swipe left and tap **Delete** (or use "Remove All Website Data" if you prefer)

### Step 2: Test in Safari
1. **Open the site:**
   - Open Safari on your iPhone
   - Navigate to https://rtub.azurewebsites.net
   - Wait for the page to fully load

2. **Add to Home Screen:**
   - Tap the **Share** button (square with arrow pointing up) at the bottom of Safari
   - Scroll down and tap **"Add to Home Screen"**
   - **VERIFY:** The icon preview in the dialog should show the RTUB logo (purple background with "RTUB" text), NOT a generic purple tile with the letter "A"
   - Tap **Add** to confirm

3. **Verify the installed icon:**
   - Go to your home screen
   - Find the RTUB icon
   - **VERIFY:** The icon should display the RTUB logo clearly, with proper branding

### Step 3: Test in Chrome (Optional)
1. **Clear Chrome data (if you previously added RTUB):**
   - Open Chrome for iOS
   - Go to Settings → Privacy and Security → Clear Browsing Data
   - Select "Cookies, Site Data" and clear for rtub.azurewebsites.net

2. **Open the site:**
   - Open Chrome for iOS
   - Navigate to https://rtub.azurewebsites.net
   - Wait for the page to fully load

3. **Add to Home Screen:**
   - Tap the **Share** button (square with arrow pointing up)
   - Tap **"Add to Home Screen"**
   - **VERIFY:** The icon preview should show the RTUB logo
   - Tap **Add** to confirm

4. **Verify the installed icon:**
   - Go to your home screen
   - **VERIFY:** The Chrome-installed icon should also display the RTUB logo

## What Should You See?

### ✅ Expected Result (Correct)
- Icon displays the RTUB logo
- Purple background color (#3F2A86)
- Clear, recognizable branding
- Icon looks professional and matches the desktop favicon

### ❌ Previous Issue (Incorrect)
- Generic purple tile with just the letter "A"
- No RTUB branding visible
- Poor user experience

## Technical Details

### Files Changed
- **Program.cs**: Extended static file caching to include `manifest.webmanifest` with 1-hour cache duration

### Key Files Verified
- **App.razor**: HeadOutlet component present (line 32)
- **MainLayout.razor**: Apple touch icon link present (line 233)
- **wwwroot/icons/rtub-logo-180.png**: 180×180 PNG icon with no transparency
- **wwwroot/manifest.webmanifest**: Correct PWA manifest with icon references
- **Program.cs**: Static file caching configured for PWA files (lines 409-414)

### Icon Specifications Met
- ✅ Format: PNG
- ✅ Size: 180×180 pixels (exact)
- ✅ Transparency: None (RGB, no alpha channel)
- ✅ Shape: Square (no rounded corners - iOS applies its own rounding)
- ✅ Branding: Consistent RTUB logo

## Troubleshooting

### If the icon still doesn't show correctly:

1. **Hard refresh the website:**
   - Pull down the Safari/Chrome page to refresh
   - Or close and reopen the browser tab

2. **Clear ALL website data:**
   - Go back to Step 1 and thoroughly clear all cached data

3. **Wait for CDN cache expiration:**
   - The manifest and icon files are now cached for 1 hour
   - If you just deployed, you may need to wait up to 1 hour or use cache-busting query parameters

4. **Check the manifest URL directly:**
   - Visit https://rtub.azurewebsites.net/manifest.webmanifest in Safari
   - Verify it loads without errors
   - Check that the JSON is valid

5. **Check the icon URL directly:**
   - Visit https://rtub.azurewebsites.net/icons/rtub-logo-180.png in Safari
   - Verify the image loads and displays correctly

6. **Verify no authentication issues:**
   - The icon and manifest must be accessible without login
   - Test in a private/incognito window to verify

## References
- [Apple Developer: Configuring Web Applications](https://developer.apple.com/library/archive/documentation/AppleApplications/Reference/SafariWebContent/ConfiguringWebApplications/ConfiguringWebApplications.html)
- [Web App Manifest Specification](https://www.w3.org/TR/appmanifest/)

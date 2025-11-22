# PWA Icon Troubleshooting Guide

This guide helps troubleshoot Progressive Web App (PWA) icon issues, particularly on iOS/iPhone devices.

## Background

RTUB uses custom RTUB logo icons for the PWA home screen shortcut. If you see the Azure default "A" icon or any other generic icon instead of the RTUB logo, this guide will help you resolve it.

## Why Icons May Not Update

1. **Aggressive Browser Caching**: iOS Safari caches icons very aggressively
2. **Previous Installations**: Old PWA installations may retain cached icons
3. **Service Worker Cache**: Service workers may cache old icon references

## iOS/iPhone: How to Clear Cache and Update Icons

### Method 1: Clear Safari History and Website Data (Most Reliable)

1. Open iPhone **Settings**
2. Scroll down and tap **Safari**
3. Scroll down and tap **Clear History and Website Data**
4. Tap **Clear History and Data** to confirm
5. Open Safari and navigate to the RTUB website
6. Delete any existing home screen shortcuts for RTUB
7. Tap the Share button (square with arrow pointing up)
8. Tap **Add to Home Screen**
9. The new RTUB logo should now appear

### Method 2: Clear Specific Website Data (Less Intrusive)

1. Open iPhone **Settings**
2. Scroll down and tap **Safari**
3. Scroll down and tap **Advanced**
4. Tap **Website Data**
5. Use the search bar to find "rtub" or your domain
6. Swipe left on the entry and tap **Delete**
7. Delete any existing home screen shortcuts for RTUB
8. Open Safari, navigate to RTUB, and add to home screen again

### Method 3: Toggle Airplane Mode (Alternative)

1. Delete the existing RTUB home screen shortcut
2. Turn on **Airplane Mode**
3. Try to open Safari (it will fail - this is expected)
4. Turn off **Airplane Mode**
5. Open Safari and navigate to RTUB
6. Add to Home Screen again

## Android/Chrome: How to Clear Cache

### Method 1: Clear Site Data

1. Open Chrome
2. Navigate to the RTUB website
3. Tap the three dots menu (⋮)
4. Tap **Settings**
5. Tap **Site settings**
6. Tap **All sites**
7. Find and tap on your RTUB domain
8. Tap **Clear & reset**
9. Uninstall the PWA if already installed
10. Reinstall from Chrome

### Method 2: Clear Browsing Data

1. Open Chrome
2. Tap the three dots menu (⋮)
3. Tap **Settings**
4. Tap **Privacy and security**
5. Tap **Clear browsing data**
6. Select **Cached images and files**
7. Tap **Clear data**
8. Uninstall and reinstall the PWA

## For Developers

### Recent Changes (Latest Fix)

The following improvements were made to ensure icons work properly:

1. **Removed apple-touch-icon-precomposed tags**: These can interfere with modern iOS
2. **Ensured default apple-touch-icon comes first**: iOS requires the default (without sizes) to be declared before sized variants
3. **Optimized manifest.json**: Added proper scope, orientation, and maskable icon support
4. **Reduced cache time for icons**: Icons now cache for 1 hour instead of 30 days, with must-revalidate directive

### Icon Specifications

- **iOS**: Uses 180×180 PNG (RGB, no alpha channel)
- **Android**: Uses 192×192 and 512×512 PNG
- **Format**: PNG (not SVG)
- **Transparency**: No alpha channel (RGB only)
- **Location**: `/wwwroot/icons/rtub-logo-*.png`

### Cache Headers

PWA icons and manifest.json are now served with:
```
Cache-Control: public,max-age=3600,must-revalidate
```

This ensures:
- Icons are cached for better performance
- Updates are picked up within 1 hour
- Browsers must revalidate with the server

### Testing Changes

After deploying icon changes:

1. **Clear browser cache** completely
2. **Hard refresh** the website (Ctrl+Shift+R / Cmd+Shift+R)
3. **Delete old PWA** installations
4. **Add to home screen** again
5. **Verify** the new icon appears

### Common Pitfalls

❌ **Don't**: Use SVG for apple-touch-icon  
✅ **Do**: Use PNG files

❌ **Don't**: Put icons behind authentication  
✅ **Do**: Ensure icons are publicly accessible

❌ **Don't**: Use transparent PNGs for iOS  
✅ **Do**: Use RGB PNGs without alpha channel

❌ **Don't**: Cache icons for too long  
✅ **Do**: Use reasonable cache times with revalidation

## Still Having Issues?

If the icon still doesn't appear after trying these methods:

1. **Check HTTPS certificate**: Ensure the site uses a valid CA-issued certificate (not self-signed)
2. **Verify icon accessibility**: Open icon URLs directly in browser to ensure they're accessible
3. **Check browser console**: Look for any 404 or loading errors for icon files
4. **Try different device**: Test on another iOS/Android device to isolate the issue
5. **Wait 24-48 hours**: Sometimes iOS takes time to propagate icon changes across its CDN

## Additional Resources

- [Apple Developer: Configuring Web Applications](https://developer.apple.com/library/archive/documentation/AppleApplications/Reference/SafariWebContent/ConfiguringWebApplications/ConfiguringWebApplications.html)
- [MDN: Define your app icons](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/How_to/Define_app_icons)
- [PWA Icon Best Practices](https://web.dev/add-manifest/)

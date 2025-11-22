# PWA Icon Configuration

## Icon Locations

All PWA and favicon icons are stored in `wwwroot/icons/`:

- `rtub-logo-16.png` - 16x16 favicon
- `rtub-logo-32.png` - 32x32 favicon
- `rtub-logo-180.png` - 180x180 Apple Touch Icon
- `rtub-logo-192.png` - 192x192 PWA icon
- `rtub-logo-512.png` - 512x512 PWA icon

## Updating Icons

To replace the RTUB logo icons in the future:

1. Generate PNG files at the required sizes (16, 32, 180, 192, 512 pixels)
2. Replace the files in `wwwroot/icons/`
3. Ensure files are proper PNG format, square, with transparent or white backgrounds
4. Clear browser cache and test

## iOS Home Screen Updates

**Important:** On iOS devices, users must delete the old home screen icon and re-add it after icon changes are deployed. iOS caches home screen icons and does not automatically update them.

Steps for iOS users:
1. Delete the existing RTUB icon from the home screen
2. Open Safari and navigate to the RTUB site
3. Tap Share → Add to Home Screen
4. Confirm the new icon appears correctly

## Browser Testing

After deployment, verify:
- Desktop browsers (Chrome/Edge/Firefox) show `rtub-logo-32.png` in tabs/bookmarks
- iOS Safari shows `rtub-logo-180.png` when added to home screen
- PWA installation uses `rtub-logo-192.png` and `rtub-logo-512.png`
- Manifest is accessible at `https://<domain>/manifest.webmanifest`

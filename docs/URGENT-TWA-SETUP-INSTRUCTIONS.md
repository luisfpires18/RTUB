# 🚨 URGENT: Complete TWA Configuration to Fix Standalone Mode

## Problem

Your Android TWA app currently opens in browser instead of standalone mode (with address bar visible). This is because the Digital Asset Links verification is failing.

## Root Cause

**Missing/incomplete `.well-known/assetlinks.json` file** - Android cannot verify that your website trusts your app.

## Solution (5 Minutes)

### Step 1: Get SHA-256 Fingerprint from Play Console

1. Go to: https://play.google.com/console
2. Select your **RTUB** app
3. Click **Setup** → **App signing** (left sidebar)
4. Find section: **App signing key certificate**
5. Copy the **SHA-256 certificate fingerprint**
   - Example format: `AB:CD:EF:12:34:56:78:90:AB:CD:EF:12:34:56:78:90:AB:CD:EF:12:34:56:78:90:AB:CD:EF:12:34:56:78:90`

**⚠️ CRITICAL**: Use the fingerprint from **"App signing key certificate"** section, NOT "Upload key certificate"!

### Step 2: Update assetlinks.json

1. Open file: `src/RTUB.Web/wwwroot/.well-known/assetlinks.json`

2. Replace `REPLACE_WITH_PLAY_CONSOLE_SHA256_FINGERPRINT` with your SHA-256 from Step 1:

```json
[{
  "relation": ["delegate_permission/common.handle_all_urls"],
  "target": {
    "namespace": "android_app",
    "package_name": "com.braganca.rtub",
    "sha256_cert_fingerprints": [
      "AB:CD:EF:12:34:56:78:90:AB:CD:EF:12:34:56:78:90:AB:CD:EF:12:34:56:78:90:AB:CD:EF:12:34:56:78:90"
    ]
  }
}]
```

3. **Verify the package_name** matches your actual Android app package:
   - Check in Play Console → Dashboard → Your app name
   - Should be something like `com.braganca.rtub` or similar

### Step 3: Deploy to Production

```bash
git add src/RTUB.Web/wwwroot/.well-known/assetlinks.json
git commit -m "Configure Digital Asset Links with Play Console SHA-256"
git push
```

Wait for Azure deployment to complete (usually 2-5 minutes).

### Step 4: Verify Configuration

Run these commands to test:

```bash
# 1. Check file is accessible
curl -I https://rtub.azurewebsites.net/.well-known/assetlinks.json

# Expected: HTTP/1.1 200 OK
# Expected: Content-Type: application/json

# 2. Check content
curl https://rtub.azurewebsites.net/.well-known/assetlinks.json

# Should show your SHA-256 fingerprint (not the placeholder)

# 3. Test with Google API
curl "https://digitalassetlinks.googleapis.com/v1/statements:list?source.web.site=https://rtub.azurewebsites.net&relation=delegate_permission/common.handle_all_urls"

# Should return your assetlinks statement
```

### Step 5: Test on Device

1. **Wait 30 minutes** (Digital Asset Links propagation time)

2. On your Android test device:
   - Go to Settings → Apps → RTUB
   - Tap **Storage** → **Clear data**
   - Go to Settings → Apps → Chrome
   - Tap **Storage** → **Clear cache**
   - Uninstall RTUB app completely

3. **Reinstall** from Play Store

4. **Launch the app** - should now open in standalone mode (no address bar!)

## What Changed

This PR created:

1. ✅ `.well-known/assetlinks.json` - Digital Asset Links file (needs your SHA-256)
2. ✅ Updated `Program.cs` - Serves assetlinks.json with correct Content-Type
3. ✅ Documentation - Complete guides for configuration and troubleshooting

## Next Steps After Merging

1. **Merge this PR**
2. **Complete Step 1-3 above** (add your SHA-256 fingerprint)
3. **Deploy to production**
4. **Wait 30 minutes**
5. **Test on device**

## Documentation

For detailed information, see:

- **[TWA Configuration Guide](twa-configuration-guide.md)** - Complete setup instructions
- **[TWA Release Runbook](twa-release-runbook.md)** - Future maintenance guide
- **[Android TWA Checklist](android-twa-checklist.md)** - Deployment checklist

## Troubleshooting

### App still opens in browser after 30 minutes?

**Check 1**: Verify file is accessible
```bash
curl https://rtub.azurewebsites.net/.well-known/assetlinks.json
```

**Check 2**: Verify you used "App signing key" SHA-256, not "Upload key"
- Go back to Play Console → App signing
- Make sure you copied from "App signing key certificate" section

**Check 3**: Verify package name matches exactly
- Check Play Console for your app's package name
- Update assetlinks.json if different

**Check 4**: Clear all caches
- Uninstall app
- Clear Chrome cache
- Wait full 30 minutes
- Reinstall from Play Store

## Questions?

See detailed troubleshooting in: [twa-configuration-guide.md](twa-configuration-guide.md)

## Summary

**Required Action**: Add your Play Console SHA-256 fingerprint to `assetlinks.json`

**Time Required**: 5 minutes to configure + 30 minutes wait time

**Result**: Android app will launch in standalone mode without browser UI

---

**Priority**: HIGH - Affects all Android app users

**Estimated Time**: 5 minutes configuration + 30 minutes propagation

**Risk**: Low - Only adds a configuration file, no code changes to core functionality

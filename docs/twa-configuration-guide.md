# TWA Configuration Guide - Digital Asset Links Setup

This guide provides step-by-step instructions for configuring Digital Asset Links to ensure your Android TWA app launches in standalone mode (without browser address bar).

## Root Cause

When an Android TWA app opens in browser instead of standalone mode, it's almost always due to **missing or misconfigured Digital Asset Links**. Android uses this verification system to confirm that your website trusts your Android app.

## Prerequisites

- App already published on Google Play Store (at least in internal/closed testing track)
- Access to Google Play Console
- Access to deploy files to your web server at `rtub.azurewebsites.net`

## Step-by-Step Configuration

### Step 1: Get Your App's SHA-256 Fingerprint from Play Console

1. **Open Google Play Console**: https://play.google.com/console
2. **Select your app** (RTUB)
3. Navigate to **Setup** → **App signing** (in the left sidebar)
4. Find the **App signing key certificate** section
5. Copy the **SHA-256 certificate fingerprint**
   - It looks like: `AB:CD:EF:12:34:56:78:90:AB:CD:EF:12:34:56:78:90:AB:CD:EF:12:34:56:78:90:AB:CD:EF:12:34:56:78:90`
   - **IMPORTANT**: Use the fingerprint from "App signing key certificate", NOT "Upload key certificate"

### Step 2: Verify Your Package Name

1. In Play Console, go to **Setup** → **App content**
2. Note your **Application ID** (package name)
   - Example: `com.braganca.rtub`
3. This MUST match the package name used when generating the TWA package

### Step 3: Update assetlinks.json

1. Open the file: `src/RTUB.Web/wwwroot/.well-known/assetlinks.json`

2. Replace `REPLACE_WITH_PLAY_CONSOLE_SHA256_FINGERPRINT` with the SHA-256 fingerprint from Step 1:

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

3. **Verify the package_name matches your actual package** (from Step 2)

### Step 4: Deploy to Production

1. Commit your changes:
```bash
git add src/RTUB.Web/wwwroot/.well-known/assetlinks.json
git commit -m "Add Digital Asset Links for Android TWA verification"
git push
```

2. Deploy to Azure App Service (follow your normal deployment process)

### Step 5: Verify the File is Accessible

After deployment, verify the file is publicly accessible:

```bash
curl -I https://rtub.azurewebsites.net/.well-known/assetlinks.json
```

**Expected response:**
```
HTTP/1.1 200 OK
Content-Type: application/json
```

**Test the content:**
```bash
curl https://rtub.azurewebsites.net/.well-known/assetlinks.json
```

Should return your JSON with the correct SHA-256 fingerprint.

### Step 6: Test Digital Asset Links

Use Google's Asset Links Tester:

1. Go to: https://digitalassetlinks.googleapis.com/v1/statements:list?source.web.site=https://rtub.azurewebsites.net&relation=delegate_permission/common.handle_all_urls

2. You should see your assetlinks statement in the response

3. Alternatively, use the Play Console Asset Links tool:
   - Play Console → Your App → Setup → App signing
   - Scroll down to "Digital Asset Links JSON"
   - Click "Test statement"

### Step 7: Update Android App (if needed)

If you made changes to the package name, you'll need to regenerate the TWA package:

1. Go to [PWABuilder](https://www.pwabuilder.com)
2. Enter: `https://rtub.azurewebsites.net`
3. Package for Android
4. Use the **same package name** as in assetlinks.json
5. Generate and download new `.aab` file
6. Upload to Play Console as a new release

### Step 8: Clear App Data and Test

On your Android device:

1. **Uninstall the app** completely (or clear app data):
   - Settings → Apps → RTUB → Storage → Clear data
   
2. **Clear Chrome data** (important for verification cache):
   - Settings → Apps → Chrome → Storage → Clear cache

3. **Wait 15-30 minutes** for Digital Asset Links to propagate

4. **Reinstall the app** from Play Store

5. **Launch the app** - it should now open in standalone mode (no address bar)

## Troubleshooting

### App still opens in browser after following all steps

**Check 1: Wait for propagation**
- Digital Asset Links can take 15-30 minutes to propagate
- Wait at least 30 minutes after deploying assetlinks.json

**Check 2: Verify exact package name match**
```bash
# Check assetlinks.json
curl https://rtub.azurewebsites.net/.well-known/assetlinks.json | jq .

# Verify package_name matches your app
```

**Check 3: Verify SHA-256 fingerprint is correct**
- Double-check you used the "App signing key certificate" fingerprint
- NOT the "Upload key certificate" fingerprint
- Fingerprint should be in uppercase with colons (XX:XX:XX:...)

**Check 4: Check for redirects**
```bash
curl -L -I https://rtub.azurewebsites.net
```
- If there's a redirect to `www.` or another domain, update manifest and assetlinks accordingly
- The domain in the browser and in assetlinks.json MUST match exactly

**Check 5: Content-Type header**
```bash
curl -I https://rtub.azurewebsites.net/.well-known/assetlinks.json
```
Should return: `Content-Type: application/json`

**Check 6: Clear all caches**
- Uninstall app completely
- Clear Chrome app data
- Reboot device
- Wait 30 minutes
- Reinstall app

### File returns 404

- Ensure `.well-known` directory is being deployed
- Check that Azure App Service is serving static files
- Verify no URL rewrite rules are blocking `/.well-known/`

### Wrong Content-Type

The server configuration in `Program.cs` should handle this automatically. If you see wrong Content-Type:

1. Check that the static files middleware configuration includes the JSON mapping
2. Verify no other middleware is overriding the Content-Type

## Configuration Reference

### Correct assetlinks.json Structure

```json
[{
  "relation": ["delegate_permission/common.handle_all_urls"],
  "target": {
    "namespace": "android_app",
    "package_name": "com.braganca.rtub",
    "sha256_cert_fingerprints": [
      "YOUR_SHA256_FINGERPRINT_HERE"
    ]
  }
}]
```

### Key Points

- **URL**: `https://rtub.azurewebsites.net/.well-known/assetlinks.json`
- **Content-Type**: `application/json`
- **Package Name**: `com.braganca.rtub` (or your actual package)
- **SHA-256**: From "App signing key certificate" in Play Console
- **Cache**: 1 hour (3600 seconds) is reasonable
- **Access**: Must be publicly accessible (no authentication)

## Verification Checklist

Before releasing to production:

- [ ] assetlinks.json file exists at `.well-known/assetlinks.json`
- [ ] File is publicly accessible (returns HTTP 200)
- [ ] Content-Type is `application/json`
- [ ] Package name matches Android app exactly
- [ ] SHA-256 fingerprint is from "App signing key certificate"
- [ ] No redirects on the main domain
- [ ] Digital Asset Links API returns the statement
- [ ] Waited 30 minutes after deployment
- [ ] Tested on physical Android device
- [ ] App launches without address bar (standalone mode)

## Maintenance

### When to Update assetlinks.json

You need to update assetlinks.json when:

1. **Changing package name** (requires new app submission)
2. **Regenerating signing key** (requires adding new fingerprint)
3. **Supporting multiple apps** (add more entries to the array)

### You DO NOT need to update for:

- Website content changes
- PWA manifest updates (unless changing domain)
- App version updates
- Bug fixes

## Support Resources

- **Digital Asset Links Documentation**: https://developers.google.com/digital-asset-links
- **TWA Documentation**: https://developer.chrome.com/docs/android/trusted-web-activity/
- **Asset Links Tester**: https://digitalassetlinks.googleapis.com/v1/statements:list
- **PWABuilder**: https://www.pwabuilder.com

## Common Mistakes to Avoid

1. ❌ Using upload key fingerprint instead of app signing key
2. ❌ Package name mismatch between app and assetlinks.json
3. ❌ Not waiting 15-30 minutes for propagation
4. ❌ File not accessible (404 or authentication required)
5. ❌ Wrong Content-Type (text/html instead of application/json)
6. ❌ Domain redirects not handled (http → https, non-www → www)
7. ❌ Testing on debug/development build instead of Play Store build

## Quick Test Command

After deployment, run this quick test:

```bash
echo "Testing Digital Asset Links configuration..."
echo "1. File accessibility:"
curl -I https://rtub.azurewebsites.net/.well-known/assetlinks.json
echo ""
echo "2. File content:"
curl https://rtub.azurewebsites.net/.well-known/assetlinks.json
echo ""
echo "3. Google Asset Links API:"
curl "https://digitalassetlinks.googleapis.com/v1/statements:list?source.web.site=https://rtub.azurewebsites.net&relation=delegate_permission/common.handle_all_urls"
```

All three should return valid responses.

---

**Last Updated**: December 2024
**Maintainer**: RTUB Development Team

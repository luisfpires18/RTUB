# TWA Release Runbook

Quick reference guide for maintaining TWA app and ensuring it continues to launch in standalone mode.

## Pre-Release Checklist

Before every Android TWA release, verify:

### 1. Digital Asset Links (Critical!)

```bash
# Test file accessibility
curl -I https://rtub.azurewebsites.net/.well-known/assetlinks.json

# Should return:
# HTTP/1.1 200 OK
# Content-Type: application/json
```

```bash
# Verify content
curl https://rtub.azurewebsites.net/.well-known/assetlinks.json | jq .

# Should show:
# - package_name: "com.braganca.rtub" (or your package)
# - sha256_cert_fingerprints: [correct Play signing certificate SHA-256]
```

### 2. PWA Manifest Validation

```bash
# Check manifest is accessible
curl https://rtub.azurewebsites.net/manifest.webmanifest

# Verify:
# - "display": "standalone"
# - "start_url": "/"
# - "scope": "/"
```

### 3. No Redirects

```bash
# Check for HTTP redirects
curl -L -I https://rtub.azurewebsites.net/

# Should show single redirect chain:
# HTTP -> HTTPS (acceptable)
# Should NOT redirect to www. or different domain
```

## When Digital Asset Links Needs Updating

### Scenario 1: First Time Setup

Follow: [TWA Configuration Guide](twa-configuration-guide.md)

1. Get SHA-256 from Play Console → App signing
2. Update `src/RTUB.Web/wwwroot/.well-known/assetlinks.json`
3. Deploy to production
4. Wait 30 minutes
5. Test on device

### Scenario 2: Package Name Changed

⚠️ **This requires a NEW app submission to Play Store**

1. Update package name in TWA project
2. Generate new `.aab`
3. Update `assetlinks.json` with new package name
4. Deploy both
5. Submit as new app to Play Store

### Scenario 3: Signing Key Changed

🔴 **This is rare and should be avoided**

If you regenerate your signing key:

1. Get new SHA-256 from Play Console
2. Add to existing fingerprints array in `assetlinks.json`:
```json
"sha256_cert_fingerprints": [
  "OLD_FINGERPRINT",
  "NEW_FINGERPRINT"
]
```
3. Deploy
4. Keep both until old versions are phased out

### Scenario 4: Website Content Update

✅ **No action needed!**

Regular website updates do NOT require updating assetlinks.json or releasing a new Android app version.

## Deployment Process

### Standard Website Update (No TWA Changes)

```bash
# 1. Deploy website normally
git push origin main

# 2. Azure auto-deploys
# No TWA-specific actions needed
```

### TWA-Specific Update (Rare)

Only needed if:
- Changing package name
- Changing signing certificate
- Fixing standalone mode issues

```bash
# 1. Update assetlinks.json if needed
nano src/RTUB.Web/wwwroot/.well-known/assetlinks.json

# 2. Commit and deploy
git add src/RTUB.Web/wwwroot/.well-known/assetlinks.json
git commit -m "Update Digital Asset Links for TWA"
git push

# 3. Wait for Azure deployment

# 4. Verify accessibility
curl https://rtub.azurewebsites.net/.well-known/assetlinks.json

# 5. Wait 30 minutes for propagation

# 6. Test on device
```

## Testing Standalone Mode

### After Updating assetlinks.json

1. **On Android device:**
   - Uninstall RTUB app completely
   - Settings → Apps → Chrome → Storage → Clear cache
   - Wait 30 minutes
   - Reinstall from Play Store
   - Launch app

2. **Expected result:**
   - App opens in standalone mode
   - No address bar visible
   - No browser UI elements
   - Native Android navigation

3. **If it still opens in browser:**
   - Wait full 30 minutes
   - Check all items in troubleshooting guide
   - Verify SHA-256 is from "App signing key" not "Upload key"

## Quick Diagnostics

### Is assetlinks.json accessible?

```bash
curl -I https://rtub.azurewebsites.net/.well-known/assetlinks.json
# Expected: HTTP/1.1 200 OK
```

### Is the content correct?

```bash
curl https://rtub.azurewebsites.net/.well-known/assetlinks.json | jq .
```

Verify:
- ✅ `relation`: `["delegate_permission/common.handle_all_urls"]`
- ✅ `package_name`: Matches your Android app
- ✅ `sha256_cert_fingerprints`: From Play Console "App signing key"

### Does Google see it?

```bash
curl "https://digitalassetlinks.googleapis.com/v1/statements:list?source.web.site=https://rtub.azurewebsites.net&relation=delegate_permission/common.handle_all_urls"
```

Should return your statement. If empty, wait longer or check accessibility.

## Common Mistakes to Avoid

| Mistake | Impact | Solution |
|---------|--------|----------|
| Using upload key SHA-256 | App opens in browser | Use "App signing key certificate" from Play Console |
| Package name mismatch | App opens in browser | Ensure exact match in app and assetlinks.json |
| Not waiting 30 minutes | App opens in browser | Be patient, propagation takes time |
| File returns 404 | App opens in browser | Verify `.well-known` directory is deployed |
| Wrong Content-Type | Verification fails | Ensure `application/json` is set |
| Domain redirect issues | Verification fails | Use canonical domain consistently |

## Emergency Troubleshooting

### App suddenly opens in browser after working fine

**Check 1: Is assetlinks.json still accessible?**
```bash
curl https://rtub.azurewebsites.net/.well-known/assetlinks.json
```

**Check 2: Did Azure deployment break static file serving?**
- Check Azure App Service logs
- Verify static files middleware is enabled
- Check for URL rewrite rules blocking `.well-known`

**Check 3: Did domain change?**
- Verify no redirects: `curl -L -I https://rtub.azurewebsites.net`
- Domain in assetlinks must match where app loads

**Check 4: Did Play Store change signing key?**
- Unlikely but check Play Console → App signing
- Verify SHA-256 hasn't changed

## Monitoring

### Periodic Checks (Monthly)

```bash
# 1. Verify assetlinks.json is accessible
curl -I https://rtub.azurewebsites.net/.well-known/assetlinks.json

# 2. Test on Android device
# Install from Play Store and verify standalone mode

# 3. Check Play Console for user reports
# Look for "opens in browser" complaints
```

## Version Control

### Files to Track in Git

```
src/RTUB.Web/wwwroot/.well-known/assetlinks.json  # ✅ Track this
src/RTUB.Web/Program.cs                            # ✅ Contains static file config
src/RTUB.Web/wwwroot/manifest.webmanifest          # ✅ PWA manifest
docs/twa-*.md                                      # ✅ Documentation
```

### Never Commit

- Signing keys (`.keystore`, `.jks` files)
- Play Store credentials
- Upload certificates

## Support Contacts

### Internal

- **Developer**: Check git blame on assetlinks.json
- **DevOps**: Check Azure deployment logs

### External

- **Play Console Support**: https://support.google.com/googleplay/android-developer
- **TWA Documentation**: https://developer.chrome.com/docs/android/trusted-web-activity/
- **Digital Asset Links**: https://developers.google.com/digital-asset-links

## Quick Reference Commands

```bash
# Test full TWA configuration
./scripts/test-twa-config.sh  # If you create this script

# Or manually:
echo "=== Testing TWA Configuration ==="
echo ""
echo "1. assetlinks.json accessibility:"
curl -I https://rtub.azurewebsites.net/.well-known/assetlinks.json
echo ""
echo "2. assetlinks.json content:"
curl https://rtub.azurewebsites.net/.well-known/assetlinks.json | jq .
echo ""
echo "3. manifest.webmanifest:"
curl https://rtub.azurewebsites.net/manifest.webmanifest | jq '.display, .start_url, .scope'
echo ""
echo "4. Redirect check:"
curl -L -I https://rtub.azurewebsites.net/ | grep -i location
echo ""
echo "5. Google Asset Links API:"
curl "https://digitalassetlinks.googleapis.com/v1/statements:list?source.web.site=https://rtub.azurewebsites.net&relation=delegate_permission/common.handle_all_urls" | jq .
```

---

## Summary

**For routine deployments**: No action needed for TWA.

**For TWA troubleshooting**: Follow [TWA Configuration Guide](twa-configuration-guide.md).

**Key file**: `src/RTUB.Web/wwwroot/.well-known/assetlinks.json`

**Remember**: 
- Verify assetlinks.json is accessible before every release
- Use "App signing key" SHA-256, not upload key
- Wait 30 minutes after changes
- Test on real device from Play Store

---

**Last Updated**: December 2024
**Maintainer**: RTUB Development Team

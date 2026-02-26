# .well-known Directory

This directory contains files for web standards and protocols.

## assetlinks.json

**Purpose**: Digital Asset Links for Android Trusted Web Activities (TWA)

This file is **critical** for the Android app to launch in standalone mode (without browser address bar).

### What it does

- Verifies that the RTUB website trusts the RTUB Android app
- Allows the Android app to open links in a trusted TWA (Trusted Web Activity)
- Without this file, the app will open in a browser tab instead of standalone mode

### Configuration

Before deploying, you MUST:

1. Get the SHA-256 fingerprint from Play Console:
   - Play Console → Your App → Setup → App signing
   - Copy "App signing key certificate" SHA-256 (NOT upload key)

2. Update `assetlinks.json` with the correct:
   - `package_name`: Your Android app package (e.g., `com.braganca.rtub`)
   - `sha256_cert_fingerprints`: The SHA-256 from step 1

3. Verify accessibility after deployment:
   ```bash
   curl https://rtub.azurewebsites.net/.well-known/assetlinks.json
   ```

### Documentation

For complete setup instructions, see:
- [TWA Configuration Guide](../../../../docs/twa-configuration-guide.md)
- [TWA Release Runbook](../../../../docs/twa-release-runbook.md)
- [Android TWA Checklist](../../../../docs/android-twa-checklist.md)

### ⚠️ Important

- This file MUST be publicly accessible (no authentication)
- MUST return `Content-Type: application/json`
- MUST be served over HTTPS
- Changes take 15-30 minutes to propagate
- DO NOT delete this file if you have an Android app on Play Store

### Troubleshooting

If the Android app opens in browser instead of standalone:
1. Verify this file is accessible (returns HTTP 200)
2. Check SHA-256 is from "App signing key" not "Upload key"
3. Verify package name matches Android app exactly
4. Wait 30 minutes after making changes
5. Clear app data and reinstall

See [twa-configuration-guide.md](../../../../docs/twa-configuration-guide.md) for detailed troubleshooting.

---

**Last Updated**: December 2024

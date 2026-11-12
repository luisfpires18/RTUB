# .well-known

## assetlinks.json

Digital Asset Links for the Android app (a Trusted Web Activity of this site, package `ipb.pt.rtub.app`). Without it
the app opens in a browser tab instead of standalone.

- `sha256_cert_fingerprints` must be the **App signing key** certificate SHA-256 from Play Console (Setup → App
  signing), not the upload key.
- `package_name` must match the Android app exactly.
- Must be public, served over HTTPS with `Content-Type: application/json`. Check:
  `curl https://rtub.azurewebsites.net/.well-known/assetlinks.json`
- Changes take 15-30 minutes to propagate. Do not delete it while the app is on Play Store.

If the app opens in a browser: check the file returns 200, the fingerprint is the signing key, the package name
matches, wait 30 minutes, then clear the app data and reinstall.

Install guidance and the manifest: `docs/pwa-practices.md`.

#!/bin/bash

# Test TWA Configuration
# This script verifies that Digital Asset Links are configured correctly

echo "=========================================="
echo "TWA Configuration Test"
echo "=========================================="
echo ""

DOMAIN="rtub.azurewebsites.net"
ASSETLINKS_URL="https://${DOMAIN}/.well-known/assetlinks.json"
MANIFEST_URL="https://${DOMAIN}/manifest.webmanifest"

echo "Testing domain: ${DOMAIN}"
echo ""

# Test 1: Check if assetlinks.json is accessible
echo "1️⃣  Testing assetlinks.json accessibility..."
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${ASSETLINKS_URL}")
CONTENT_TYPE=$(curl -s -I "${ASSETLINKS_URL}" | grep -i "content-type" | awk '{print $2}' | tr -d '\r')

if [ "$HTTP_CODE" == "200" ]; then
    echo "   ✅ File is accessible (HTTP ${HTTP_CODE})"
else
    echo "   ❌ File returned HTTP ${HTTP_CODE} (expected 200)"
    echo "   💡 Deploy the .well-known directory to production"
    exit 1
fi

if [[ "$CONTENT_TYPE" == *"application/json"* ]]; then
    echo "   ✅ Content-Type is correct (${CONTENT_TYPE})"
else
    echo "   ⚠️  Content-Type is ${CONTENT_TYPE} (expected application/json)"
fi
echo ""

# Test 2: Validate JSON structure
echo "2️⃣  Validating assetlinks.json content..."
ASSETLINKS_CONTENT=$(curl -s "${ASSETLINKS_URL}")

if echo "$ASSETLINKS_CONTENT" | python3 -m json.tool > /dev/null 2>&1; then
    echo "   ✅ JSON is valid"
else
    echo "   ❌ JSON is invalid"
    echo "   Content: ${ASSETLINKS_CONTENT}"
    exit 1
fi

# Extract values
PACKAGE_NAME=$(echo "$ASSETLINKS_CONTENT" | python3 -c "import sys, json; print(json.load(sys.stdin)[0]['target']['package_name'])" 2>/dev/null)
FINGERPRINT=$(echo "$ASSETLINKS_CONTENT" | python3 -c "import sys, json; print(json.load(sys.stdin)[0]['target']['sha256_cert_fingerprints'][0])" 2>/dev/null)

echo "   Package name: ${PACKAGE_NAME}"
echo "   SHA-256: ${FINGERPRINT}"

if [[ "$FINGERPRINT" == "REPLACE_WITH_PLAY_CONSOLE_SHA256_FINGERPRINT" ]]; then
    echo "   ⚠️  SHA-256 fingerprint not configured yet!"
    echo "   💡 Update with your Play Console SHA-256 fingerprint"
    echo "   📖 See: docs/URGENT-TWA-SETUP-INSTRUCTIONS.md"
else
    echo "   ✅ SHA-256 fingerprint is configured"
fi
echo ""

# Test 3: Check manifest
echo "3️⃣  Testing manifest.webmanifest..."
MANIFEST_HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${MANIFEST_URL}")

if [ "$MANIFEST_HTTP_CODE" == "200" ]; then
    echo "   ✅ Manifest is accessible (HTTP ${MANIFEST_HTTP_CODE})"
    
    MANIFEST_CONTENT=$(curl -s "${MANIFEST_URL}")
    DISPLAY=$(echo "$MANIFEST_CONTENT" | python3 -c "import sys, json; print(json.load(sys.stdin).get('display', 'not set'))" 2>/dev/null)
    START_URL=$(echo "$MANIFEST_CONTENT" | python3 -c "import sys, json; print(json.load(sys.stdin).get('start_url', 'not set'))" 2>/dev/null)
    SCOPE=$(echo "$MANIFEST_CONTENT" | python3 -c "import sys, json; print(json.load(sys.stdin).get('scope', 'not set'))" 2>/dev/null)
    
    echo "   Display mode: ${DISPLAY}"
    echo "   Start URL: ${START_URL}"
    echo "   Scope: ${SCOPE}"
    
    if [ "$DISPLAY" == "standalone" ]; then
        echo "   ✅ Display mode is standalone"
    else
        echo "   ⚠️  Display mode should be 'standalone'"
    fi
else
    echo "   ❌ Manifest returned HTTP ${MANIFEST_HTTP_CODE}"
fi
echo ""

# Test 4: Check for redirects
echo "4️⃣  Checking for domain redirects..."
REDIRECT_COUNT=$(curl -s -I -L "https://${DOMAIN}" | grep -i "location" | wc -l)

if [ "$REDIRECT_COUNT" -eq 0 ]; then
    echo "   ✅ No redirects detected"
elif [ "$REDIRECT_COUNT" -eq 1 ]; then
    echo "   ⚠️  One redirect detected (may be HTTP→HTTPS, which is OK)"
    curl -s -I -L "https://${DOMAIN}" | grep -i "location"
else
    echo "   ⚠️  Multiple redirects detected:"
    curl -s -I -L "https://${DOMAIN}" | grep -i "location"
    echo "   💡 Ensure consistent domain (no www redirects)"
fi
echo ""

# Test 5: Google Digital Asset Links API
echo "5️⃣  Testing with Google Digital Asset Links API..."
API_URL="https://digitalassetlinks.googleapis.com/v1/statements:list?source.web.site=https://${DOMAIN}&relation=delegate_permission/common.handle_all_urls"
API_RESPONSE=$(curl -s "${API_URL}")

if echo "$API_RESPONSE" | grep -q "package_name"; then
    echo "   ✅ Google API can see your assetlinks statement"
    echo "$API_RESPONSE" | python3 -m json.tool 2>/dev/null | head -20
else
    echo "   ⚠️  Google API cannot verify assetlinks yet"
    echo "   💡 This can take 15-30 minutes after deployment"
    if [[ "$FINGERPRINT" == "REPLACE_WITH_PLAY_CONSOLE_SHA256_FINGERPRINT" ]]; then
        echo "   💡 Also make sure to configure your SHA-256 fingerprint first"
    fi
fi
echo ""

# Summary
echo "=========================================="
echo "Summary"
echo "=========================================="
echo ""

if [ "$HTTP_CODE" == "200" ] && [[ "$CONTENT_TYPE" == *"application/json"* ]] && [ "$DISPLAY" == "standalone" ]; then
    if [[ "$FINGERPRINT" == "REPLACE_WITH_PLAY_CONSOLE_SHA256_FINGERPRINT" ]]; then
        echo "⚠️  Configuration is INCOMPLETE"
        echo ""
        echo "Action Required:"
        echo "1. Get SHA-256 from Play Console (App signing → App signing key certificate)"
        echo "2. Update src/RTUB.Web/wwwroot/.well-known/assetlinks.json"
        echo "3. Deploy to production"
        echo "4. Wait 30 minutes"
        echo "5. Test on device"
        echo ""
        echo "📖 See: docs/URGENT-TWA-SETUP-INSTRUCTIONS.md"
    else
        echo "✅ Configuration looks good!"
        echo ""
        echo "Next steps:"
        echo "1. Wait 15-30 minutes for Digital Asset Links propagation"
        echo "2. Clear app data and Chrome cache on device"
        echo "3. Reinstall app from Play Store"
        echo "4. App should launch in standalone mode (no address bar)"
        echo ""
        echo "If issues persist, see: docs/twa-configuration-guide.md"
    fi
else
    echo "❌ Configuration has issues"
    echo ""
    echo "Please review the errors above and:"
    echo "1. Ensure .well-known/assetlinks.json is deployed"
    echo "2. Check Content-Type headers"
    echo "3. Verify manifest display mode is 'standalone'"
    echo ""
    echo "📖 See: docs/twa-configuration-guide.md"
fi
echo ""

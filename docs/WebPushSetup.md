# Web Push Notifications Setup Guide

This guide explains how to configure and use Web Push notifications in the RTUB application.

## Overview

The Web Push notification system allows the application to send push notifications to users' browsers, even when the application is not actively open. This feature uses the Web Push API with VAPID authentication.

## Configuration

### 1. Generate VAPID Keys

VAPID keys are required for secure authentication when sending push notifications. You can generate them using one of these methods:

#### Method 1: Using Node.js (web-push package)

```bash
# Install the web-push package globally
npm install -g web-push

# Generate VAPID keys
web-push generate-vapid-keys
```

This will output something like:
```
=======================================
Public Key:
BF8i...rest-of-public-key...abc

Private Key:
def...rest-of-private-key...xyz
=======================================
```

#### Method 2: Using Online Generator

You can use an online VAPID key generator like:
- https://vapidkeys.com/
- https://www.attheminute.com/vapid-key-generator

⚠️ **IMPORTANT**: Keep your private key secret! Never commit it to source control or share it publicly.

### 2. Configure appsettings.json

Add the VAPID keys to your `appsettings.json` or better yet, use User Secrets for sensitive data:

```json
{
  "WebPush": {
    "Enabled": true,
    "VapidSubject": "mailto:admin@rtub.pt",
    "VapidPublicKey": "YOUR_PUBLIC_KEY_HERE",
    "VapidPrivateKey": "YOUR_PRIVATE_KEY_HERE"
  }
}
```

#### Using User Secrets (Recommended for Development)

```bash
cd src/RTUB.Web
dotnet user-secrets set "WebPush:VapidPublicKey" "YOUR_PUBLIC_KEY_HERE"
dotnet user-secrets set "WebPush:VapidPrivateKey" "YOUR_PRIVATE_KEY_HERE"
```

#### Using Environment Variables (Recommended for Production)

```bash
export WebPush__VapidPublicKey="YOUR_PUBLIC_KEY_HERE"
export WebPush__VapidPrivateKey="YOUR_PRIVATE_KEY_HERE"
```

### 3. Configuration Options

- **Enabled**: Controls whether non-OWNER users can access Web Push features. OWNER users always have access regardless of this setting.
- **VapidSubject**: A mailto: URL or HTTPS URL that identifies your application (e.g., "mailto:admin@rtub.pt")
- **VapidPublicKey**: The public VAPID key (safe to expose to clients)
- **VapidPrivateKey**: The private VAPID key (must be kept secret)

## Authorization Logic

The Web Push feature implements a dual authorization model:

1. **OWNER Role Users**: Always have access to Web Push features, regardless of the `WebPush:Enabled` setting
2. **Non-OWNER Users**: Only have access when `WebPush:Enabled` is set to `true`

This allows administrators (OWNER role) to test and use push notifications even when the feature is disabled for regular users.

## Database Migration

Run the database migration to create the PushSubscriptions table:

```bash
cd src/RTUB.Web
dotnet ef database update
```

## Usage

### For Users

1. Navigate to your profile page (`/profile`)
2. Scroll to the "Notificações" section
3. Toggle the "Push Notifications" switch
4. Grant permission when prompted by the browser
5. Use the "Send Test Notification" button to verify it's working

### For Administrators (OWNER Role)

Administrators can:
- Send test notifications to themselves
- Broadcast notifications to all subscribed users via the API endpoint:

```http
POST /api/push/broadcast
Content-Type: application/json

{
  "title": "Important Announcement",
  "body": "This is a broadcast message to all users",
  "url": "/events",
  "icon": "/icons/rtub-logo-192.png"
}
```

## API Endpoints

- `GET /api/push/status` - Check if Web Push is available for the current user
- `POST /api/push/subscribe` - Subscribe to push notifications
- `POST /api/push/unsubscribe` - Unsubscribe from push notifications
- `POST /api/push/send-test` - Send a test notification to the current user
- `POST /api/push/broadcast` - Broadcast notification to all users (OWNER only)

## Browser Support

Web Push notifications are supported in:
- Chrome/Edge (Desktop & Android)
- Firefox (Desktop & Android)
- Safari (macOS 16+, iOS 16.4+)
- Opera (Desktop & Android)

## Troubleshooting

### Notifications not appearing

1. Check browser notification permissions (should be "Allow")
2. Verify service worker is registered (check browser DevTools > Application > Service Workers)
3. Check that VAPID keys are correctly configured
4. Ensure `WebPush:Enabled` is true (or you're an OWNER user)
5. Check browser console for errors

### Service Worker not registering

1. Ensure the application is served over HTTPS (required for service workers, except localhost)
2. Check that `/service-worker.js` is accessible
3. Verify no browser extensions are blocking service workers

### Subscription fails

1. Check that the VAPID public key is correctly set
2. Verify the server can reach the push service endpoints (not blocked by firewall)
3. Check server logs for detailed error messages

## Security Considerations

1. **VAPID Private Key**: Must be kept secret. Never commit to source control.
2. **HTTPS Required**: Service workers and push notifications require HTTPS in production.
3. **User Consent**: Always respect user preferences and allow them to unsubscribe.
4. **Rate Limiting**: Consider implementing rate limiting for push notifications to prevent spam.

## Testing

Unit tests for the push notification system are located in:
- `/tests/RTUB.Application.Tests/Services/PushNotificationServiceTests.cs`
- `/tests/RTUB.Web.Tests/Controllers/PushControllerTests.cs`

Integration tests verify the complete flow including:
- Service worker registration
- Push subscription creation
- Notification sending
- Authorization logic

Run tests with:
```bash
dotnet test
```

## Additional Resources

- [Web Push Protocol](https://datatracker.ietf.org/doc/html/rfc8030)
- [VAPID Specification](https://datatracker.ietf.org/doc/html/rfc8292)
- [MDN Web Push API](https://developer.mozilla.org/en-US/docs/Web/API/Push_API)
- [Service Worker API](https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API)

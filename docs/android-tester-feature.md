# Android Tester Feature

**Date:** 2026-02-01  
**Version:** 1.0  
**Status:** Active

## Overview

The Android Tester feature enables the RTUB platform owner to manage a group of users designated as Android testers. These testers receive automated reminder notifications throughout the day to encourage them to test the Android Progressive Web App (PWA). The feature includes both automated scheduled notifications and the ability to send custom push notifications on-demand.

## Purpose

This feature was designed to support Android app testing campaigns by:
- Identifying specific users as Android testers
- Sending automated reminders at configured times during the day
- Allowing the owner to send custom notifications to all testers
- Tracking user login activity to avoid sending redundant notifications
- Managing testing campaigns with configurable start and end dates

## Key Features

### 1. Android Tester Management

**Location:** Members page (`/members`)

Owners can designate users as Android testers through the Members page:
- Edit any member's profile using the edit button
- Check the "Android Tester" checkbox in the edit modal
- Changes are saved immediately to the database
- The `IsAndroidTester` boolean flag is stored in the `ApplicationUser` entity

### 2. Android Testers Dashboard

**Component:** `AndroidTestersButton.razor`  
**Location:** Available on relevant admin pages

The Android Testers button opens a modal that provides:

#### View All Testers
- **Search Functionality:** Filter testers by name or username
- **Member Cards:** Display avatar, nickname, full name, and last login date
- **Pagination:** Configurable items per page (12, 24, 36, 48, 60)
- **Sorting:** Alphabetically sorted by display nickname

#### Send Custom Notifications
- **Message Input:** Text area with 500-character limit
- **Push Notification:** Sends to all Android testers immediately
- **Validation:** Ensures message is not empty before sending
- **Feedback:** Success/error messages displayed after sending
- **Notification Details:**
  - Title: "RTUB - Testadores Android"
  - Tag: "android-testers-notification"
  - Icon: `/images/logo-512x512.png`
  - URL: Redirects to homepage

### 3. Automated Reminder Notifications

**Service:** `AndroidTesterNotificationBackgroundService`  
**Type:** Hosted Background Service

#### How It Works

1. **Startup:** Service starts 20 seconds after application launch
2. **Scheduling:** Calculates next notification time based on configured times
3. **Smart Filtering:** 
   - Checks if user has logged in today
   - Skips users who already logged in (no more notifications for that day)
   - Tracks which users were already notified today
4. **Campaign Dates:** Only sends notifications within configured date range
5. **Daily Reset:** Clears tracking at midnight UTC to start fresh each day

#### Notification Process

```
For each configured time (e.g., 09:00, 12:00, 15:00, 18:00, 21:00):
  ↓
Check if campaign is active (StartDate ≤ Today ≤ EndDate)
  ↓
Query database for all users where IsAndroidTester = true
  ↓
Filter out users who:
  - Already logged in today (LastLoginDate >= today)
  - Were already notified today
  ↓
Send push notification to remaining users
  ↓
Mark users as notified for today
  ↓
Wait until next scheduled time
```

#### Default Notification Content

- **Title:** "Lembrete de Teste"
- **Body:** "Você é um Android tester e precisamos que entres na app durante 5 minutos hoje."
- **Icon:** `/icons/rtub-logo-192.png`
- **Tag:** "android-tester-reminder"
- **URL:** Homepage (`/`)

## Technical Architecture

### Database Schema

**Table:** `AspNetUsers`  
**New Column:** `IsAndroidTester` (Boolean, Default: `false`)

```sql
ALTER TABLE AspNetUsers ADD IsAndroidTester INTEGER NOT NULL DEFAULT 0;
```

**Migration:** `20260201141937_AddIsAndroidTesterToApplicationUser`

### Components & Services

#### 1. Domain Model
- **File:** `ApplicationUser.cs`
- **Property:** `public bool IsAndroidTester { get; set; }`
- **Location:** User entity in Identity system

#### 2. Background Service
- **File:** `AndroidTesterNotificationBackgroundService.cs`
- **Namespace:** `RTUB.Application.Services`
- **Lifetime:** Singleton hosted service
- **Dependencies:**
  - `ILogger<AndroidTesterNotificationBackgroundService>`
  - `IServiceScopeFactory` (for scoped services)
  - `IOptions<AndroidTesterNotificationOptions>`

#### 3. Configuration Options
- **File:** `AndroidTesterNotificationOptions.cs`
- **Namespace:** `RTUB.Application.Configuration`
- **Section:** `AndroidTesterNotifications`

#### 4. UI Component
- **File:** `AndroidTestersButton.razor`
- **Namespace:** `RTUB.Shared`
- **Type:** Blazor component
- **Dependencies:**
  - `UserManager<ApplicationUser>`
  - `IPushNotificationService`
  - `NavigationManager`

#### 5. Notification Factory
- **File:** `PushNotificationFactory.cs`
- **Method:** `CreateAndroidTesterReminderNotification(string baseUrl)`
- **Returns:** `SendPushNotificationDto`

### Service Registration

**File:** `Program.cs`

```csharp
// Configure options
builder.Services.Configure<AndroidTesterNotificationOptions>(
    builder.Configuration.GetSection(AndroidTesterNotificationOptions.SectionName));

// Register background service
builder.Services.AddHostedService<AndroidTesterNotificationBackgroundService>();
```

## Data Flow

### Custom Notification Flow

```
User clicks "Android Testers" button
  ↓
Modal opens and loads all Android testers from database
  ↓
User enters custom message (max 500 chars)
  ↓
User clicks "Send Notification"
  ↓
Component validates message
  ↓
Creates SendPushNotificationDto with custom message
  ↓
Calls IPushNotificationService.SendToSelectedUsersAsync()
  ↓
Push notification sent to all Android tester user IDs
  ↓
Success/error message displayed
```

### Automated Notification Flow

```
Background Service starts (20s after app start)
  ↓
Validates configuration (notification times format)
  ↓
Calculates next scheduled notification time
  ↓
Waits until scheduled time
  ↓
Checks if feature is enabled
  ↓
Checks if current date is within campaign dates
  ↓
Creates scoped services (DbContext, PushNotificationService)
  ↓
Queries all users where IsAndroidTester = true
  ↓
Filters users (already logged in today OR already notified)
  ↓
Creates reminder notification DTO
  ↓
Sends push notification to filtered users
  ↓
Tracks notified users in memory (HashSet)
  ↓
Logs success/failure
  ↓
Calculates next notification time and repeats
```

## User Experience

### For Owners

1. **Designating Testers:**
   - Navigate to Members page
   - Click edit icon on any member
   - Check "Android Tester" checkbox
   - Changes save automatically

2. **Viewing Testers:**
   - Click "Testadores Android" button
   - View all designated testers
   - Search by name or username
   - See last login date for each tester

3. **Sending Custom Notifications:**
   - Open Android Testers modal
   - Type custom message (up to 500 characters)
   - Click "Enviar Notificação"
   - Wait for confirmation message

### For Android Testers

1. **Receiving Automated Reminders:**
   - Receive push notifications at configured times (default: 9am, 12pm, 3pm, 6pm, 9pm UTC)
   - Notification includes reminder to test the app for 5 minutes
   - Click notification to open the app
   - Once logged in, no more reminders for that day

2. **Receiving Custom Notifications:**
   - Receive push notification when owner sends custom message
   - Notification title: "RTUB - Testadores Android"
   - Click to open the app homepage

## Security & Privacy Considerations

### Access Control
- ✅ Only users with Owner role can designate Android testers
- ✅ Only users with Owner role can view the Android Testers modal
- ✅ Only users with Owner role can send custom notifications

### Data Privacy
- ✅ Android tester status is a simple boolean flag
- ✅ No additional personal data is collected
- ✅ Push notification subscriptions use existing WebPush infrastructure
- ✅ Users can unsubscribe from push notifications through browser settings

### Performance
- ✅ Background service uses scoped services to avoid memory leaks
- ✅ Notification tracking uses in-memory HashSet (cleared daily)
- ✅ Database queries use `AsNoTracking()` for read-only operations
- ✅ Pagination limits UI rendering to configurable page size

## Logging & Monitoring

### Log Events

The background service logs the following events:

| Level | Event | Example |
|-------|-------|---------|
| Information | Service started | "Android tester notification service started. Enabled: True, Times: 09:00, 12:00..." |
| Information | Notifications sent | "Sent Android tester reminder to 15 users at 09:00 UTC" |
| Debug | Next run scheduled | "Next Android tester notification check scheduled for 2026-02-01 12:00:00 UTC" |
| Debug | Daily reset | "Reset daily notification tracking for Android testers" |
| Debug | No testers found | "No Android testers found" |
| Debug | Campaign inactive | "Android tester campaign is not active today (2026-03-01)" |
| Debug | User already logged in | "Android tester user123 has already logged in today (2026-02-01 08:30:15), skipping remaining notifications" |
| Warning | Invalid time format | "Invalid notification time format: 25:00" |
| Error | Notification failure | "Error checking and sending Android tester notifications" |

### Monitoring Recommendations

1. **Check logs daily** during active campaigns for notification delivery
2. **Monitor login patterns** to understand tester engagement
3. **Review error logs** for notification failures
4. **Validate configuration** at service startup

## Best Practices

### Campaign Management

1. **Set Clear Date Ranges:**
   - Define `StartDate` and `EndDate` in configuration
   - Communicate testing period to testers in advance
   - Keep campaigns short and focused (1-4 weeks recommended)

2. **Notification Timing:**
   - Default times are spread throughout the day (9am, 12pm, 3pm, 6pm, 9pm UTC)
   - Adjust times based on tester time zones
   - Avoid too frequent notifications (minimum 3-hour intervals recommended)
   - Consider removing early morning or late night slots

3. **Tester Selection:**
   - Choose engaged and reliable users
   - Aim for diverse user profiles (different devices, usage patterns)
   - Limit group size to manageable number (10-30 testers recommended)
   - Rotate testers between campaigns to avoid fatigue

### Custom Notifications

1. **Message Quality:**
   - Keep messages clear and concise
   - Include specific testing goals (e.g., "Test the new gallery feature")
   - Add urgency when appropriate (e.g., "Please test before 6pm today")
   - Avoid generic messages

2. **Frequency:**
   - Don't overuse custom notifications
   - Reserve for important updates or urgent testing needs
   - Consider combining with automated reminders

### Monitoring & Feedback

1. **Track Engagement:**
   - Review last login dates in the testers modal
   - Identify inactive testers and follow up
   - Remove testers who consistently don't respond

2. **Gather Feedback:**
   - Create separate channel for tester feedback
   - Ask about notification frequency and timing
   - Adjust configuration based on feedback

3. **Performance Monitoring:**
   - Check background service logs for errors
   - Verify notifications are sent at expected times
   - Monitor push notification delivery rates

## Troubleshooting

### Notifications Not Sending

**Symptoms:** Android testers not receiving automated reminders

**Checklist:**
1. ✓ Is `AndroidTesterNotifications.Enabled` set to `true` in appsettings?
2. ✓ Is current date within `StartDate` and `EndDate` range?
3. ✓ Are `NotificationTimes` in valid format (HH:mm)?
4. ✓ Is background service running? (Check startup logs)
5. ✓ Are there users with `IsAndroidTester = true` in database?
6. ✓ Have users subscribed to push notifications?
7. ✓ Check error logs for exceptions

**Solutions:**
- Enable feature in configuration
- Update date range to include current date
- Fix invalid time formats
- Restart application
- Verify database records
- Ask users to enable push notifications in app

### Users Still Receiving Notifications After Login

**Symptoms:** Tester receives all 5 daily notifications even after logging in

**Possible Causes:**
1. `LastLoginDate` not being updated correctly
2. Time zone mismatch (login date in different zone than UTC)
3. Service restart cleared in-memory tracking

**Solutions:**
- Verify `LastLoginDate` is set on login
- Ensure dates are compared in UTC
- In-memory tracking resets on service restart (expected behavior)

### Custom Notifications Fail

**Symptoms:** Error when sending custom notification from modal

**Checklist:**
1. ✓ Is message not empty?
2. ✓ Are there Android testers in database?
3. ✓ Is push notification service configured?
4. ✓ Check browser console for JavaScript errors

**Solutions:**
- Ensure message field has content
- Verify users are marked as Android testers
- Check WebPush configuration
- Review browser console and server logs

## Future Enhancements

Potential improvements for future versions:

1. **Per-User Notification Preferences:**
   - Allow testers to choose their preferred notification times
   - Enable/disable automated reminders individually

2. **Analytics Dashboard:**
   - Track notification delivery rates
   - Monitor tester engagement metrics
   - Display testing activity trends

3. **A/B Testing:**
   - Test different notification messages
   - Compare engagement rates

4. **Localization:**
   - Multi-language notification support
   - Time zone-aware scheduling

5. **Notification History:**
   - Store sent notifications in database
   - Display history in admin panel

6. **Integration Testing:**
   - Automated tests for background service
   - End-to-end tests for notification flow

## Related Documentation

- [Configuration Guide](./android-tester-configuration.md) - Detailed configuration options
- [Component Documentation](./components/AndroidTestersButton.md) - UI component details
- [Backend Practices](./backend-practices.md) - General backend development guidelines
- [Frontend Practices](./frontend-practices.md) - UI component guidelines
- [PWA Practices](./pwa-practices.md) - Progressive Web App best practices

## Changelog

### [1.0] - 2026-02-01

**Added:**
- Initial release of Android Tester feature
- `IsAndroidTester` property on `ApplicationUser` entity
- Database migration for Android tester flag
- `AndroidTestersButton.razor` component for viewing and notifying testers
- `AndroidTesterNotificationBackgroundService` for automated reminders
- `AndroidTesterNotificationOptions` configuration class
- `CreateAndroidTesterReminderNotification` factory method
- Configuration section in `appsettings.json`
- Service registration in `Program.cs`

**Features:**
- Mark users as Android testers via checkbox in Members page
- View all Android testers with search and pagination
- Send custom push notifications to all Android testers
- Automated reminder notifications at configurable times
- Smart filtering to skip users who already logged in
- Campaign date range support (StartDate/EndDate)
- Daily notification tracking reset

---

**Document Author:** @rtub-docs-agent  
**Last Updated:** 2026-02-01  
**Review Status:** Initial Documentation

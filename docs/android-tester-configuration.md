# Android Tester Configuration Guide

**Date:** 2026-02-01  
**Version:** 1.0  
**Status:** Active

## Overview

This guide provides detailed information on configuring the Android Tester notification system through the `appsettings.json` file. The configuration controls automated reminder notifications sent to designated Android testers throughout the day.

## Configuration Location

**File:** `appsettings.json` (or environment-specific variants like `appsettings.Production.json`)  
**Section Name:** `AndroidTesterNotifications`

## Configuration Schema

```json
{
  "AndroidTesterNotifications": {
    "Enabled": false,
    "StartDate": "2026-02-01T00:00:00Z",
    "EndDate": "2026-02-28T23:59:59Z",
    "NotificationTimes": ["09:00", "12:00", "15:00", "18:00", "21:00"]
  }
}
```

## Configuration Properties

### Enabled

**Type:** `boolean`  
**Required:** Yes  
**Default:** `false`

Controls whether the automated Android tester notification system is active.

**Values:**
- `true` - Background service sends automated notifications at configured times
- `false` - Background service is disabled, no automated notifications sent

**Example:**
```json
{
  "AndroidTesterNotifications": {
    "Enabled": true
  }
}
```

**Usage Notes:**
- ✅ Set to `true` only during active testing campaigns
- ✅ Set to `false` to disable without removing service registration
- ✅ Can be toggled without redeploying application (requires app restart)
- ⚠️ Custom notifications via the UI still work when `Enabled: false`

**Best Practices:**
- Enable only when actively running a testing campaign
- Disable after campaign ends to conserve resources
- Use environment-specific settings for different environments

---

### StartDate

**Type:** `DateTime?` (nullable)  
**Required:** No  
**Default:** `null` (no start date restriction)  
**Format:** ISO 8601 (`YYYY-MM-DDTHH:mm:ssZ`)

Defines the beginning of the Android testing campaign. Notifications will only be sent on or after this date.

**Example:**
```json
{
  "AndroidTesterNotifications": {
    "StartDate": "2026-02-01T00:00:00Z"
  }
}
```

**Date Format Details:**
- Use UTC time zone (indicated by `Z` suffix)
- Time component can be set to `00:00:00` for start of day
- Year-Month-Day format: `YYYY-MM-DD`

**Behavior:**
- If `null`: No start date restriction, notifications can begin immediately
- If set: Notifications only sent when current UTC date ≥ StartDate

**Examples:**

```json
// Campaign starts February 1st, 2026 at midnight UTC
"StartDate": "2026-02-01T00:00:00Z"

// Campaign starts February 15th, 2026 at 8am UTC
"StartDate": "2026-02-15T08:00:00Z"

// No start date restriction
"StartDate": null
```

**Common Scenarios:**

| Scenario | Configuration | Effect |
|----------|---------------|--------|
| Future campaign | `"StartDate": "2026-03-01T00:00:00Z"` | Notifications don't send until March 1st |
| Immediate start | `"StartDate": null` | Notifications start based on Enabled flag |
| Delayed start | `"StartDate": "2026-02-05T00:00:00Z"` | Wait until February 5th to begin |

**Best Practices:**
- Always use UTC timezone to avoid confusion
- Set to midnight (`00:00:00`) unless specific time needed
- Test date parsing in staging environment before production
- Document timezone in team communications

---

### EndDate

**Type:** `DateTime?` (nullable)  
**Required:** No  
**Default:** `null` (no end date restriction)  
**Format:** ISO 8601 (`YYYY-MM-DDTHH:mm:ssZ`)

Defines the end of the Android testing campaign. Notifications will only be sent on or before this date.

**Example:**
```json
{
  "AndroidTesterNotifications": {
    "EndDate": "2026-02-28T23:59:59Z"
  }
}
```

**Date Format Details:**
- Use UTC time zone (indicated by `Z` suffix)
- Time component typically set to `23:59:59` for end of day
- Year-Month-Day format: `YYYY-MM-DD`

**Behavior:**
- If `null`: No end date restriction, notifications continue indefinitely (while Enabled)
- If set: Notifications only sent when current UTC date ≤ EndDate

**Examples:**

```json
// Campaign ends February 28th, 2026 at end of day
"EndDate": "2026-02-28T23:59:59Z"

// Campaign ends March 15th, 2026 at noon UTC
"EndDate": "2026-03-15T12:00:00Z"

// No end date restriction
"EndDate": null
```

**Common Scenarios:**

| Scenario | Configuration | Effect |
|----------|---------------|--------|
| One-week campaign | `"EndDate": "2026-02-07T23:59:59Z"` | Stops after 7 days |
| Month-long test | `"EndDate": "2026-02-28T23:59:59Z"` | Full February campaign |
| Indefinite | `"EndDate": null` | Continues until manually disabled |

**Best Practices:**
- Set to end of day (`23:59:59`) unless specific time needed
- Always define end date for campaigns (avoid indefinite notifications)
- Include buffer time for wrap-up activities
- Consider weekends and holidays in date selection

---

### NotificationTimes

**Type:** `List<string>`  
**Required:** Yes (must have at least one time)  
**Default:** `["09:00", "12:00", "15:00", "18:00", "21:00"]`  
**Format:** `HH:mm` (24-hour format)

List of times during the day when automated reminder notifications should be sent to Android testers.

**Example:**
```json
{
  "AndroidTesterNotifications": {
    "NotificationTimes": ["09:00", "12:00", "15:00", "18:00", "21:00"]
  }
}
```

**Format Requirements:**
- ✅ 24-hour time format (00:00 to 23:59)
- ✅ HH:mm format (e.g., "09:00", "14:30", "18:15")
- ✅ Leading zero required for hours (e.g., "09:00" not "9:00")
- ✅ All times interpreted as UTC timezone
- ❌ Invalid: "9:00", "25:00", "12:75", "12pm", "12:00:00"

**Validation:**
- Service validates format at startup
- Throws `InvalidOperationException` if any time is invalid
- Application fails to start if validation fails
- Logs validation results at Information level

**Examples:**

```json
// Default times (9am, 12pm, 3pm, 6pm, 9pm UTC)
"NotificationTimes": ["09:00", "12:00", "15:00", "18:00", "21:00"]

// Morning and evening only
"NotificationTimes": ["08:00", "20:00"]

// Every 3 hours throughout the day
"NotificationTimes": ["06:00", "09:00", "12:00", "15:00", "18:00", "21:00"]

// Single midday reminder
"NotificationTimes": ["12:00"]

// Business hours only (9am-5pm)
"NotificationTimes": ["09:00", "12:00", "15:00", "17:00"]
```

**Scheduling Behavior:**

The background service:
1. Sorts all times in chronological order
2. Calculates next upcoming time
3. Waits until that time
4. Sends notifications
5. Moves to next time
6. At end of day, schedules first time next day

**Example Timeline:**

```
Current Time: 10:30 UTC
Times: ["09:00", "12:00", "15:00", "18:00", "21:00"]

→ Next notification: 12:00 (today)
→ After 12:00 runs: 15:00 (today)
→ After 15:00 runs: 18:00 (today)
→ After 18:00 runs: 21:00 (today)
→ After 21:00 runs: 09:00 (tomorrow)
```

**Timezone Considerations:**

| User Timezone | UTC Time "09:00" | User's Local Time |
|---------------|------------------|-------------------|
| Lisbon (UTC+0) | 09:00 | 09:00 (9am) |
| New York (UTC-5) | 09:00 | 04:00 (4am) ⚠️ |
| Tokyo (UTC+9) | 09:00 | 18:00 (6pm) |
| London (UTC+0/+1) | 09:00 | 09:00/10:00 |

**Best Practices:**
- **Frequency:** Space notifications 3-4 hours apart minimum
- **Count:** 3-5 notifications per day recommended (avoid notification fatigue)
- **Timing:** Consider primary tester timezones
- **Avoid:** Very early morning or late night times
- **Test:** Verify times work for majority of testers
- **Adjust:** Update based on tester feedback

**Common Configurations:**

```json
// Light touch (2 reminders/day)
"NotificationTimes": ["10:00", "18:00"]

// Moderate (3 reminders/day)
"NotificationTimes": ["09:00", "14:00", "20:00"]

// Standard (5 reminders/day) - Default
"NotificationTimes": ["09:00", "12:00", "15:00", "18:00", "21:00"]

// Intensive (7 reminders/day) - Not recommended
"NotificationTimes": ["08:00", "10:00", "12:00", "14:00", "16:00", "18:00", "20:00"]

// Weekend-friendly (late morning/evening)
"NotificationTimes": ["11:00", "19:00"]
```

---

## Complete Configuration Examples

### Example 1: Standard One-Month Campaign

```json
{
  "AndroidTesterNotifications": {
    "Enabled": true,
    "StartDate": "2026-02-01T00:00:00Z",
    "EndDate": "2026-02-28T23:59:59Z",
    "NotificationTimes": ["09:00", "12:00", "15:00", "18:00", "21:00"]
  }
}
```

**Use Case:** February 2026 testing campaign with 5 daily reminders

---

### Example 2: Weekend Testing Sprint

```json
{
  "AndroidTesterNotifications": {
    "Enabled": true,
    "StartDate": "2026-02-14T00:00:00Z",
    "EndDate": "2026-02-16T23:59:59Z",
    "NotificationTimes": ["10:00", "14:00", "19:00"]
  }
}
```

**Use Case:** Three-day weekend testing event with relaxed timing

---

### Example 3: Two-Week Intensive Testing

```json
{
  "AndroidTesterNotifications": {
    "Enabled": true,
    "StartDate": "2026-03-01T00:00:00Z",
    "EndDate": "2026-03-14T23:59:59Z",
    "NotificationTimes": ["08:00", "11:00", "14:00", "17:00", "20:00"]
  }
}
```

**Use Case:** Intensive two-week campaign with 6 reminders per day

---

### Example 4: Disabled Configuration

```json
{
  "AndroidTesterNotifications": {
    "Enabled": false,
    "StartDate": null,
    "EndDate": null,
    "NotificationTimes": ["09:00", "12:00", "15:00", "18:00", "21:00"]
  }
}
```

**Use Case:** No active campaign, service disabled but configuration preserved

---

### Example 5: Minimal Configuration

```json
{
  "AndroidTesterNotifications": {
    "Enabled": true,
    "NotificationTimes": ["12:00"]
  }
}
```

**Use Case:** Single daily reminder with no date restrictions

---

## Environment-Specific Configuration

### Development Environment

**File:** `appsettings.Development.json`

```json
{
  "AndroidTesterNotifications": {
    "Enabled": true,
    "StartDate": null,
    "EndDate": null,
    "NotificationTimes": ["10:00"]
  },
  "Logging": {
    "LogLevel": {
      "RTUB.Application.Services.AndroidTesterNotificationBackgroundService": "Debug"
    }
  }
}
```

**Rationale:**
- Frequent testing without date restrictions
- Single notification time to reduce noise
- Debug logging enabled for service

---

### Staging Environment

**File:** `appsettings.Staging.json`

```json
{
  "AndroidTesterNotifications": {
    "Enabled": true,
    "StartDate": "2026-02-01T00:00:00Z",
    "EndDate": "2026-02-07T23:59:59Z",
    "NotificationTimes": ["11:00", "15:00"]
  }
}
```

**Rationale:**
- One-week test campaign in staging
- Reduced notification frequency
- Matches production configuration structure

---

### Production Environment

**File:** `appsettings.Production.json`

```json
{
  "AndroidTesterNotifications": {
    "Enabled": true,
    "StartDate": "2026-02-01T00:00:00Z",
    "EndDate": "2026-02-28T23:59:59Z",
    "NotificationTimes": ["09:00", "12:00", "15:00", "18:00", "21:00"]
  },
  "Logging": {
    "LogLevel": {
      "RTUB.Application.Services.AndroidTesterNotificationBackgroundService": "Information"
    }
  }
}
```

**Rationale:**
- Full month campaign with defined dates
- Standard 5 notifications per day
- Information-level logging for production monitoring

---

## Configuration Validation

### Startup Validation

The `AndroidTesterNotificationBackgroundService` validates configuration at startup:

1. **NotificationTimes Presence Check:**
   - Ensures list is not null or empty
   - Throws exception if missing

2. **Time Format Validation:**
   - Validates each time string can be parsed as TimeSpan
   - Throws exception listing all invalid formats

3. **Success Logging:**
   - Logs validated times at Information level

**Example Log Output:**

```
[2026-02-01 09:00:00 INF] Validated 5 notification time(s): 09:00, 12:00, 15:00, 18:00, 21:00
[2026-02-01 09:00:00 INF] Android tester notification service started. Enabled: True, Times: 09:00, 12:00, 15:00, 18:00, 21:00
```

**Error Example:**

```json
{
  "AndroidTesterNotifications": {
    "Enabled": true,
    "NotificationTimes": ["09:00", "25:00", "invalid"]
  }
}
```

**Result:**
```
System.InvalidOperationException: AndroidTesterNotificationOptions.NotificationTimes contains invalid time format(s): 25:00, invalid. Expected format: HH:mm (e.g., '09:00', '15:30')
```

---

## Runtime Behavior

### Service Lifecycle

1. **Application Startup:**
   - Service registered as `IHostedService`
   - Waits 20 seconds before starting (allows app initialization)

2. **Configuration Loading:**
   - Reads from `appsettings.json` via `IOptions<AndroidTesterNotificationOptions>`
   - Validates notification times
   - Logs configuration

3. **Operation Loop:**
   - Calculates next notification time
   - Waits until that time
   - Checks if enabled
   - Checks if campaign is active (date range)
   - Queries Android testers
   - Filters already-logged-in users
   - Sends notifications
   - Repeats

4. **Daily Reset:**
   - At midnight UTC, clears in-memory tracking
   - Allows users to receive notifications again next day

---

## Troubleshooting Configuration Issues

### Problem: Service Not Starting

**Symptoms:**
- No log messages from AndroidTesterNotificationBackgroundService
- Application starts but notifications never sent

**Possible Causes:**
1. Invalid time format in NotificationTimes
2. Empty NotificationTimes array
3. Exception during service registration

**Solutions:**
- Check application startup logs for exceptions
- Validate JSON syntax in appsettings.json
- Ensure all times match HH:mm format
- Verify service is registered in Program.cs

---

### Problem: Notifications Not Sending

**Symptoms:**
- Service logs show it's running
- No notifications received by testers

**Checklist:**
```
✓ Is Enabled: true?
✓ Is current date >= StartDate?
✓ Is current date <= EndDate?
✓ Are there users with IsAndroidTester = true?
✓ Have users subscribed to push notifications?
✓ Is push notification service configured?
```

**Debug Steps:**
1. Enable Debug logging for the service
2. Check logs at scheduled notification times
3. Verify database has Android testers
4. Test custom notification from UI (bypasses scheduling)

---

### Problem: Wrong Notification Times

**Symptoms:**
- Notifications sent at unexpected times
- Times don't match configuration

**Possible Causes:**
1. Timezone confusion (all times are UTC)
2. Application not restarted after config change
3. Wrong appsettings file loaded

**Solutions:**
- Verify times are in UTC (not local timezone)
- Restart application to reload configuration
- Check which appsettings file is active (Development/Staging/Production)
- Add logging to confirm loaded configuration

---

## Configuration Change Workflow

### Step-by-Step Process

1. **Backup Current Configuration:**
   ```bash
   cp appsettings.json appsettings.json.backup
   ```

2. **Edit Configuration:**
   - Open `appsettings.json` (or environment-specific file)
   - Modify `AndroidTesterNotifications` section
   - Validate JSON syntax

3. **Validate Changes:**
   - Check time format (HH:mm)
   - Verify dates are ISO 8601 format
   - Confirm enabled flag is boolean

4. **Test in Development:**
   - Update `appsettings.Development.json`
   - Run application locally
   - Verify startup logs show correct configuration
   - Test notification sending

5. **Deploy to Staging:**
   - Update `appsettings.Staging.json`
   - Deploy application
   - Monitor logs for validation success
   - Test with staging testers

6. **Deploy to Production:**
   - Update `appsettings.Production.json`
   - Deploy application
   - Monitor startup logs
   - Verify first notification sent correctly

7. **Monitor:**
   - Check logs at first scheduled time
   - Confirm notifications sent to correct users
   - Review tester feedback

---

## Security Considerations

### Configuration Security

- ✅ Configuration file should be read-only in production
- ✅ Use environment variables for sensitive settings (if applicable)
- ✅ Validate configuration in CI/CD pipeline
- ✅ Restrict access to production configuration files

### Best Practices

1. **Version Control:**
   - Commit appsettings.json to version control
   - Use .gitignore for appsettings.Production.json with secrets
   - Document configuration changes in commit messages

2. **Deployment:**
   - Validate configuration before deploying
   - Use deployment scripts to apply configuration
   - Keep backup of previous configuration

3. **Monitoring:**
   - Alert on service startup failures
   - Monitor notification delivery rates
   - Track configuration changes in audit log

---

## Related Documentation

- [Android Tester Feature Overview](./android-tester-feature.md) - Main feature documentation
- [Component Documentation](./components/AndroidTestersButton.md) - UI component details
- [Backend Practices](./backend-practices.md) - Background service patterns

---

## Appendix: Configuration Class Reference

### AndroidTesterNotificationOptions.cs

```csharp
namespace RTUB.Application.Configuration;

public class AndroidTesterNotificationOptions
{
    public const string SectionName = "AndroidTesterNotifications";

    /// <summary>
    /// Whether automatic Android tester reminder notifications are enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Start date of the Android testing campaign.
    /// Notifications will only be sent on or after this date.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date of the Android testing campaign.
    /// Notifications will only be sent on or before this date.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// List of times during the day to send notifications (format: "HH:mm").
    /// Default times: 09:00, 12:00, 15:00, 18:00, 21:00 (UTC).
    /// </summary>
    public List<string> NotificationTimes { get; set; } = new()
    {
        "09:00",
        "12:00",
        "15:00",
        "18:00",
        "21:00"
    };
}
```

---

## Changelog

### [1.0] - 2026-02-01

**Added:**
- Initial configuration documentation
- Complete property reference
- Environment-specific examples
- Validation rules and error handling
- Troubleshooting guide
- Configuration change workflow

---

**Document Author:** @rtub-docs-agent  
**Last Updated:** 2026-02-01  
**Review Status:** Initial Documentation

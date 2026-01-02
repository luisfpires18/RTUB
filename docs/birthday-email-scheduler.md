# Birthday Email Scheduler

## Overview

The Birthday Email Scheduler is an automated background service that sends birthday notifications to all subscribed members daily. This replaces the previous manual process where admins had to click an action button in the Members page to send birthday emails.

## How It Works

### Automatic Scheduling

The service runs as a `BackgroundService` that:
1. Starts automatically when the application starts
2. Checks for birthdays once per day at a configurable time (default: 09:00 UTC)
3. Sends personalized birthday emails to all subscribed members for each person with a birthday on that day
4. Continues running in the background throughout the application lifetime

### Email Recipients

Birthday notifications are sent to:
- All members with `Subscribed = true`
- With confirmed email addresses (`EmailConfirmed = true`)
- With valid email addresses (not null or empty)

### Birthday Detection

The service identifies members with birthdays by:
- Comparing the current day and month (UTC) with each member's `DateOfBirth`
- Only users with a `DateOfBirth` value set are eligible

## Configuration

Configuration is managed through `appsettings.json`:

```json
{
  "BirthdayEmailScheduler": {
    "Enabled": true,
    "ScheduledTime": "09:00"
  }
}
```

### Configuration Options

- **`Enabled`** (boolean, default: `true`): Controls whether the scheduler is active
  - Set to `false` to disable automatic birthday emails
  
- **`ScheduledTime`** (string, format: "HH:mm", default: "09:00"): The time of day to send birthday emails in UTC
  - Uses 24-hour format
  - Represents UTC time (not local time)
  - Examples: "09:00" (9 AM UTC), "14:30" (2:30 PM UTC)

## Manual Override

Administrators can still manually send birthday emails:
1. Navigate to the Members page
2. Click the "Aniversários" (Birthdays) button
3. Find the member whose birthday is today
4. Click the envelope button to send the email manually

This is useful for:
- Testing the email functionality
- Resending emails if needed
- Sending emails outside the normal schedule

## Technical Implementation

### Service Components

1. **`BirthdayEmailSchedulerService`** (`src/RTUB.Application/Services/BirthdayEmailSchedulerService.cs`)
   - Implements `BackgroundService`
   - Runs continuously in the background
   - Manages scheduling and execution

2. **`BirthdayEmailSchedulerOptions`** (`src/RTUB.Application/Configuration/BirthdayEmailSchedulerOptions.cs`)
   - Configuration model
   - Provides strongly-typed access to settings

3. **Registration** (`src/RTUB.Web/Program.cs`)
   - Service registered as `AddHostedService<BirthdayEmailSchedulerService>()`
   - Configuration bound to options pattern

### Key Features

- **UTC Timezone Handling**: All datetime operations use UTC to avoid timezone issues
- **Once-per-day Execution**: Tracks last run date to prevent duplicate sends
- **Error Handling**: Comprehensive logging and error recovery
- **Rate Limiting**: Built-in delay between multiple birthday emails to avoid SMTP server overload
- **Cancellation Support**: Properly handles application shutdown

### Logging

The service logs the following events:
- Startup with configuration details
- Daily birthday check results (number of birthdays found)
- Success/failure for each email sent
- Errors and warnings

Log messages are prefixed with the service name for easy filtering.

## Deployment Considerations

### Production Setup

1. **Email Configuration**: Ensure SMTP settings are properly configured in production
2. **Timezone Awareness**: The `ScheduledTime` is in UTC, so calculate the appropriate time based on your local timezone
3. **Subscription Management**: Ensure members have opted in to email notifications

### Monitoring

Monitor the service by:
- Checking application logs for birthday email activity
- Verifying emails are sent at the scheduled time
- Monitoring for error messages related to `BirthdayEmailSchedulerService`

### Disabling the Service

To disable automatic birthday emails:
1. Update `appsettings.json` (or environment-specific configuration)
2. Set `BirthdayEmailScheduler:Enabled` to `false`
3. Restart the application

Alternatively, remove the service registration from `Program.cs` (requires code change and deployment).

## Future Enhancements

Potential improvements for future releases:
- Make email delay configurable
- Add support for local timezone configuration
- Implement database indexing on birthday fields for better performance with large user tables
- Add admin dashboard for viewing scheduled emails
- Support for advance birthday notifications (e.g., tomorrow's birthdays)

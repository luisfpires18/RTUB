# Android Tester Usage Guide

**Date:** 2026-02-01  
**Version:** 1.0  
**Status:** Active

## Overview

This guide provides practical, step-by-step instructions for using the Android Tester feature. It covers both owner (administrator) workflows and tester (end-user) experiences.

---

## For Owners/Administrators

### Setting Up a Testing Campaign

#### Step 1: Configure the Campaign

1. **Open Configuration File:**
   - Navigate to your project root
   - Edit `appsettings.json` (or environment-specific file)

2. **Set Campaign Dates:**
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

3. **Adjust Notification Times (Optional):**
   - Consider your testers' timezones
   - All times are in UTC
   - Recommended: 3-5 notifications per day, 3-4 hours apart

4. **Save and Deploy:**
   - Save the configuration file
   - Deploy/restart the application

---

#### Step 2: Designate Android Testers

1. **Navigate to Members Page:**
   - Log in as Owner
   - Go to `/members` or click "Membros" in navigation

2. **Select Testers:**
   - Identify users you want as Android testers
   - Click the edit icon (✏️) next to their name

3. **Enable Android Tester Flag:**
   - In the edit modal, scroll to "Android Tester" section
   - Check the "Android Tester" checkbox
   - Click "Save" or "Update"

4. **Repeat for All Testers:**
   - Repeat for each user you want to designate
   - Recommended: 10-30 testers for manageable group

**Visual Reference:**
```
Members Page → Edit User → ✓ Android Tester → Save
```

---

#### Step 3: Verify Configuration

1. **Check Android Testers List:**
   - Click "Testadores Android" button (available on relevant pages)
   - Modal opens showing all designated testers
   - Verify all expected users appear in the list

2. **Verify Application Logs:**
   - Check startup logs for configuration validation
   - Look for: `"Android tester notification service started. Enabled: True..."`
   - Verify no validation errors

**Expected Log Output:**
```
[2026-02-01 09:00:00 INF] Validated 5 notification time(s): 09:00, 12:00, 15:00, 18:00, 21:00
[2026-02-01 09:00:00 INF] Android tester notification service started. Enabled: True, Times: 09:00, 12:00, 15:00, 18:00, 21:00
```

---

### Managing Android Testers

#### Viewing All Testers

1. **Open Android Testers Modal:**
   - Click "Testadores Android" button
   - Modal displays with full list of testers

2. **Review Tester Information:**
   - Avatar/profile picture
   - Nickname or username
   - Full name
   - **Last Login Date** (important for tracking engagement)

3. **Search for Specific Tester:**
   - Use search bar at top of modal
   - Type nickname, username, or full name
   - Results filter in real-time

4. **Pagination:**
   - Use pagination controls at bottom
   - Change items per page (12, 24, 36, 48, 60)
   - Navigate between pages

**Example Workflow:**
```
Click "Testadores Android" → View list → Search "João" → Review last login dates
```

---

#### Removing Android Testers

1. **Navigate to Members Page:**
   - Go to `/members`

2. **Find User to Remove:**
   - Search or browse for the user
   - Click edit icon (✏️)

3. **Disable Android Tester Flag:**
   - Uncheck the "Android Tester" checkbox
   - Click "Save"

4. **Verify Removal:**
   - Open Android Testers modal
   - Confirm user no longer appears in list

**Note:** Removing a user as an Android tester stops all future automated notifications for that user. They can still receive general push notifications.

---

### Sending Custom Notifications

Custom notifications allow you to send immediate, personalized messages to all Android testers outside of the automated schedule.

#### When to Use Custom Notifications

✅ **Good Use Cases:**
- Announce new feature to test immediately
- Request urgent feedback on specific functionality
- Inform testers of known issues or workarounds
- Thank testers for participation
- Remind about specific testing deadline

❌ **Avoid:**
- Generic "please test" messages (use automated reminders)
- Too frequent custom messages (causes notification fatigue)
- Non-urgent communications (use email or other channels)

---

#### Sending Process

1. **Open Android Testers Modal:**
   - Click "Testadores Android" button

2. **Locate Custom Notification Section:**
   - At the top of the modal
   - Gray background box titled "Enviar Notificação Personalizada"

3. **Compose Message:**
   - Click in the text area
   - Type your message (max 500 characters)
   - Character count displays below text area

4. **Review Message:**
   - Ensure message is clear and actionable
   - Check for typos or unclear wording
   - Verify it fits within 500 character limit

5. **Send Notification:**
   - Click "Enviar Notificação" button
   - Button shows spinner while sending
   - Wait for success/error message

6. **Verify Success:**
   - Green success message appears: "Notificação enviada com sucesso para X testador(es) Android!"
   - Message field clears automatically
   - Notification sent to all current Android testers

**Example Messages:**

```
✅ Good:
"Novo recurso de galeria disponível! Por favor, teste upload de fotos e vídeos nos próximos 30 minutos. Relatem qualquer problema."

✅ Good:
"Atenção: Bug conhecido ao fazer login com Google. Usem email/senha por enquanto. Fix em breve!"

✅ Good:
"Último dia de testes! Obrigado pela vossa ajuda. Feedback final bem-vindo até às 18h."

❌ Too Generic:
"Por favor testem a app."

❌ Too Long:
[501 character message that gets cut off]
```

---

#### Notification Details

When you send a custom notification, testers receive:

- **Title:** "RTUB - Testadores Android"
- **Body:** Your custom message
- **Icon:** RTUB logo (512x512)
- **Action:** Clicking opens app homepage
- **Tag:** `android-testers-notification` (replaces previous custom notification)

---

### Monitoring Campaign Progress

#### Daily Monitoring Checklist

1. **Check Application Logs:**
   - Review logs at scheduled notification times
   - Look for: `"Sent Android tester reminder to X users at HH:mm UTC"`
   - Verify notification counts match expectations

2. **Review Tester Engagement:**
   - Open Android Testers modal
   - Sort by last login date
   - Identify testers who haven't logged in recently

3. **Follow Up with Inactive Testers:**
   - Contact testers with no recent login
   - Ask if they're receiving notifications
   - Verify push notifications are enabled in browser

**Example Log Monitoring:**
```
[2026-02-01 09:00:15 INF] Sent Android tester reminder to 12 users at 09:00 UTC
[2026-02-01 12:00:15 INF] Sent Android tester reminder to 8 users at 12:00 UTC
[2026-02-01 15:00:15 INF] Sent Android tester reminder to 5 users at 15:00 UTC
```

**Interpretation:**
- At 9am: 12 users hadn't logged in yet → received notification
- At 12pm: 4 users logged in between 9am-12pm → only 8 received notification
- At 3pm: 3 more users logged in → only 5 received notification
- Smart filtering working correctly ✓

---

#### Weekly Review

1. **Engagement Analysis:**
   - Calculate % of testers who logged in each day
   - Identify consistently inactive testers
   - Consider removing testers with 0% engagement

2. **Notification Effectiveness:**
   - Review if notification times work for testers
   - Consider adjusting times based on login patterns
   - Survey testers about notification frequency

3. **Issue Tracking:**
   - Collect feedback from testers
   - Document reported bugs or issues
   - Share findings with development team

---

### Ending a Campaign

#### Step 1: Disable Automated Notifications

**Option A: Wait for End Date**
- If `EndDate` is configured, service automatically stops on that date
- No action needed

**Option B: Manual Disable**
1. Edit `appsettings.json`
2. Set `"Enabled": false`
3. Save and deploy/restart application

```json
{
  "AndroidTesterNotifications": {
    "Enabled": false
  }
}
```

---

#### Step 2: Send Thank You Message

1. Open Android Testers modal
2. Compose final custom notification:
   ```
   Campanha de testes concluída! Obrigado a todos pelo tempo e feedback. 
   A vossa ajuda foi essencial para melhorar a app Android.
   ```
3. Send notification

---

#### Step 3: Review Results

1. Export or document:
   - Total number of testers
   - Engagement rates
   - Key findings and bugs discovered
   - Tester feedback summary

2. Share results with team

---

#### Step 4: Clean Up (Optional)

**Option A: Keep Testers Flagged**
- Leave `IsAndroidTester` flag enabled
- Easy to restart campaign in future
- No impact when feature is disabled

**Option B: Remove All Tester Flags**
- Go through each tester in Members page
- Uncheck "Android Tester" checkbox
- Clean database state

**Recommendation:** Keep flags in place for future campaigns.

---

## For Android Testers

### Getting Started

#### Receiving Your First Notification

1. **Owner Designates You:**
   - Owner marks your account as Android Tester
   - You'll automatically start receiving notifications

2. **Enable Push Notifications:**
   - Open RTUB app in browser
   - When prompted, click "Allow" for push notifications
   - (If missed, go to browser settings to enable)

3. **Wait for First Notification:**
   - First notification arrives at next scheduled time
   - Default times: 9am, 12pm, 3pm, 6pm, 9pm UTC

---

#### Understanding Notifications

**Automated Reminders:**
- **Frequency:** Up to 5 times per day (based on configuration)
- **Content:** "Você é um Android tester e precisamos que entres na app durante 5 minutos hoje."
- **Purpose:** Reminder to test the app
- **Smart Behavior:** Once you log in, no more reminders that day

**Custom Messages:**
- **Frequency:** Occasional (sent by owner as needed)
- **Content:** Specific instructions or updates
- **Title:** "RTUB - Testadores Android"
- **Purpose:** Urgent updates, specific feature testing, announcements

---

### Daily Testing Routine

#### Recommended Workflow

1. **Receive Notification:**
   - Notification appears on your Android device
   - Read the message

2. **Open the App:**
   - Click the notification
   - App opens in browser
   - OR: Open bookmarked app manually

3. **Log In:**
   - Enter your credentials
   - Complete login process

4. **Test for 5 Minutes:**
   - Navigate through different pages
   - Try key features (galleries, events, rankings, etc.)
   - Use app as you normally would

5. **Note Any Issues:**
   - Take screenshots of bugs
   - Write down error messages
   - Note unexpected behavior

6. **Report Problems:**
   - Contact owner or designated feedback channel
   - Provide details about issues
   - Include screenshots if applicable

---

### Managing Notifications

#### Stopping Notifications for the Day

**Option 1: Log In Early**
- Log in to the app before first notification
- System detects your login
- No notifications sent for rest of day
- Smart and automatic ✓

**Example:**
```
Campaign Time: 9am, 12pm, 3pm, 6pm, 9pm
You log in at: 8:30am
Result: No notifications sent today (system knows you're active)
```

---

**Option 2: Turn Off Browser Notifications (Temporary)**
- Android Settings → Apps → Chrome/Browser → Notifications → Off
- Re-enable when ready to receive again
- Note: Disables all notifications from browser, not just RTUB

---

**Option 3: Disable in Browser Settings (Per-Site)**
- Open Chrome on Android
- Go to RTUB site
- Tap lock icon → Settings → Notifications → Block
- More targeted than Option 2

---

#### If You Can't Test

If you're unable to participate in testing for a period:

1. **Inform the Owner:**
   - Let them know you'll be unavailable
   - Provide dates if possible

2. **Owner Can Remove You Temporarily:**
   - Owner unchecks your Android Tester flag
   - Stops all notifications
   - Can re-enable later

3. **Or: Ignore Notifications:**
   - Simply don't click notifications
   - They'll stop after you log in next time
   - No harm in receiving them

---

### Best Practices for Testers

#### Maximize Your Impact

✅ **Do:**
- Test daily during campaign if possible
- Try different features each session
- Report issues promptly with details
- Note both bugs AND positive experiences
- Test on different network conditions (WiFi, mobile data)
- Try offline functionality
- Test during different times of day

❌ **Don't:**
- Just click notification and close app (defeats purpose)
- Ignore obvious bugs without reporting
- Disable notifications without telling owner
- Test only one feature repeatedly

---

#### Reporting Issues

**Good Bug Report:**
```
Problem: Gallery upload failed
Steps:
1. Clicked "Gallery" menu
2. Selected "Upload Photos"
3. Chose 3 photos from device
4. Clicked "Upload"
5. Spinning icon appeared but never finished

Error: No error message shown
Device: Samsung Galaxy S21
Browser: Chrome 98
Time: 2026-02-01 14:30
```

**Bad Bug Report:**
```
Upload doesn't work
```

---

### Understanding the Smart Notification System

#### How It Saves You from Notification Spam

The system is designed to be respectful of your time:

```
Day 1: You receive 5 notifications (don't log in)
Day 2: You log in at 10am
        → No more notifications for Day 2 ✓
Day 3: Fresh start → Can receive up to 5 again
        → You log in at 2pm
        → No more notifications for Day 3 ✓
```

#### Why This Matters

- **Prevents Spam:** No redundant reminders after you're active
- **Respects Your Time:** System knows you're engaged
- **Encourages Testing:** Gentle reminders if you forget
- **Daily Reset:** Fresh start each day

---

## Common Scenarios & Solutions

### Scenario 1: Not Receiving Notifications

**Check:**
1. Are push notifications enabled in browser?
2. Is your account marked as Android Tester?
3. Is your device connected to internet?
4. Check browser notification settings for RTUB site

**Solution:**
- Contact owner to verify you're designated as tester
- Re-enable push notifications in app
- Check device notification settings
- Try on different browser

---

### Scenario 2: Too Many Notifications

**Check:**
1. Are you logging in daily?
2. What times are configured for notifications?

**Solution:**
- Log in early in the day to stop further notifications
- Contact owner to request fewer notification times
- Temporarily disable browser notifications

---

### Scenario 3: Notifications at Wrong Times

**Check:**
1. What timezone are you in?
2. All notification times are UTC

**Example:**
```
Configured Time: 09:00 UTC
Your Timezone: Lisbon (UTC+0)
Your Local Time: 09:00 (9am) ✓

Configured Time: 09:00 UTC  
Your Timezone: New York (UTC-5)
Your Local Time: 04:00 (4am) ✗
```

**Solution:**
- Contact owner to adjust times for your timezone
- Owner can modify `NotificationTimes` in configuration

---

### Scenario 4: Notification Opens Wrong Page

**Issue:** Notification should open specific page but opens homepage

**Explanation:**
- Automated reminders intentionally open homepage
- Custom notifications may specify different URLs
- This is expected behavior

---

## Tips & Tricks

### For Owners

1. **Start Small:**
   - Begin with 10-15 testers
   - Expand if needed
   - Easier to manage feedback

2. **Communicate Clearly:**
   - Set expectations before campaign
   - Explain purpose and duration
   - Provide feedback channel

3. **Monitor Actively:**
   - Check logs daily
   - Reach out to inactive testers
   - Acknowledge participation

4. **Be Flexible:**
   - Adjust times based on feedback
   - Remove inactive testers
   - Modify campaign duration as needed

5. **Show Appreciation:**
   - Send thank you messages
   - Recognize top testers
   - Share results after campaign

---

### For Testers

1. **Set a Routine:**
   - Test at same time daily
   - Easier to remember
   - Becomes habit

2. **Vary Your Testing:**
   - Different features each day
   - Try edge cases
   - Think like a user

3. **Keep Notes:**
   - Document issues immediately
   - Screenshots are valuable
   - Track patterns

4. **Be Proactive:**
   - Test even without notification
   - Explore new features
   - Provide positive feedback too

---

## Frequently Asked Questions

### For Owners

**Q: Can I change notification times mid-campaign?**  
A: Yes, update `appsettings.json` and restart the app. Changes take effect immediately.

**Q: What happens if I add new testers mid-campaign?**  
A: They start receiving notifications at the next scheduled time. No historical notifications sent.

**Q: Can testers opt-out of automated notifications?**  
A: Yes, by disabling browser notifications or asking you to remove their Android Tester flag.

**Q: Do custom notifications count toward daily limit?**  
A: No, custom notifications are independent of automated reminders. Use sparingly.

**Q: Can I target specific testers with custom notifications?**  
A: Not currently. Custom notifications go to all Android testers. For specific users, use other communication channels.

---

### For Testers

**Q: Will I receive notifications even on weekends?**  
A: Yes, unless owner configures different times. Service runs every day during campaign.

**Q: What if I'm traveling to different timezone?**  
A: Notifications are sent based on UTC time, so local time changes. You may receive them at different hours.

**Q: Can I test on iPhone/iOS?**  
A: This feature is specifically for Android testing, but iOS users can test the PWA independently.

**Q: How long do testing campaigns usually last?**  
A: Typically 1-4 weeks. Owner sets specific dates in configuration.

**Q: What if I accidentally delete a notification?**  
A: No problem. Next scheduled notification will arrive. Or just open the app manually.

---

## Checklist: Campaign Launch

### Pre-Launch (Owner)

- [ ] Configure `appsettings.json` with campaign dates
- [ ] Set appropriate notification times for testers' timezones
- [ ] Enable the feature (`Enabled: true`)
- [ ] Deploy/restart application
- [ ] Verify startup logs show correct configuration
- [ ] Designate all Android testers in Members page
- [ ] Verify testers appear in Android Testers modal
- [ ] Inform testers about campaign start date
- [ ] Provide instructions for enabling notifications
- [ ] Set up feedback/reporting channel

### During Campaign (Owner)

- [ ] Monitor logs at notification times
- [ ] Check tester engagement weekly
- [ ] Follow up with inactive testers
- [ ] Send custom notifications for special announcements
- [ ] Collect and triage reported issues
- [ ] Adjust configuration if needed

### Post-Campaign (Owner)

- [ ] Disable feature or wait for end date
- [ ] Send thank you notification to all testers
- [ ] Export/document engagement metrics
- [ ] Summarize findings and bugs
- [ ] Share results with team
- [ ] Decide whether to keep tester flags enabled

---

## Related Documentation

- [Android Tester Feature Overview](./android-tester-feature.md) - Technical details and architecture
- [Android Tester Configuration Guide](./android-tester-configuration.md) - Detailed configuration reference

---

## Support

For questions or issues with the Android Tester feature:

1. **Technical Issues:** Contact system administrator or owner
2. **Configuration Help:** Refer to [Configuration Guide](./android-tester-configuration.md)
3. **Feature Requests:** Submit through normal feedback channels
4. **Bug Reports:** Use established bug reporting process

---

**Document Author:** @rtub-docs-agent  
**Last Updated:** 2026-02-01  
**Review Status:** Initial Documentation

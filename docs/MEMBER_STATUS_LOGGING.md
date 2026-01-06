# Member Status Update Logging Examples

This document shows examples of the enhanced logging output for member status updates.

## Configuration

The member status update service is configured via `appsettings.json`:

```json
"MemberStatusUpdate": {
  "Enabled": true,
  "ScheduledTime": "21:00"
}
```

- **Enabled**: Set to `false` to disable automatic member status updates
- **ScheduledTime**: Time of day (24-hour format) when the scheduled update runs. Default is 21:00 (9 PM)

The service runs:
1. **Once on application startup** - Ensures data is available immediately after deployment
2. **Once per day at the configured time** - Keeps data up-to-date

This ensures "Gestao de membros ativos" always has current data, even immediately after deployment.

## Background Service Logs

When the background service runs (daily at 21:00), you'll see:

```
Member Status Update Background Service is starting. Scheduled time: 21:00
Next member status update scheduled for 2026-01-05 21:00:00 (in 03:15:30)

========================================
Starting member status update cycle at 2026-01-05 21:00:00
========================================

Created status for JoãoTuno: Active, Progress: 4 meses até reforma
✅ MariaCaloira: Retired => Active (achieved 3/3 consecutive months)
⚠️ PedroVeterano: Active => Retired (6+ months without activity)
📊 AnaReformada: 1/3 => 2/3 months toward reactivation
📊 CarlosTuno: 4 months until retirement => 3 months

========================================
Member status update completed. Updated 87 members
Next update scheduled for tomorrow at 21:00
========================================
```

## Audit Log (Tracing Page)

All member status changes are now recorded in the **AuditLog** table and visible on the **owner/tracing** page. This prevents spam in console logs while maintaining a permanent record of all status transitions.

### Audit Log Fields:
- **Entity Type**: `MemberStatus`
- **Action**: `StatusChange` or `ProgressChange`
- **User Name**: `System` (automated background process)
- **Target Member Name**: Member's nickname or full name
- **Changes**: Description of the change in "old => new" format
- **Timestamp**: When the change occurred

### Example Audit Log Entries:

| Timestamp | Target Member | Action | Changes |
|-----------|--------------|---------|---------|
| 2026-01-05 17:25:00 | MariaCaloira | StatusChange | Retired => Active (achieved 3/3 consecutive months) |
| 2026-01-05 17:25:00 | PedroVeterano | StatusChange | Active => Retired (6+ months without activity) |
| 2026-01-05 17:25:00 | AnaReformada | ProgressChange | 1/3 => 2/3 months toward reactivation |
| 2026-01-05 17:25:00 | CarlosTuno | ProgressChange | 4 months until retirement => 3 months |

## State Change Log Examples

**Note:** Logs only appear when there's an actual status or progress change. If nothing changes, no spam logs are generated.

### Member becomes active (retired => active)
```
✅ JoãoTuno: Retired => Active (achieved 3/3 consecutive months)
```

### Member becomes retired (active => retired)
```
⚠️ MariaCaloira: Active => Retired (6+ months without activity)
```

### Retired member making progress toward reactivation
```
📊 PedroVeterano: 1/3 => 2/3 months toward reactivation
📊 AnaReformada: 2/3 => 3/3 months toward reactivation
```

### Active member approaching retirement
```
📊 CarlosTuno: 4 months until retirement => 3 months
📊 SofiaTuna: 3 months until retirement => 2 months
```

### New member status created
```
Created status for NovoMembro: Active, Progress: 5 meses até reforma
Created status for OutroMembro: Retired, Progress: 0/3 meses de atividade consecutiva
```

## What This Means

- **✅ Green check**: Member successfully returned to active status
- **⚠️ Warning**: Member moved to retired status due to inactivity
- **📊 Bar chart**: Progress tracking (either toward reactivation or retirement)
- **Old => New format**: Shows the clear transition from previous state to new state

## Key Features

1. **No Spam**: Only logs when actual changes occur (status or progress)
2. **Clear Format**: Uses "old => new" notation for easy tracking
3. **Visual Indicators**: Emoji icons for quick status identification in console logs
4. **Member Names**: Uses nickname when available, full name otherwise
5. **Configurable Schedule**: Runs once per day at a configurable time (default 21:00)
6. **Audit Trail**: All changes permanently recorded in AuditLog table for the tracing page

## Viewing Status Changes

### Console Logs (Server)
- Real-time monitoring during development
- Shows emoji indicators for quick visual scanning
- Includes cycle boundaries for context
- Logs when the next update is scheduled

### Audit Log / Tracing Page (UI)
- Access via `/owner/tracing` page
- Permanent record of all status changes
- Filterable by member name, date, action type
- No spam - only actual changes are recorded
- Searchable and exportable

These logs make it easy to:
1. See exactly which members changed status
2. Track progress of retired members trying to return
3. Monitor active members approaching retirement
4. Understand the complete state of your membership without noise
5. Review historical status changes via the tracing page
6. Schedule updates at the most convenient time for your organization

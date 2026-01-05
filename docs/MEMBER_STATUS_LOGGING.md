# Member Status Update Logging Examples

This document shows examples of the enhanced logging output for member status updates.

## Background Service Logs

When the background service runs (every 5 minutes for testing), you'll see:

```
========================================
Starting member status update cycle at 2026-01-05 17:20:00
========================================

Created status for JoãoTuno: Active, Progress: 4 meses até reforma
✅ MariaCaloira: Retired => Active (achieved 3/3 consecutive months)
⚠️ PedroVeterano: Active => Retired (6+ months without activity)
📊 AnaReformada: 1/3 => 2/3 months toward reactivation
📊 CarlosTuno: 4 months until retirement => 3 months

========================================
Member status update completed. Updated 87 members
Next update in 00:05:00
========================================
```

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
3. **Visual Indicators**: Emoji icons for quick status identification
4. **Member Names**: Uses nickname when available, full name otherwise
5. **Testing Interval**: Runs every 5 minutes for testing (will be 1 hour in production)

These logs make it easy to:
1. See exactly which members changed status
2. Track progress of retired members trying to return
3. Monitor active members approaching retirement
4. Understand the complete state of your membership without noise

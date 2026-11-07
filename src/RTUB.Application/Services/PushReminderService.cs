using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// The startup "Ativa as notificações" reminder (task 080, issue #261; docs/react-push-v2.md → Startup reminder).
/// The device's push state is the browser's; the server only says whether the member is Reformado
/// (<see cref="ApplicationUser.IsRetired"/>, the canonical flag) and whether that member already acknowledged the
/// once-per-account reminder. The acknowledgement is a <see cref="PendingActionDismissal"/> row of
/// <see cref="AckKind"/> (no source, no period), so it follows the account across devices under that table's unique
/// index. An active member is reminded on every app opening whatever was stored; the row only matters while Reformado.
/// </summary>
public sealed class PushReminderService(IDbContextFactory<ApplicationDbContext> contexts, TimeProvider? time = null)
{
    /// <summary>No pending-action source uses it, so it never hides or adds a row of /pending-actions.</summary>
    public const string AckKind = "startup.push";

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public sealed record ReminderState(bool Reformed, bool Acknowledged);

    /// <summary>Null when the account no longer exists.</summary>
    public async Task<ReminderState?> GetAsync(string userId)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var reformed = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => (bool?)u.IsRetired).FirstOrDefaultAsync();
        return reformed is null ? null : new ReminderState(reformed.Value, await AcknowledgedAsync(db, userId));
    }

    /// <summary>Stores the caller's acknowledgement once; a second tab or device storing it at the same time is fine.</summary>
    public async Task<bool> AcknowledgeAsync(string userId)
    {
        await using var db = await contexts.CreateDbContextAsync();
        if (!await db.Users.AnyAsync(u => u.Id == userId))
        {
            return false;
        }

        if (await AcknowledgedAsync(db, userId))
        {
            return true;
        }

        db.PendingActionDismissals.Add(new PendingActionDismissal
        {
            UserId = userId, ActionKind = AckKind, SourceKey = "", Period = "", DismissedAt = _time.GetUtcNow().UtcDateTime,
        });
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            // Only a row that really exists (another tab won the race) is success; any other write error is not.
            await using var check = await contexts.CreateDbContextAsync();
            if (await AcknowledgedAsync(check, userId))
            {
                return true;
            }

            throw;
        }
    }

    private static Task<bool> AcknowledgedAsync(ApplicationDbContext db, string userId) =>
        db.PendingActionDismissals.AnyAsync(d => d.UserId == userId && d.ActionKind == AckKind && d.SourceKey == "" && d.Period == "");
}

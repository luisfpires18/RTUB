using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Task 069: sends the Logística reminders that are due (docs/react-logistics.md → Reminders).
///
/// Time: <see cref="LogisticsCardReminder.NextReminderDate"/> is Portugal wall-clock time; it is due at
/// <see cref="PortugalTime.ToUtc"/> of it (a time skipped by the spring change fires an hour later, a repeated autumn
/// hour fires once). Daily / weekly reminders step in wall-clock days, so 10:00 stays 10:00 across DST.
///
/// One run: activate pre-069 rows (never replayed), then for each due reminder: claim it (one atomic UPDATE, so
/// overlapping runs or instances never take the same occurrence), re-check everything, send, move on.
/// - Stops (inactive): card or board gone or archived, card done, nobody left to receive it, a one-time reminder sent.
/// - Recipients, re-read now: not expelled, still see the board; a personal reminder only while its member still works
///   on the card (owner or collaborator) or manages Logística.
/// - Late: an occurrence more than <see cref="CatchUp"/> late (the app was down) is skipped, not sent.
/// - Failure: an exception while sending keeps the occurrence and retries after 2, then 4 minutes; the third failure
///   skips it.
/// Delivery is at least once per occurrence: a crash between the send and the save sends it again once the claim
/// expires. The push service accepting a push is no proof it was shown; the inbox copy is kept either way.
/// </summary>
public sealed class LogisticsReminderDelivery
{
    public static readonly TimeSpan CatchUp = TimeSpan.FromHours(6);
    public static readonly TimeSpan Claim = TimeSpan.FromMinutes(10);
    public const int MaxAttempts = 3;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IPushNotificationService _push;
    private readonly IPushNotificationFactory _factory;
    private readonly ILogger<LogisticsReminderDelivery> _logger;
    private readonly TimeProvider _time;

    public LogisticsReminderDelivery(IDbContextFactory<ApplicationDbContext> contexts, IPushNotificationService push, IPushNotificationFactory factory,
        ILogger<LogisticsReminderDelivery> logger, TimeProvider? time = null)
    {
        _contexts = contexts;
        _push = push;
        _factory = factory;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public sealed record RunResult(int Sent, int Skipped, int Stopped, int Failed);

    public async Task<RunResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        await ActivateLegacyAsync(cancellationToken);
        var now = _time.GetUtcNow().UtcDateTime;
        var nowLocal = PortugalTime.Now(_time);
        await using var db = await _contexts.CreateDbContextAsync(cancellationToken);
        // Narrowed in SQL (a day of margin for the UTC offset), exact in memory.
        var horizon = nowLocal.AddDays(1);
        var candidates = (await db.LogisticsCardReminders.AsNoTracking()
                .Where(r => r.IsActive && r.ActivatedAt != null && (r.ClaimedUntil == null || r.ClaimedUntil < now) && r.NextReminderDate <= horizon)
                .Select(r => new { r.Id, r.NextReminderDate })
                .ToListAsync(cancellationToken))
            .Where(r => PortugalTime.ToUtc(r.NextReminderDate) <= now)
            .ToList();

        int sent = 0, skipped = 0, stopped = 0, failed = 0;
        foreach (var c in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var claimed = await db.LogisticsCardReminders
                .Where(r => r.Id == c.Id && r.IsActive && r.NextReminderDate == c.NextReminderDate && (r.ClaimedUntil == null || r.ClaimedUntil < now))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ClaimedUntil, now + Claim), cancellationToken);
            if (claimed == 0)
            {
                continue; // another run has it
            }

            switch (await DeliverAsync(c.Id, now, nowLocal, cancellationToken))
            {
                case Outcome.Sent: sent++; break;
                case Outcome.Skipped: skipped++; break;
                case Outcome.Stopped: stopped++; break;
                default: failed++; break;
            }
        }

        if (candidates.Count > 0)
        {
            _logger.LogInformation("Logistics reminders: {Sent} sent, {Skipped} skipped (late), {Stopped} stopped, {Failed} failed",
                sent, skipped, stopped, failed);
        }

        return new RunResult(sent, skipped, stopped, failed);
    }

    private enum Outcome { Sent, Skipped, Stopped, Failed }

    private async Task<Outcome> DeliverAsync(int id, DateTime now, DateTime nowLocal, CancellationToken cancellationToken)
    {
        await using var db = await _contexts.CreateDbContextAsync(cancellationToken);
        var reminder = await db.LogisticsCardReminders.AsNoTracking().SingleAsync(r => r.Id == id, cancellationToken);
        var card = await db.LogisticsCards.AsNoTracking().Include(x => x.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(x => x.Id == reminder.CardId, cancellationToken);
        if (card is null || card.ArchivedAt is not null || card.List.Board.ArchivedAt is not null || card.Status == CardStatus.Done)
        {
            await StopAsync(db, id, cancellationToken);
            return Outcome.Stopped;
        }

        var recipients = await RecipientsAsync(db, reminder, card, cancellationToken);
        if (recipients.Count == 0)
        {
            await StopAsync(db, id, cancellationToken);
            return Outcome.Stopped;
        }

        if (now - PortugalTime.ToUtc(reminder.NextReminderDate) > CatchUp)
        {
            await AdvanceAsync(db, reminder, nowLocal, sentAt: null, cancellationToken);
            return Outcome.Skipped;
        }

        try
        {
            var notification = _factory.CreateLogisticsNotification(LogisticsNotice.Reminder, card.List.BoardId, card.Id, card.Title,
                card.List.Board.Name, dueDate: card.DueDate);
            await _push.SendToSelectedUsersAsync(recipients, notification);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var attempts = reminder.Attempts + 1;
            _logger.LogWarning(ex, "Logistics reminder {Id}: attempt {Attempt} of {Max} failed", id, attempts, MaxAttempts);
            if (attempts >= MaxAttempts)
            {
                await AdvanceAsync(db, reminder, nowLocal, sentAt: null, cancellationToken);
                return Outcome.Failed;
            }

            await db.LogisticsCardReminders.Where(r => r.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Attempts, attempts)
                .SetProperty(r => r.ClaimedUntil, now + TimeSpan.FromMinutes(Math.Pow(2, attempts))), cancellationToken);
            return Outcome.Failed;
        }

        await AdvanceAsync(db, reminder, nowLocal, sentAt: now, cancellationToken);
        return Outcome.Sent;
    }

    /// <summary>Who receives it now: targets that still see the board; a personal one only while its member works on the card.</summary>
    private static async Task<List<string>> RecipientsAsync(ApplicationDbContext db, LogisticsCardReminder reminder, LogisticsCard card,
        CancellationToken cancellationToken)
    {
        var targets = reminder.TargetUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        if (reminder.Personal)
        {
            targets = targets.Where(t => t == reminder.CreatedByUserId).ToList();
            var working = targets.Count > 0 && (
                (card.AssignedToUserId is { } owner && targets.Contains(owner))
                || await db.LogisticsCardAssignments.AnyAsync(a => a.CardId == card.Id && targets.Contains(a.UserId), cancellationToken)
                || (await LogisticsKanbanService.StaffIdsAsync(db, targets)).Count > 0);
            if (!working)
            {
                return new List<string>();
            }
        }

        var viewers = await LogisticsKanbanService.BoardViewersAsync(db, card.List.Board, targets);
        return targets.Where(viewers.Contains).ToList();
    }

    private static Task StopAsync(ApplicationDbContext db, int id, CancellationToken cancellationToken) =>
        db.LogisticsCardReminders.Where(r => r.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.IsActive, false)
            .SetProperty(r => r.ClaimedUntil, (DateTime?)null)
            .SetProperty(r => r.Attempts, 0), cancellationToken);

    private static Task AdvanceAsync(ApplicationDbContext db, LogisticsCardReminder reminder, DateTime nowLocal, DateTime? sentAt,
        CancellationToken cancellationToken)
    {
        reminder.Advance(nowLocal);
        var lastSent = sentAt ?? reminder.LastSentAt;
        return db.LogisticsCardReminders.Where(r => r.Id == reminder.Id).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.IsActive, reminder.IsActive)
            .SetProperty(r => r.NextReminderDate, reminder.NextReminderDate)
            .SetProperty(r => r.LastSentAt, lastSent)
            .SetProperty(r => r.Attempts, 0)
            .SetProperty(r => r.ClaimedUntil, (DateTime?)null), cancellationToken);
    }

    public sealed record ActivationReport(int Activated, int ExpiredOneTime, int MovedForward, int AlreadyInactive);

    /// <summary>
    /// Pre-069 reminders were saved but never sent. Each is taken on once (ActivatedAt set), without a flood: a one-time
    /// reminder already in the past stops; a daily / weekly one moves to its next occurrence after now; a future one
    /// stays as it is and is sent when due. Idempotent; logged as a report.
    /// </summary>
    public async Task<ActivationReport> ActivateLegacyAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contexts.CreateDbContextAsync(cancellationToken);
        var legacy = await db.LogisticsCardReminders.AsNoTracking().Where(r => r.ActivatedAt == null).ToListAsync(cancellationToken);
        if (legacy.Count == 0)
        {
            return new ActivationReport(0, 0, 0, 0);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var nowLocal = PortugalTime.Now(_time);
        int expired = 0, moved = 0, inactive = 0;
        foreach (var r in legacy)
        {
            if (!r.IsActive)
            {
                inactive++;
            }
            else if (PortugalTime.ToUtc(r.NextReminderDate) <= now)
            {
                if (r.Frequency == ReminderFrequency.OneTime)
                {
                    expired++;
                }
                else
                {
                    moved++;
                }

                r.Advance(nowLocal);
            }

            await db.LogisticsCardReminders.Where(x => x.Id == r.Id && x.ActivatedAt == null).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsActive, r.IsActive)
                .SetProperty(x => x.NextReminderDate, r.NextReminderDate)
                .SetProperty(x => x.ActivatedAt, now), cancellationToken);
        }

        var report = new ActivationReport(legacy.Count, expired, moved, inactive);
        _logger.LogInformation(
            "Logistics reminders activated: {Activated} pre-069 reminder(s); {Expired} one-time already past stopped, {Moved} recurring moved to their next occurrence, {Inactive} already inactive; none replayed",
            report.Activated, report.ExpiredOneTime, report.MovedForward, report.AlreadyInactive);
        return report;
    }
}

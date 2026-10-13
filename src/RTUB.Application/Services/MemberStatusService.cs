using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Member activity status (task 044B). Two strictly separate paths over the same batch-loaded activity and
/// <see cref="MemberActivityStatusEvaluator"/>:
/// <list type="bullet">
/// <item><b>Read</b> (<see cref="GetMemberStatusAsync"/>, <see cref="GetMemberStatusesBatchAsync"/>): calculate and
/// display only. The persisted <see cref="ApplicationUser.IsRetired"/> is shown; progress is computed in memory.
/// No database write, no audit, no notification.</item>
/// <item><b>Reconcile</b> (<see cref="ReconcileMemberStatusAsync"/>, <see cref="ReconcileAllMemberStatusesAsync"/>):
/// calculate, then in one SaveChanges per member write the flag, its <see cref="MemberStatus"/> projection and, on a
/// transition, a "MemberActivityStatus" audit entry; then notify. Callers: the background service, the rehearsal
/// attendance and event enrollment triggers, the Owner sync. Idempotent.</item>
/// </list>
/// </summary>
public class MemberStatusService : IMemberStatusService
{
    public const string AuditEntityType = "MemberActivityStatus";

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly ILogger<MemberStatusService> _logger;
    private readonly MemberStatusUpdateOptions _options;
    private readonly TimeProvider _time;

    public MemberStatusService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        ILogger<MemberStatusService> logger,
        IOptions<MemberStatusUpdateOptions> options,
        TimeProvider time)
    {
        _contextFactory = contextFactory;
        _pushNotificationService = pushNotificationService;
        _pushNotificationFactory = pushNotificationFactory;
        _logger = logger;
        _options = options.Value;
        _time = time;
    }

    // ---------- Read: calculate and display, never write ----------

    public async Task<MemberStatusResult> GetMemberStatusAsync(string userId)
    {
        RequireId(userId);
        return (await ReadAsync(new[] { userId })).GetValueOrDefault(userId) ?? throw new InvalidOperationException($"User '{userId}' not found");
    }

    public async Task<Dictionary<string, MemberStatusResult?>> GetMemberStatusesBatchAsync(IEnumerable<string> userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var results = await ReadAsync(ids);
        return ids.ToDictionary(id => id, id => (MemberStatusResult?)results.GetValueOrDefault(id), StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, MemberStatusResult>> ReadAsync(IReadOnlyCollection<string> ids)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        await using var db = await _contextFactory.CreateDbContextAsync();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync();
        var activity = await LoadActivityAsync(db, ids, now);

        return users.ToDictionary(u => u.Id, u =>
        {
            var userActivity = activity.GetValueOrDefault(u.Id) ?? Activity.None;
            // The persisted flag is what the member is until a reconciliation changes it.
            return ToResult(u.IsRetired, Evaluate(u, userActivity, now), userActivity, now);
        }, StringComparer.Ordinal);
    }

    // ---------- Reconcile: calculate + persist transition + audit + notify ----------

    public async Task<MemberStatusResult> ReconcileMemberStatusAsync(string userId)
    {
        RequireId(userId);
        var results = (await ReconcileAsync(new[] { userId })).Results;
        if (!results.TryGetValue(userId, out var result))
        {
            throw new InvalidOperationException($"User '{userId}' not found");
        }

        return result ?? throw new InvalidOperationException($"Could not evaluate the status of member '{userId}'");
    }

    public async Task<MemberStatusReconciliationResult> ReconcileAllMemberStatusesAsync()
    {
        if (!_options.Enabled)
        {
            return MemberStatusReconciliationResult.Empty;
        }

        return (await ReconcileAsync(null)).Counts;
    }

    private static void RequireId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }
    }

    private static MemberActivityEvaluation? Evaluate(ApplicationUser user, Activity activity, DateTime now) =>
        MemberActivityStatusEvaluator.IsSubjectToRule(user)
            ? MemberActivityStatusEvaluator.Evaluate(user.IsRetired, activity.Dates, MemberActivityStatusEvaluator.ActiveSince(user), now)
            : null;

    /// <summary>Null <paramref name="ids"/> = every user. Failed members map to null and are counted, never stop the run.</summary>
    private async Task<(Dictionary<string, MemberStatusResult?> Results, MemberStatusReconciliationResult Counts)> ReconcileAsync(
        IReadOnlyCollection<string>? ids)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        List<string> userIds;
        Dictionary<string, Activity> activity;
        await using (var db = await _contextFactory.CreateDbContextAsync())
        {
            userIds = await db.Users.AsNoTracking()
                .Where(u => ids == null || ids.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync();
            activity = await LoadActivityAsync(db, ids, now);
        }

        var results = new Dictionary<string, MemberStatusResult?>(StringComparer.Ordinal);
        int evaluated = 0, activated = 0, reformed = 0, unchanged = 0, excluded = 0, failed = 0;
        foreach (var userId in userIds)
        {
            try
            {
                var (result, outcome) = await ReconcileMemberAsync(userId, activity.GetValueOrDefault(userId) ?? Activity.None, now);
                results[userId] = result;
                switch (outcome)
                {
                    case Outcome.Excluded: excluded++; break;
                    case Outcome.Activated: evaluated++; activated++; break;
                    case Outcome.Reformed: evaluated++; reformed++; break;
                    default: evaluated++; unchanged++; break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Member status reconciliation failed for member {UserId}", userId);
                results[userId] = null;
                failed++;
            }
        }

        return (results, new MemberStatusReconciliationResult(evaluated, activated, reformed, unchanged, excluded, failed));
    }

    private async Task<(MemberStatusResult Result, Outcome Outcome)> ReconcileMemberAsync(string userId, Activity activity, DateTime now)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException($"User '{userId}' not found");
        var projection = await db.MemberStatuses.FirstOrDefaultAsync(s => s.UserId == userId);

        // Members outside the rule (only Tuno Honorário, or no category) keep their flag; only their projection is refreshed.
        var evaluation = Evaluate(user, activity, now);
        var result = ToResult(evaluation?.IsRetired ?? user.IsRetired, evaluation, activity, now);
        var previousProgressMonths = projection?.IsRetired == false ? projection.ProgressMonths : null;

        if (evaluation is { Changed: true })
        {
            db.AuditLogs.Add(TransitionAudit(user, evaluation, now));
            user.IsRetired = evaluation.IsRetired;
        }

        if (projection is null)
        {
            projection = new MemberStatus { UserId = userId, CreatedAt = now };
            db.MemberStatuses.Add(projection);
        }

        if (Project(projection, result, now) || evaluation is { Changed: true })
        {
            await db.SaveChangesAsync();
        }

        if (evaluation is { Changed: true })
        {
            _logger.LogInformation("Member {UserName} is now {State} ({Reason})",
                user.UserName, evaluation.IsRetired ? "Reformado" : "active", evaluation.Reason);
        }

        await NotifyAsync(user, evaluation, result, previousProgressMonths);

        return (result, evaluation switch
        {
            null => Outcome.Excluded,
            { Changed: false } => Outcome.Unchanged,
            { IsRetired: true } => Outcome.Reformed,
            _ => Outcome.Activated,
        });
    }

    /// <summary>One query per source for the members asked for (all users when <paramref name="ids"/> is null).</summary>
    private static async Task<Dictionary<string, Activity>> LoadActivityAsync(ApplicationDbContext db, IReadOnlyCollection<string>? ids, DateTime now)
    {
        var rehearsals = await RehearsalActivityAsync(db, ids, now);
        var performances = await PerformanceActivityAsync(db, ids, now);

        return rehearsals.Select(r => r.UserId).Concat(performances.Select(e => e.UserId)).Distinct()
            .ToDictionary(id => id, id => new Activity(
                rehearsals.Where(r => r.UserId == id).Select(r => r.Date).ToList(),
                performances.Where(e => e.UserId == id).Select(e => e.Date).ToList()));
    }

    /// <summary>Ensaio: a presença confirmed by Admin (<c>Attended</c>) at a past, not cancelled rehearsal.</summary>
    private static async Task<List<ActivityRow>> RehearsalActivityAsync(ApplicationDbContext db, IReadOnlyCollection<string>? ids, DateTime now) =>
        await db.RehearsalAttendances.AsNoTracking()
            .Where(a => a.Attended && (ids == null || ids.Contains(a.UserId)))
            .Join(db.Rehearsals.AsNoTracking(), a => a.RehearsalId, r => r.Id, (a, r) => new { a.UserId, r.Date, r.IsCanceled })
            .Where(x => !x.IsCanceled && x.Date < now)
            .Select(x => new ActivityRow(x.UserId, x.Date))
            .ToListAsync();

    /// <summary>
    /// Atuação: an ended (<c>EndDate ?? Date</c> before now), not cancelled event with a <c>WillAttend</c> enrollment,
    /// dated by its start. <b>Temporary source:</b> <c>WillAttend</c> is the member's answer ("vai"), reused as post-event
    /// participation because RTUB has no performance-attendance record (after the event the app shows it as "Quem foi"
    /// and Admin corrects it by removing / adding answers). When a real attendance field or table exists, replace only
    /// this query.
    /// </summary>
    private static async Task<List<ActivityRow>> PerformanceActivityAsync(ApplicationDbContext db, IReadOnlyCollection<string>? ids, DateTime now) =>
        await db.Enrollments.AsNoTracking()
            .Where(e => e.WillAttend && (ids == null || ids.Contains(e.UserId)))
            .Join(db.Events.AsNoTracking(), e => e.EventId, ev => ev.Id,
                (e, ev) => new { e.UserId, Start = ev.Date, End = ev.EndDate ?? ev.Date, ev.IsCancelled })
            .Where(x => !x.IsCancelled && x.End < now)
            .Select(x => new ActivityRow(x.UserId, x.Start))
            .ToListAsync();

    private static MemberStatusResult ToResult(bool isRetired, MemberActivityEvaluation? evaluation, Activity activity, DateTime now)
    {
        var dates = activity.Dates.ToList();
        var result = new MemberStatusResult
        {
            IsRetired = isRetired,
            LastRehearsalDate = activity.Rehearsals.Count == 0 ? null : activity.Rehearsals.Max(),
            LastEventDate = activity.Events.Count == 0 ? null : activity.Events.Max(),
            LastActivityDate = dates.Count == 0 ? null : dates.Max(),
            HasAnyActivity = dates.Count > 0,
            TotalActivitiesCount = dates.Count,
            HasActivityInCurrentMonth = dates.Any(d => d.Year == now.Year && d.Month == now.Month),
        };

        // No progress for members outside the rule or with nothing to show yet (the UI hides the section then).
        if (evaluation is null || !result.HasAnyActivity)
        {
            return result;
        }

        if (isRetired)
        {
            var months = Math.Min(evaluation.ConsecutiveActiveMonths, MemberActivityStatusEvaluator.ReturnMonths);
            result.ProgressMonths = months;
            result.ProgressTotalMonths = MemberActivityStatusEvaluator.ReturnMonths;
            result.ProgressDescription = $"{months}/{MemberActivityStatusEvaluator.ReturnMonths} meses de atividade consecutiva";
        }
        else
        {
            // On a read the persisted flag can still say active past the threshold: 0 until the next reconciliation.
            var months = evaluation.MonthsUntilRetirement ?? 0;
            result.ProgressMonths = months;
            result.ProgressTotalMonths = MemberActivityStatusEvaluator.InactivityMonths;
            result.ProgressDescription = months == 0 ? "Próximo da reforma" : $"{months} meses até reforma";
        }

        return result;
    }

    /// <summary>Copies the result onto the projection; true when anything changed (and only then is it saved).</summary>
    private static bool Project(MemberStatus entity, MemberStatusResult result, DateTime now)
    {
        var same = entity.Id != 0
            && entity.IsRetired == result.IsRetired
            && entity.LastRehearsalDate == result.LastRehearsalDate
            && entity.LastEventDate == result.LastEventDate
            && entity.LastActivityDate == result.LastActivityDate
            && entity.HasAnyActivity == result.HasAnyActivity
            && entity.ProgressMonths == result.ProgressMonths
            && entity.ProgressTotalMonths == result.ProgressTotalMonths
            && entity.ProgressDescription == result.ProgressDescription
            && entity.TotalActivitiesCount == result.TotalActivitiesCount;
        if (same)
        {
            return false;
        }

        entity.IsRetired = result.IsRetired;
        entity.LastRehearsalDate = result.LastRehearsalDate;
        entity.LastEventDate = result.LastEventDate;
        entity.LastActivityDate = result.LastActivityDate;
        entity.HasAnyActivity = result.HasAnyActivity;
        entity.ProgressMonths = result.ProgressMonths;
        entity.ProgressTotalMonths = result.ProgressTotalMonths;
        entity.ProgressDescription = result.ProgressDescription;
        entity.TotalActivitiesCount = result.TotalActivitiesCount;
        entity.LastUpdatedAt = now;
        return true;
    }

    private static AuditLog TransitionAudit(ApplicationUser user, MemberActivityEvaluation evaluation, DateTime now) => new()
    {
        EntityType = AuditEntityType,
        Action = evaluation.IsRetired ? "Reformed" : "Activated",
        UserId = user.Id,
        UserName = user.UserName,
        EntityDisplayName = user.Nickname ?? user.UserName,
        Timestamp = now,
        Changes = JsonSerializer.Serialize(new
        {
            From = evaluation.IsRetired ? "Active" : "Reformado",
            To = evaluation.IsRetired ? "Reformado" : "Active",
            Reason = evaluation.Reason.ToString(),
            LastActivity = evaluation.LastActivity?.ToString("yyyy-MM-dd"),
            Threshold = evaluation.IsRetired ? evaluation.RetirementThreshold?.ToString("yyyy-MM-dd") : null,
            Months = evaluation.IsRetired ? null : evaluation.StreakMonths.Select(m => m.ToString("yyyy-MM")).ToList(),
        }),
    };

    private async Task NotifyAsync(ApplicationUser user, MemberActivityEvaluation? evaluation, MemberStatusResult result, int? previousProgressMonths)
    {
        if (!_options.PushNotificationsEnabled || evaluation is null)
        {
            return;
        }

        try
        {
            var nickname = user.Nickname ?? user.UserName ?? "Membro";
            if (evaluation is { Changed: true, IsRetired: false })
            {
                await _pushNotificationService.BroadcastAsync(_pushNotificationFactory.CreateMemberReactivatedNotification(nickname, "/"));
            }

            // The warning once, when an active member enters the last month - not on every run while in it.
            if (!result.IsRetired && result.ProgressMonths == 1 && previousProgressMonths != 1)
            {
                await _pushNotificationService.SendToUserAsync(user.Id, _pushNotificationFactory.CreateMemberRetirementWarningNotification(nickname, "/"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Member status notification failed for {UserName}", user.UserName);
        }
    }

    private enum Outcome { Excluded, Unchanged, Activated, Reformed }

    private sealed record ActivityRow(string UserId, DateTime Date);

    private sealed record Activity(List<DateTime> Rehearsals, List<DateTime> Events)
    {
        public static readonly Activity None = new(new List<DateTime>(), new List<DateTime>());

        public IEnumerable<DateTime> Dates => Rehearsals.Concat(Events);
    }
}

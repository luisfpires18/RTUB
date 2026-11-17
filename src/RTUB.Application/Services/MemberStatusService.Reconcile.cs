// Member status: reconcile one or all members, evaluate, project, audit.
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

public partial class MemberStatusService
{
    // ---------- Reconcile: calculate + persist transition + audit + notify ----------

    public async Task<MemberStatusResult> ReconcileMemberStatusAsync(string userId)
    {
        RequireId(userId);
        var results = (await ReconcileAsync(new[] { userId }, notify: false)).Results;
        if (!results.TryGetValue(userId, out var result))
        {
            throw new InvalidOperationException($"User '{userId}' not found");
        }

        return result ?? throw new InvalidOperationException($"Could not evaluate the status of member '{userId}'");
    }

    public async Task<MemberStatusReconciliationResult> ReconcileAllMemberStatusesAsync(bool notify = false)
    {
        if (!_options.Enabled)
        {
            return MemberStatusReconciliationResult.Empty;
        }

        var counts = (await ReconcileAsync(null, notify)).Counts;

        // The premade /messages groups follow the members (NO ATIVO follows the flag just reconciled) - task 058.
        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync();
            await MessagingGroups.SyncAsync(db, _time.GetUtcNow().UtcDateTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Syncing the premade message groups failed");
        }

        return counts;
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
        IReadOnlyCollection<string>? ids, bool notify)
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
                var (result, outcome) = await ReconcileMemberAsync(userId, activity.GetValueOrDefault(userId) ?? Activity.None, now, notify);
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

    private async Task<(MemberStatusResult Result, Outcome Outcome)> ReconcileMemberAsync(string userId, Activity activity, DateTime now, bool notify)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException($"User '{userId}' not found");
        var projection = await db.MemberStatuses.FirstOrDefaultAsync(s => s.UserId == userId);

        // Members outside the rule (only Tuno Honorário, or no category) keep their flag; only their projection is refreshed.
        var evaluation = Evaluate(user, activity, now);
        var result = ToResult(evaluation?.IsRetired ?? user.IsRetired, evaluation, activity, now);
        var previousProgressMonths = projection?.IsRetired == false ? projection.ProgressMonths : null;
        var firstEvaluation = projection is null;

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

        if (notify && !firstEvaluation)
        {
            await NotifyAsync(user, evaluation, result, previousProgressMonths, now);
        }

        return (result, evaluation switch
        {
            null => Outcome.Excluded,
            { Changed: false } => Outcome.Unchanged,
            { IsRetired: true } => Outcome.Reformed,
            _ => Outcome.Activated,
        });
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
}

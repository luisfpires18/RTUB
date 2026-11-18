// Member status: rehearsal activity loading and notifications.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public partial class MemberStatusService
{
    /// <summary>One query per source for the members asked for (all users when <paramref name="ids"/> is null).</summary>
    private static async Task<Dictionary<string, Activity>> LoadActivityAsync(ApplicationDbContext db, IReadOnlyCollection<string>? ids, DateTime now)
    {
        var rehearsals = await RehearsalActivityAsync(db, ids, now);
        var performances = await PerformanceActivityAsync(db, ids, now);

        // Grouped once: the Where-per-member version walked every row again for each member.
        var byRehearsal = rehearsals.ToLookup(r => r.UserId, r => r.Date);
        var byPerformance = performances.ToLookup(e => e.UserId, e => e.Date);
        return byRehearsal.Select(g => g.Key).Concat(byPerformance.Select(g => g.Key)).Distinct()
            .ToDictionary(id => id, id => new Activity(byRehearsal[id].ToList(), byPerformance[id].ToList()));
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
            result.ProgressDescription = months switch
            {
                0 => "Próximo da reforma",
                1 => "1 mês até reforma",
                _ => $"{months} meses até reforma",
            };
        }

        return result;
    }

    private async Task NotifyAsync(ApplicationUser user, MemberActivityEvaluation? evaluation, MemberStatusResult result, int? previousProgressMonths, DateTime now)
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
                await _pushNotificationService.BroadcastAsync(_pushNotificationFactory.CreateMemberReactivatedNotification(nickname));
            }

            // Only to the member about to lose the active status, once, when they enter the last month - not on every
            // run while in it. A stored 0 is a legacy final partial month (now 1), already warned when it
            // passed 1: no second warning. A trigger (an answer to an event) can store the 1 first on the day it starts;
            // a countdown that was still 2+ a day ago is that day, so the daily run warns all the same.
            if (!result.IsRetired && result.ProgressMonths == 1
                && (previousProgressMonths is not (1 or 0) || EnteredLastMonthSince(evaluation, now.AddDays(-1))))
            {
                await _pushNotificationService.SendToUserAsync(user.Id, _pushNotificationFactory.CreateMemberRetirementWarningNotification(nickname));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Member status notification failed for {UserName}", user.UserName);
        }
    }

    /// <summary>Whether the countdown still showed 2 or more months at <paramref name="since"/> (MonthsUntil &gt;= 2).</summary>
    private static bool EnteredLastMonthSince(MemberActivityEvaluation evaluation, DateTime since) =>
        evaluation.RetirementThreshold is { } threshold && since.AddMonths(2) <= threshold;

    private enum Outcome { Excluded, Unchanged, Activated, Reformed }

    private sealed record ActivityRow(string UserId, DateTime Date);

    private sealed record Activity(List<DateTime> Rehearsals, List<DateTime> Events)
    {
        public static readonly Activity None = new(new List<DateTime>(), new List<DateTime>());

        public IEnumerable<DateTime> Dates => Rehearsals.Concat(Events);
    }
}

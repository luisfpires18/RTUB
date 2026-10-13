using RTUB.Application.Extensions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>Why an evaluation changed a member's <see cref="ApplicationUser.IsRetired"/> flag.</summary>
public enum MemberStatusTransition
{
    None,
    /// <summary>Active, and the six-month threshold after the last activity (or the active-since date) has passed.</summary>
    SixMonthsWithoutActivity,
    /// <summary>Active, with no qualifying activity and no admission date at all to start the six months from.</summary>
    NoQualifyingActivity,
    /// <summary>Reformado, with a qualifying activity in each of three consecutive calendar months up to now.</summary>
    ThreeConsecutiveActiveMonths,
}

/// <summary>The outcome of <see cref="MemberActivityStatusEvaluator.Evaluate"/>: the state and everything the projection and UI show.</summary>
/// <param name="RetirementThreshold">Active: the moment the member becomes Reformado without new activity; just reformed: the threshold that passed. Null for a member who stays Reformado.</param>
/// <param name="MonthsUntilRetirement">Active: whole months left before the threshold. Null for a Reformado.</param>
/// <param name="StreakMonths">The current run of calendar months with activity, oldest first (first day of each month).</param>
public sealed record MemberActivityEvaluation(
    bool IsRetired,
    bool Changed,
    MemberStatusTransition Reason,
    DateTime? LastActivity,
    DateTime? RetirementThreshold,
    int? MonthsUntilRetirement,
    IReadOnlyList<DateTime> StreakMonths)
{
    public int ConsecutiveActiveMonths => StreakMonths.Count;
}

/// <summary>
/// The one place the member activity rules live (RGI: Reformados are effective members away from activity for six
/// months or more; they come back after three months of uninterrupted activity). Pure: every input, the clock included,
/// is a parameter. <see cref="MemberStatusService"/> loads the data, persists the result and audits/notifies.
/// <list type="bullet">
/// <item>Active → Reformado when <c>now &gt;= max(lastActivity, activeSince).AddMonths(6)</c>: six full calendar
/// months (DateTime.AddMonths, so 31 Aug + 6 = 28/29 Feb) with nothing newer. No last activity and no active-since
/// date → Reformado at once (an active flag with no evidence never stays active forever).</item>
/// <item>Reformado → active when the months with activity, counted back from the current month (or the previous one
/// while the current month has none yet), form a run of three. Several activities in one month are one month.</item>
/// </list>
/// Pre-reformation activity cannot join a new streak: a member is reformed only after six months without any
/// activity, so the run counted back from now always stops in that gap.
/// </summary>
public static class MemberActivityStatusEvaluator
{
    public const int InactivityMonths = 6;
    public const int ReturnMonths = 3;

    /// <summary>
    /// The flag a new account starts with: a Leitão is someone genuinely joining the Tuna and starts active; any other
    /// category is an existing or former member being digitised and starts Reformado until the activity rule says otherwise.
    /// </summary>
    public static bool InitialIsRetired(IEnumerable<MemberCategory> categories) => !categories.Contains(MemberCategory.Leitao);

    /// <summary>
    /// The state of a member account being created at <paramref name="now"/>: <see cref="InitialIsRetired"/>, and a Leitão
    /// with no Leitão date gets the creation month, so <see cref="ActiveSince"/> always has a start for the six months.
    /// </summary>
    public static void ApplyInitialState(ApplicationUser member, DateTime now)
    {
        member.IsRetired = InitialIsRetired(member.Categories);
        if (member.Categories.Contains(MemberCategory.Leitao) && member.YearLeitao is null)
        {
            (member.YearLeitao, member.MonthLeitao) = (now.Year, now.Month);
        }
    }

    /// <summary>
    /// Anyone with a membership category: Leitão, Caloiro, Tuno, Veterano or Tunossauro (Fundadores are Tunos). The
    /// Tuno Honorário distinction never takes precedence over them (it does not invalidate membership in another
    /// category); only someone who is <i>just</i> Tuno Honorário, or has no category at all, is out of scope.
    /// </summary>
    public static bool IsSubjectToRule(ApplicationUser user) => user.IsLeitao() || user.IsEffectiveMember();

    /// <summary>
    /// Where the six months of a member with no (newer) qualifying activity run from: the latest admission milestone
    /// (Leitão, Caloiro or Tuno year/month, first day of the month; a missing month is January). Every Leitão created in
    /// the app has one (<see cref="MemberAdminService"/> fills the creation month when the form leaves it empty), so a new
    /// Leitão with an empty attendance table is reformed only six months after that start. ApplicationUser has no creation timestamp.
    /// </summary>
    public static DateTime? ActiveSince(ApplicationUser user) =>
        new[] { Milestone(user.YearLeitao, user.MonthLeitao), Milestone(user.YearCaloiro, user.MonthCaloiro), Milestone(user.YearTuno, user.MonthTuno) }
            .Max();

    public static MemberActivityEvaluation Evaluate(bool currentIsRetired, IEnumerable<DateTime> activityDates, DateTime? activeSince, DateTime now)
    {
        // Callers pass past activity only; a future date here is ignored rather than trusted.
        var dates = activityDates.Where(d => d <= now).ToList();
        DateTime? last = dates.Count == 0 ? null : dates.Max();
        var streak = Streak(dates.Select(MonthOf).ToHashSet(), now);

        var threshold = Threshold(last, activeSince);
        var isRetired = currentIsRetired;
        var reason = MemberStatusTransition.None;
        if (!currentIsRetired)
        {
            if (threshold is null || now >= threshold)
            {
                isRetired = true;
                reason = threshold is null ? MemberStatusTransition.NoQualifyingActivity : MemberStatusTransition.SixMonthsWithoutActivity;
            }
        }
        else if (streak.Count >= ReturnMonths)
        {
            isRetired = false;
            reason = MemberStatusTransition.ThreeConsecutiveActiveMonths;
        }

        var changed = isRetired != currentIsRetired;
        return new MemberActivityEvaluation(isRetired, changed, reason, last,
            isRetired && !changed ? null : threshold,
            isRetired ? null : WholeMonthsUntil(now, threshold!.Value),
            streak);
    }

    private static DateTime? Threshold(DateTime? last, DateTime? activeSince) =>
        (activeSince is null || last > activeSince ? last : activeSince)?.AddMonths(InactivityMonths);

    private static List<DateTime> Streak(HashSet<DateTime> months, DateTime now)
    {
        var month = MonthOf(now);
        if (!months.Contains(month))
        {
            // A month that has only just started is not a gap yet.
            month = month.AddMonths(-1);
        }

        var streak = new List<DateTime>();
        while (months.Contains(month))
        {
            streak.Insert(0, month);
            month = month.AddMonths(-1);
        }

        return streak;
    }

    private static int WholeMonthsUntil(DateTime now, DateTime threshold)
    {
        var months = 0;
        while (months < InactivityMonths && now.AddMonths(months + 1) <= threshold)
        {
            months++;
        }

        return months;
    }

    private static DateTime MonthOf(DateTime d) => new(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime? Milestone(int? year, int? month) =>
        year is >= 1 and <= 9999 ? new DateTime(year.Value, month is >= 1 and <= 12 ? month.Value : 1, 1, 0, 0, 0, DateTimeKind.Utc) : null;
}

using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /hall-of-fame calculations behind the React /hall-of-fame (task 032, docs/react-member-area.md). Same
/// twelve records, same data, same rules:
/// - every account counts, as before (no category or expelled filter);
/// - ties: every member who shares the best value wins;
/// - durations (Leitão → Caloiro, Caloiro → Tuno) are whole months between the two month/year pairs, and only a
///   positive duration counts;
/// - "Mais ensaios": attended, past, not-cancelled rehearsals; "Mais atuações": "vou" on past, not-cancelled events
///   (the leaderboard's XP rules);
/// - the counts (cargos, mandatos de Magister, afilhados, instrumentos) need at least one.
/// Signed-in members only, as the [Authorize] page was. Read-only; no schema change.
/// </summary>
public sealed class HallOfFameService : IHallOfFameService
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-PT");

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IRoleAssignmentService _assignments;
    private readonly IEnrollmentService _enrollments;
    private readonly IRehearsalAttendanceRepository _attendance;
    private readonly IMemberInstrumentService _instruments;

    public HallOfFameService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IRoleAssignmentService assignments,
        IEnrollmentService enrollments,
        IRehearsalAttendanceRepository attendance,
        IMemberInstrumentService instruments)
    {
        _contexts = contexts;
        _assignments = assignments;
        _enrollments = enrollments;
        _attendance = attendance;
        _instruments = instruments;
    }

    public async Task<EventResult<HallOfFameDto>> GetAsync(ClaimsPrincipal user)
    {
        if (!MembersAuthorization.IsMember(user))
        {
            return EventResult<HallOfFameDto>.Fail(EventResultStatus.SignInRequired);
        }

        List<ApplicationUser> users;
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            users = await db.Users.AsNoTracking().ToListAsync();
        }

        var assignments = (await _assignments.GetAllRoleAssignmentsAsync()).ToList();
        var now = DateTime.UtcNow;

        var categories = new List<HallOfFameCategoryDto>
        {
            Latest("ultimo-caloiro", "Última passagem a Caloiro", users, u => MonthOf(u.YearCaloiro, u.MonthCaloiro)),
            Latest("ultimo-tuno", "Última passagem a Tuno", users, u => MonthOf(u.YearTuno, u.MonthTuno)),
            Most("mais-afilhados", "Mais afilhados", users, MentorCounts(users), "afilhado", "afilhados"),
            Most("mais-cargos", "Mais cargos", users,
                users.ToDictionary(u => u.Id, u => assignments.Count(a => a.UserId == u.Id)), "cargo", "cargos"),
            Most("mais-magister", "Mais vezes Magister", users,
                users.ToDictionary(u => u.Id, u => assignments.Count(a => a.UserId == u.Id && a.Position == Position.Magister)),
                "mandato", "mandatos"),
            Most("mais-instrumentos", "Mais instrumentos a tocar", users, await InstrumentCountsAsync(users), "instrumento", "instrumentos"),
        };

        var leitao = Durations(users, u => MonthOf(u.YearLeitao, u.MonthLeitao), u => MonthOf(u.YearCaloiro, u.MonthCaloiro));
        var caloiro = Durations(users, u => MonthOf(u.YearCaloiro, u.MonthCaloiro), u => MonthOf(u.YearTuno, u.MonthTuno));
        categories.Add(Duration("mais-tempo-leitao", "Mais tempo a Leitão", leitao, longest: true));
        categories.Add(Duration("menos-tempo-leitao", "Menos tempo a Leitão", leitao, longest: false));
        categories.Add(Duration("mais-tempo-caloiro", "Mais tempo a Caloiro", caloiro, longest: true));
        categories.Add(Duration("menos-tempo-caloiro", "Menos tempo a Caloiro", caloiro, longest: false));
        categories.Add(Most("mais-ensaios", "Mais ensaios", users, await RehearsalCountsAsync(now), "ensaio", "ensaios"));
        categories.Add(Most("mais-atuacoes", "Mais atuações", users, await PerformanceCountsAsync(now), "atuação", "atuações"));

        return EventResult<HallOfFameDto>.Ok(new HallOfFameDto(categories));
    }

    // ---------- the records ----------

    /// <summary>The most recent month (the old "Última passagem"); the month is the label.</summary>
    private static HallOfFameCategoryDto Latest(string key, string title, IEnumerable<ApplicationUser> users, Func<ApplicationUser, DateTime?> month)
    {
        var dated = users.Select(u => (User: u, Date: month(u))).Where(x => x.Date.HasValue).ToList();
        if (dated.Count == 0)
        {
            return Empty(key, title);
        }

        var latest = dated.Max(x => x.Date!.Value);
        return new HallOfFameCategoryDto(key, title,
            dated.Where(x => x.Date == latest).Select(x => Person(x.User)).ToList(),
            latest.ToString("MMMM yyyy", Portuguese));
    }

    /// <summary>The highest count, at least one; <paramref name="counts"/> is by user id.</summary>
    private static HallOfFameCategoryDto Most(string key, string title, IEnumerable<ApplicationUser> users,
        IReadOnlyDictionary<string, int> counts, string one, string many)
    {
        var counted = users.Select(u => (User: u, Count: counts.GetValueOrDefault(u.Id))).Where(x => x.Count > 0).ToList();
        if (counted.Count == 0)
        {
            return Empty(key, title);
        }

        var best = counted.Max(x => x.Count);
        return new HallOfFameCategoryDto(key, title,
            counted.Where(x => x.Count == best).Select(x => Person(x.User)).ToList(),
            $"{best} {(best == 1 ? one : many)}");
    }

    private static HallOfFameCategoryDto Duration(string key, string title, IReadOnlyList<(ApplicationUser User, int Months)> durations, bool longest)
    {
        if (durations.Count == 0)
        {
            return Empty(key, title);
        }

        var target = longest ? durations.Max(d => d.Months) : durations.Min(d => d.Months);
        return new HallOfFameCategoryDto(key, title,
            durations.Where(d => d.Months == target).Select(d => Person(d.User)).ToList(),
            FormatDuration(target));
    }

    private static HallOfFameCategoryDto Empty(string key, string title) => new(key, title, Array.Empty<GovernanceMemberDto>(), null);

    // ---------- the data ----------

    /// <summary>Whole months from one month/year pair to the next; only positive durations count.</summary>
    private static List<(ApplicationUser User, int Months)> Durations(IEnumerable<ApplicationUser> users,
        Func<ApplicationUser, DateTime?> from, Func<ApplicationUser, DateTime?> to)
    {
        var durations = new List<(ApplicationUser User, int Months)>();
        foreach (var user in users)
        {
            if (from(user) is { } start && to(user) is { } end
                && (end.Year - start.Year) * 12 + (end.Month - start.Month) is var months and > 0)
            {
                durations.Add((user, months));
            }
        }

        return durations;
    }

    /// <summary>How many members name each member as padrinho.</summary>
    private static Dictionary<string, int> MentorCounts(IEnumerable<ApplicationUser> users) =>
        users
            .Where(u => !string.IsNullOrEmpty(u.MentorId))
            .GroupBy(u => u.MentorId!)
            .ToDictionary(g => g.Key, g => g.Count());

    private async Task<Dictionary<string, int>> InstrumentCountsAsync(IReadOnlyCollection<ApplicationUser> users)
    {
        var byUser = await _instruments.GetMemberInstrumentsByUserIdsAsync(users.Select(u => u.Id).ToList());
        return byUser.ToDictionary(p => p.Key, p => p.Value.Count);
    }

    /// <summary>Attended rehearsals that already happened and were not cancelled.</summary>
    private async Task<Dictionary<string, int>> RehearsalCountsAsync(DateTime now) =>
        (await _attendance.QueryAsync(q => q
            .Include(ra => ra.Rehearsal)
            .Where(ra => ra.Rehearsal != null && ra.Rehearsal.Date < now && !ra.Rehearsal.IsCanceled && ra.Attended)
            .GroupBy(ra => ra.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync()))
        .ToDictionary(x => x.UserId, x => x.Count);

    /// <summary>"Vou" on events that already ended and were not cancelled.</summary>
    private async Task<Dictionary<string, int>> PerformanceCountsAsync(DateTime now) =>
        (await _enrollments.GetAllEnrollmentsAsync())
            .Where(e => e.WillAttend && e.Event != null && (e.Event.EndDate ?? e.Event.Date) < now && !e.Event.IsCancelled)
            .GroupBy(e => e.UserId)
            .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>A month/year pair as the first of that month, when both halves are valid.</summary>
    private static DateTime? MonthOf(int? year, int? month) =>
        year is >= 1 and <= 9999 && month is >= 1 and <= 12 ? new DateTime(year.Value, month.Value, 1) : null;

    private static GovernanceMemberDto Person(ApplicationUser u) => GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl);

    /// <summary>"5 meses", "2 anos", "1 ano 3 meses", as the old page.</summary>
    internal static string FormatDuration(int months)
    {
        if (months < 12)
        {
            return $"{months} {(months == 1 ? "mês" : "meses")}";
        }

        var years = months / 12;
        var rest = months % 12;
        var yearText = $"{years} {(years == 1 ? "ano" : "anos")}";
        return rest == 0 ? yearText : $"{yearText} {rest} {(rest == 1 ? "mês" : "meses")}";
    }
}

// Member directory: cards, badges, progress and timeline helpers.
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class MemberDirectoryService
{
    // ---------- helpers ----------

    private async Task<List<ApplicationUser>> UsersAsync()
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking().ToListAsync();
    }

    /// <summary>"" when empty, the value when allowed, null when it is anything else.</summary>
    private static string? Pick(string? value, IEnumerable<string> allowed) =>
        string.IsNullOrEmpty(value) ? "" : allowed.Contains(value) ? value : null;

    private async Task<Dictionary<string, MemberStatusResult?>> LeitaoStatusesAsync(List<string> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, MemberStatusResult?>();
        }

        // A read: computed for display, never reconciled here.
        return await _status.GetMemberStatusesBatchAsync(ids);
    }

    private static MemberCardDto Card(ApplicationUser u, IEnumerable<MemberBadgeDto> badges, string? position, bool noRoles,
        Dictionary<string, List<MemberInstrument>> instruments, bool inactive, bool expelled)
    {
        var who = GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl);
        return new MemberCardDto(u.Id, who.DisplayName, who.FullName, who.AvatarUrl, badges.ToList(), position, noRoles,
            PrimaryInstrument(u.Id, instruments),
            u.LastLoginDate is { } seen && DateTime.UtcNow - seen < TimeSpan.FromHours(1),
            inactive, expelled);
    }

    /// <summary>The card shows Caloiro, Tuno and Tuno Honorário only (Veterano and the rest live in the details).</summary>
    private static IEnumerable<MemberBadgeDto> RegularBadges(ApplicationUser u) =>
        StatusHelper.GetDisplayCategories(u)
            .Where(c => c is MemberCategory.Caloiro or MemberCategory.Tuno or MemberCategory.TunoHonorario)
            .Select(Badge);

    private static MemberBadgeDto Badge(MemberCategory c) => new(StatusHelper.GetCategoryDisplay(c), c.ToString().ToLowerInvariant());

    private static string? PrimaryInstrument(string userId, Dictionary<string, List<MemberInstrument>> instruments) =>
        instruments.GetValueOrDefault(userId)?.FirstOrDefault(i => i.IsPrimary) is { } primary
            ? StatusHelper.GetInstrumentDisplay(primary.InstrumentType)
            : null;

    private static DateTime? NextBirthday(DateTime birth, int year) =>
        // 29 February has no date in a common year: it is skipped.
        DateTime.IsLeapYear(year) || birth.Month != 2 || birth.Day != 29 ? new DateTime(year, birth.Month, birth.Day) : null;

    private static bool Retired(ActiveMemberData m) => m.StatusData?.IsRetired ?? m.Member.IsRetired;

    private static int ReactivationPriority(ActiveMemberData m)
    {
        if (!Retired(m))
        {
            return 0;
        }

        return m.StatusData is { ProgressTotalMonths: 3, ProgressMonths: { } months }
            ? months switch { >= 2 => 1, 1 => 2, 0 => 3, _ => 4 }
            : 4;
    }

    /// <summary>Retired: always. Active: only between 1 and 5 months without activity and none this month.</summary>
    internal static bool ShowProgress(MemberStatusResult? s)
    {
        if (s?.ProgressDescription is null)
        {
            return false;
        }

        if (!s.IsRetired && s.ProgressTotalMonths == 6)
        {
            return !s.HasActivityInCurrentMonth && s.ProgressMonths is >= 1 and <= 5;
        }

        return true;
    }

    /// <summary>A retired member two months into the three that bring them back, with nothing this month yet.</summary>
    internal static bool Encourage(MemberStatusResult? s) =>
        s is { IsRetired: true, ProgressTotalMonths: 3, ProgressMonths: 2, HasActivityInCurrentMonth: false };

    private async Task<MemberStateDto?> StateAsync(ApplicationUser member)
    {
        var activeCategory = member.Categories.Any(c => c is MemberCategory.Leitao or MemberCategory.Caloiro or MemberCategory.Tuno
            or MemberCategory.Veterano or MemberCategory.Tunossauro);
        if (!activeCategory || member.IsTunoHonorario())
        {
            return null;
        }

        MemberStatusResult? status;
        List<AttendedActivityDto>? activities;
        try
        {
            status = await _status.GetMemberStatusAsync(member.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the status of {UserId}", member.Id);
            status = null;
        }

        if (status is not { HasAnyActivity: true })
        {
            return null;
        }

        try
        {
            activities = await _statistics.GetUserAttendedActivitiesAsync(member.Id, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the activities of {UserId}", member.Id);
            activities = null;
        }

        return new MemberStateDto(
            member.IsLeitao() ? null : status.IsRetired,
            ShowProgress(status) ? status.ProgressDescription : null,
            status.ProgressMonths, status.ProgressTotalMonths,
            Encourage(status),
            status.LastRehearsalDate, status.LastEventDate,
            (activities ?? new List<AttendedActivityDto>()).Select(a => new MemberActivityDto(a.Date, a.Name, a.Type, a.IsRehearsal)).ToList());
    }

    /// <summary>"Percurso na Tuna": memberships, sub-categories and positions by year.</summary>
    internal static IReadOnlyList<MemberTimelineItemDto> Timeline(ApplicationUser u, IEnumerable<RoleAssignment> assignments, int currentYear)
    {
        var items = new List<(int Year, int Order, MemberTimelineItemDto Item)>();
        void Add(int year, int order, string label, string kind, string state, string accent, string? years = null, string? notes = null) =>
            items.Add((year, order, new MemberTimelineItemDto(label, years ?? year.ToString(CultureInfo.InvariantCulture), kind, state, accent, notes)));

        if (u.YearLeitao is { } leitao)
        {
            Add(leitao, 1, "LEITÃO", "membership", u.IsLeitao() && !u.IsCaloiro() && !u.IsTuno() ? "active" : "completed", "leitao");
        }

        if (u.YearCaloiro is { } caloiro)
        {
            Add(caloiro, 2, "CALOIRO", "membership", u.IsCaloiro() && !u.IsTuno() ? "active" : u.IsTuno() ? "completed" : "", "caloiro");
        }

        if (u.YearTuno is { } tuno)
        {
            if (u.IsTunoHonorario())
            {
                Add(tuno, 3, "TUNO HONORÁRIO", "subcategory", "active", "honorario");
            }
            else
            {
                if (u.IsFundador())
                {
                    Add(tuno, 3, "FUNDADOR", "subcategory", "active", "fundador");
                }

                Add(tuno, 4, "TUNO", "membership", "active", "tuno");
                if (u.QualifiesForVeterano())
                {
                    Add(tuno + 2, 4, "VETERANO", "subcategory", "active", "veterano");
                }

                if (u.QualifiesForTunossauro())
                {
                    Add(tuno + 6, 5, "TUNOSSAURO", "subcategory", "active", "tunossauro");
                }
            }
        }

        var now = currentYear;
        foreach (var a in assignments.OrderBy(a => a.StartYear))
        {
            Add(a.StartYear, 10, StatusHelper.GetPositionDisplay(a.Position), "role", a.EndYear >= now ? "active" : "completed",
                a.Position == Position.Magister ? "magister" : "role", $"{a.StartYear}–{a.EndYear}", a.Notes);
        }

        return items.OrderBy(i => i.Year).ThenBy(i => i.Order).Select(i => i.Item).ToList();
    }
}

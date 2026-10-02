using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The read side of the old Blazor /members and /hierarchy behind the React /members (React track 017,
/// docs/react-members.md). Same data and rules, delegated to the services the old page used:
/// <see cref="IMemberFilterService"/> (search over name, nickname, email, phone and city; category, Tuno sub-category,
/// instrument and "só ativos"; Tunos Honorários only when asked for or searched), <see cref="IActiveMemberFilterService"/>,
/// <see cref="IMemberAnniversaryService"/>, <see cref="IMemberHierarchyService"/>, <see cref="IMemberStatusService"/> and
/// <see cref="IMemberStatisticsService"/>. Like the old page load, a Leitão with no cached status gets it computed.
/// Signed-in members only (<see cref="MembersAuthorization"/>). No schema change.
/// </summary>
public sealed class MemberDirectoryService : IMemberDirectoryService
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");
    private static readonly string[] Categories = { "Leitao", "Caloiro", "Tuno", "TunoHonorario" };
    private static readonly string[] SubCategories = { "Tuno", "Veterano", "Tunossauro", "Fundador" };

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IRoleAssignmentService _assignments;
    private readonly IMemberInstrumentService _instruments;
    private readonly IMemberFilterService _filter;
    private readonly IActiveMemberFilterService _activeFilter;
    private readonly IMemberAnniversaryService _anniversaries;
    private readonly IMemberHierarchyService _hierarchy;
    private readonly IMemberStatusService _status;
    private readonly IMemberStatisticsService _statistics;
    private readonly ILogger<MemberDirectoryService> _logger;

    public MemberDirectoryService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IRoleAssignmentService assignments,
        IMemberInstrumentService instruments,
        IMemberFilterService filter,
        IActiveMemberFilterService activeFilter,
        IMemberAnniversaryService anniversaries,
        IMemberHierarchyService hierarchy,
        IMemberStatusService status,
        IMemberStatisticsService statistics,
        ILogger<MemberDirectoryService> logger)
    {
        _contexts = contexts;
        _assignments = assignments;
        _instruments = instruments;
        _filter = filter;
        _activeFilter = activeFilter;
        _anniversaries = anniversaries;
        _hierarchy = hierarchy;
        _status = status;
        _statistics = statistics;
        _logger = logger;
    }

    public async Task<EventResult<MemberDirectoryDto>> GetDirectoryAsync(MemberDirectoryQuery query, ClaimsPrincipal user)
    {
        if (!MembersAuthorization.IsMember(user))
        {
            return EventResult<MemberDirectoryDto>.Fail(EventResultStatus.SignInRequired);
        }

        var category = Pick(query.Category, Categories);
        var subCategory = category == "Tuno" ? Pick(query.SubCategory, SubCategories) : "";
        var instrument = Pick(query.Instrument, Enum.GetNames<InstrumentType>());
        if (category is null || subCategory is null || instrument is null)
        {
            return EventResult<MemberDirectoryDto>.Invalid("filter", "Filtro inválido.");
        }

        var users = await UsersAsync();
        var instruments = await _instruments.GetMemberInstrumentsByUserIdsAsync(users.Select(u => u.Id));
        var (regular, leitoes, _) = _filter.FilterMembers(users, query.Search?.Trim() ?? "", query.ActiveOnly, instrument, category, subCategory, instruments);

        var current = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        var positions = (await _assignments.GetAllRoleAssignmentsAsync())
            .Where(a => a.StartYear == current)
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.First().Position);

        var leitaoStatus = await LeitaoStatusesAsync(users.Where(u => u.IsLeitao()).Select(u => u.Id).ToList());

        // As the old grids: members by nickname; Leitões by number of activities, then the most recent first.
        var members = regular
            .OrderBy(u => u.Nickname ?? "")
            .Select(u => Card(u, RegularBadges(u), positions.TryGetValue(u.Id, out var p) ? StatusHelper.GetPositionDisplay(p) : null,
                !u.Positions.Any() && !u.Categories.Any(), instruments, inactive: false, expelled: false))
            .ToList();
        var leitaoCards = leitoes
            .OrderByDescending(u => leitaoStatus.GetValueOrDefault(u.Id)?.TotalActivitiesCount ?? 0)
            .ThenByDescending(u => leitaoStatus.GetValueOrDefault(u.Id)?.LastActivityDate ?? DateTime.MinValue)
            .Select(u => Card(u, new[] { Badge(MemberCategory.Leitao) }, null, false, instruments,
                inactive: leitaoStatus.GetValueOrDefault(u.Id)?.HasActivityInCurrentMonth != true, expelled: u.IsExpelled))
            .ToList();

        var options = Enum.GetValues<InstrumentType>()
            .Select(i => new MemberOptionDto(i.ToString(), StatusHelper.GetInstrumentDisplay(i)))
            .ToList();

        return EventResult<MemberDirectoryDto>.Ok(new MemberDirectoryDto(members, leitaoCards, options, MembersAuthorization.CanManage(user)));
    }

    public async Task<EventResult<MemberDetailDto>> GetMemberAsync(string id, ClaimsPrincipal user)
    {
        if (!MembersAuthorization.IsMember(user))
        {
            return EventResult<MemberDetailDto>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var member = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (member is null)
        {
            return EventResult<MemberDetailDto>.Fail(EventResultStatus.NotFound);
        }

        var who = GovernanceService.ToMember(member.Nickname, member.FirstName, member.LastName, member.ImageUrl);
        var honorario = member.IsTunoHonorario();
        var instruments = honorario
            ? new List<string>()
            : (await _instruments.GetMemberInstrumentsAsync(member.Id))
                .OrderByDescending(i => i.IsPrimary)
                .Select(i => StatusHelper.GetInstrumentDisplay(i.InstrumentType) + (i.IsPrimary ? " (Principal)" : ""))
                .ToList();

        var showMentor = !member.IsFundador() && !honorario && !member.IsLeitao();
        string? mentor = null;
        if (showMentor && !string.IsNullOrEmpty(member.MentorId)
            && await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == member.MentorId) is { } m)
        {
            mentor = m.GetDisplayName();
        }

        var assignments = (await _assignments.GetAllRoleAssignmentsAsync()).Where(a => a.UserId == member.Id).ToList();

        return EventResult<MemberDetailDto>.Ok(new MemberDetailDto(
            member.Id, who.DisplayName, who.FullName, who.AvatarUrl,
            StatusHelper.GetDisplayCategories(member).Select(Badge).ToList(),
            member.Positions.Select(StatusHelper.GetPositionDisplay).ToList(),
            member.Email, member.PhoneNumber, member.City,
            member.DateOfBirth?.ToString("dd/MM/yyyy"), member.Age, member.Degree,
            !honorario, instruments, showMentor, mentor,
            Timeline(member, assignments),
            await StateAsync(member)));
    }

    public async Task<EventResult<IReadOnlyList<ActiveMemberDto>>> GetActiveMembersAsync(string? status, string? search, ClaimsPrincipal user)
    {
        if (!MembersAuthorization.IsMember(user))
        {
            return EventResult<IReadOnlyList<ActiveMemberDto>>.Fail(EventResultStatus.SignInRequired);
        }

        if (Pick(status, new[] { "active", "retired" }) is not { } statusFilter)
        {
            return EventResult<IReadOnlyList<ActiveMemberDto>>.Invalid("status", "Estado inválido.");
        }

        // Caloiro, Tuno, Veterano, Tunossauro; never Leitões or Tunos Honorários.
        var users = (await UsersAsync())
            .Where(u => !u.IsLeitao() && !u.IsTunoHonorario() && u.IsEffectiveMember())
            .ToList();
        var instruments = await _instruments.GetMemberInstrumentsByUserIdsAsync(users.Select(u => u.Id));
        Dictionary<string, MemberStatusResult?> statuses;
        try
        {
            statuses = await _status.GetMemberStatusesBatchAsync(users.Select(u => u.Id).ToList());
        }
        catch (Exception ex)
        {
            // As before: without statuses the list still shows, from the members' own retired flag.
            _logger.LogWarning(ex, "Could not load member statuses");
            statuses = new Dictionary<string, MemberStatusResult?>();
        }

        var ordered = users
            .Select(u => new ActiveMemberData { Member = u, StatusData = statuses.GetValueOrDefault(u.Id) })
            .OrderBy(m => Retired(m) ? 1 : 0)
            .ThenBy(ReactivationPriority)
            .ThenByDescending(m => m.StatusData?.LastActivityDate ?? DateTime.MinValue)
            .ThenByDescending(m => m.StatusData?.LastRehearsalDate.HasValue == true && m.StatusData.LastEventDate.HasValue)
            .ThenByDescending(m => m.StatusData?.LastRehearsalDate ?? DateTime.MinValue)
            .ThenByDescending(m => m.StatusData?.LastEventDate ?? DateTime.MinValue)
            .ThenByDescending(m => m.StatusData?.TotalActivitiesCount ?? 0)
            .ThenBy(m => m.Member.Nickname ?? m.Member.FirstName ?? string.Empty)
            .ToList();

        return EventResult<IReadOnlyList<ActiveMemberDto>>.Ok(_activeFilter.FilterActiveMembers(ordered, statusFilter, search?.Trim() ?? "")
            .Select(m =>
            {
                var who = GovernanceService.ToMember(m.Member.Nickname, m.Member.FirstName, m.Member.LastName, m.Member.ImageUrl);
                return new ActiveMemberDto(m.Member.Id, who.DisplayName, who.FullName, who.AvatarUrl, PrimaryInstrument(m.Member.Id, instruments),
                    Retired(m), m.StatusData?.LastRehearsalDate, m.StatusData?.LastEventDate,
                    ShowProgress(m.StatusData) ? m.StatusData!.ProgressDescription : null, Encourage(m.StatusData));
            })
            .ToList());
    }

    public async Task<EventResult<IReadOnlyList<MemberBirthdayDto>>> GetBirthdaysAsync(string? search, ClaimsPrincipal user)
    {
        if (!MembersAuthorization.IsMember(user))
        {
            return EventResult<IReadOnlyList<MemberBirthdayDto>>.Fail(EventResultStatus.SignInRequired);
        }

        // The birthdays from today to the end of the year, soonest first, then by name - as the old list.
        var today = DateTime.Now.Date;
        var upcoming = (await UsersAsync())
            .Where(u => u.DateOfBirth.HasValue)
            .Select(u => (User: u, Next: NextBirthday(u.DateOfBirth!.Value, today.Year)))
            .Where(x => x.Next is { } next && next >= today)
            .OrderBy(x => x.Next)
            .ThenBy(x => x.User.FirstName)
            .ThenBy(x => x.User.LastName)
            .ToList();
        var shown = _anniversaries.FilterAnniversaries(upcoming.Select(x => x.User), search?.Trim() ?? "").Select(u => u.Id).ToHashSet();

        return EventResult<IReadOnlyList<MemberBirthdayDto>>.Ok(upcoming
            .Where(x => shown.Contains(x.User.Id))
            .Select(x =>
            {
                var who = GovernanceService.ToMember(x.User.Nickname, x.User.FirstName, x.User.LastName, x.User.ImageUrl);
                return new MemberBirthdayDto(x.User.Id, who.DisplayName, who.FullName, who.AvatarUrl,
                    today.Year - x.User.DateOfBirth!.Value.Year, x.User.City, x.User.Categories.Select(Badge).ToList(),
                    x.User.DateOfBirth.Value.ToString("dd/MM"), x.Next!.Value.ToString("yyyy-MM-dd"));
            })
            .ToList());
    }

    public async Task<EventResult<IReadOnlyList<MemberTreeNodeDto>>> GetHierarchyAsync(ClaimsPrincipal user)
    {
        if (!MembersAuthorization.IsMember(user))
        {
            return EventResult<IReadOnlyList<MemberTreeNodeDto>>.Fail(EventResultStatus.SignInRequired);
        }

        var (roots, children) = _hierarchy.BuildHierarchy(await UsersAsync());

        MemberTreeNodeDto Node(ApplicationUser u)
        {
            var who = GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl);
            // Afilhados by the month they became Caloiro, then by name - as the old tree.
            var afilhados = children.TryGetValue(u.Id, out var list)
                ? list.OrderBy(a => a.YearCaloiro ?? int.MaxValue).ThenBy(a => a.MonthCaloiro ?? int.MaxValue)
                    .ThenBy(a => a.FirstName).ThenBy(a => a.LastName).Select(Node).ToList()
                : new List<MemberTreeNodeDto>();
            return new MemberTreeNodeDto(u.Id, who.DisplayName, who.FullName, who.AvatarUrl, afilhados);
        }

        return EventResult<IReadOnlyList<MemberTreeNodeDto>>.Ok(roots.Select(Node).ToList());
    }

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

        var statuses = await _status.GetMemberStatusesBatchAsync(ids);
        foreach (var id in ids.Where(id => statuses.GetValueOrDefault(id) is null))
        {
            try
            {
                statuses[id] = await _status.UpdateMemberStatusAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update status for Leitão {UserId}", id);
            }
        }

        return statuses;
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
        // 29 February has no date in a common year; the old list threw there, here it is just skipped.
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
    private static bool ShowProgress(MemberStatusResult? s)
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
    private static bool Encourage(MemberStatusResult? s) =>
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

    /// <summary>"Percurso na Tuna", as the old UnifiedTimeline: memberships, sub-categories and positions by year.</summary>
    internal static IReadOnlyList<MemberTimelineItemDto> Timeline(ApplicationUser u, IEnumerable<RoleAssignment> assignments)
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

        var now = DateTime.Now.Year;
        foreach (var a in assignments.OrderBy(a => a.StartYear))
        {
            Add(a.StartYear, 10, StatusHelper.GetPositionDisplay(a.Position), "role", a.EndYear >= now ? "active" : "completed",
                a.Position == Position.Magister ? "magister" : "role", $"{a.StartYear}–{a.EndYear}", a.Notes);
        }

        return items.OrderBy(i => i.Year).ThenBy(i => i.Order).Select(i => i.Item).ToList();
    }
}

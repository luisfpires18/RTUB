// Member directory: directory, member detail, active members, birthdays, hierarchy.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public sealed partial class MemberDirectoryService
{
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

        var recent = _time.GetUtcNow().UtcDateTime.AddDays(-15);
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
                inactive: !(leitaoStatus.GetValueOrDefault(u.Id)?.LastActivityDate >= recent), expelled: u.IsExpelled))
            .ToList();

        var options = Enum.GetValues<InstrumentType>()
            .Select(i => new MemberOptionDto(i.ToString(), StatusHelper.GetInstrumentDisplay(i)))
            .ToList();

        return EventResult<MemberDirectoryDto>.Ok(new MemberDirectoryDto(members, leitaoCards, options, MembersAuthorization.CanManage(user),
            MembersAuthorization.CanDeleteAnyMember(user), MembersAuthorization.CanEditInfo(user)));
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
            Timeline(member, assignments, PortugalTime.Today(_time).Year),
            await StateAsync(member),
            member.IsLeitao(), member.IsExpelled));
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
        var today = PortugalTime.Today(_time);
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
}

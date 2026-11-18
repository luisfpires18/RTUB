using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// "As minhas ações pendentes" (docs/react-pending-actions.md). Composes the registered
/// <see cref="IPendingActionSource"/>s: each panel is one module's own query, filtered for the member and paged in SQL.
/// No inbox table: the only thing stored is a member's own dismissal of an optional action
/// (<see cref="PendingActionDismissal"/>).
/// </summary>
public sealed partial class PendingActionsService : IPendingActionsService
{
    /// <summary>Rows per panel page: the approved footprint (three rows), and the API's bound.</summary>
    public const int PageSize = 3;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IReadOnlyList<IPendingActionSource> _sources;
    private readonly TimeProvider _time;

    public PendingActionsService(IDbContextFactory<ApplicationDbContext> contexts, IEnumerable<IPendingActionSource> sources, TimeProvider? time = null)
    {
        _contexts = contexts;
        _sources = sources.ToList();
        _time = time ?? TimeProvider.System;
    }

    public async Task<EventResult<PendingActionsPageDto>> GetPageAsync(ClaimsPrincipal user)
    {
        if (EventsAuthorization.UserId(user) is not { } userId)
        {
            return EventResult<PendingActionsPageDto>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await LoadMemberAsync(db, userId) is not { } member)
        {
            return EventResult<PendingActionsPageDto>.Fail(EventResultStatus.SignInRequired);
        }

        if (member.IsRetired)
        {
            // The page is not for a Reformado at all (no entry in the menu either); the client goes to the agenda.
            return EventResult<PendingActionsPageDto>.Fail(EventResultStatus.NotFound);
        }

        var panels = new List<PendingActionPanelDto>();
        foreach (var source in _sources.Where(s => Applies(s, member)))
        {
            panels.Add(await PanelAsync(db, source, member, null, 1));
        }

        var status = !member.IsSubjectToActivityRule ? "none" : member.IsRetired ? "retired" : "active";
        var (options, primary) = panels.Count == 0 ? ([], null) : await InstrumentOptionsAsync(db, member);
        return EventResult<PendingActionsPageDto>.Ok(new PendingActionsPageDto(
            member.Today.ToString("yyyy-MM-dd"), member.FiscalYear, status, panels,
            options.Select(i => new EventInstrumentOptionDto(i.ToString(), StatusHelper.GetInstrumentDisplay(i))).ToList(),
            primary?.ToString()));
    }

    public async Task<EventResult<PendingActionPanelDto>> GetPanelAsync(ClaimsPrincipal user, string panel, string? filter, int page)
    {
        if (EventsAuthorization.UserId(user) is not { } userId)
        {
            return EventResult<PendingActionPanelDto>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await LoadMemberAsync(db, userId) is not { } member)
        {
            return EventResult<PendingActionPanelDto>.Fail(EventResultStatus.SignInRequired);
        }

        // A panel that does not apply to the member is a 404, exactly like one that does not exist.
        var source = _sources.FirstOrDefault(s => s.Panel == panel && Applies(s, member));
        return source is null
            ? EventResult<PendingActionPanelDto>.Fail(EventResultStatus.NotFound)
            : EventResult<PendingActionPanelDto>.Ok(await PanelAsync(db, source, member, filter, page));
    }

    public async Task<int> CountPendingAsync(string userId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        if (await LoadMemberAsync(db, userId) is not { } member)
        {
            return 0;
        }

        var total = 0;
        foreach (var source in _sources.Where(s => Applies(s, member)))
        {
            if (source.Filters.Count == 0)
            {
                total += await source.CountAsync(db, member, null);
                continue;
            }

            foreach (var (key, _) in source.Filters)
            {
                total += await source.CountAsync(db, member, key);
            }
        }

        return total;
    }

    private static async Task<PendingActionPanelDto> PanelAsync(
        ApplicationDbContext db, IPendingActionSource source, PendingActionMember member, string? filter, int page)
    {
        var filters = new List<PendingActionFilterDto>();
        foreach (var (key, label) in source.Filters)
        {
            filters.Add(new PendingActionFilterDto(key, label, await source.CountAsync(db, member, key)));
        }

        // An unknown filter falls back to the default one, as an unknown page falls back into range.
        var selected = filters.FirstOrDefault(f => f.Key == filter) ?? filters.FirstOrDefault();
        var total = selected?.Count ?? await source.CountAsync(db, member, null);
        var last = Math.Max(1, (total + PageSize - 1) / PageSize);
        var current = Math.Clamp(page, 1, last);
        var rows = total == 0
            ? []
            : await source.PageAsync(db, member, selected?.Key, (current - 1) * PageSize, PageSize);

        return new PendingActionPanelDto(source.Panel, source.Title, source.Category, filters, selected?.Key, current, PageSize, total, rows);
    }

    /// <summary>
    /// Owner decision: a Reformado has no pending action at all - no participation, no own work,
    /// no responsibility - whatever the source would say. Everyone else is up to the source's own rule.
    /// </summary>
    private static bool Applies(IPendingActionSource source, PendingActionMember member) => !member.IsRetired && source.AppliesTo(member);

    /// <summary>The member as the database has them now (not the cookie's claims); null for an unknown or expelled account.</summary>
    private async Task<PendingActionMember?> LoadMemberAsync(ApplicationDbContext db, string userId)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || user.IsExpelled)
        {
            return null;
        }

        var roles = await db.UserRoles.Where(r => r.UserId == userId)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
            .ToListAsync();
        var today = PortugalTime.Today(_time);
        return new PendingActionMember(
            userId, user.Categories, roles.ToHashSet(),
            MemberActivityStatusEvaluator.IsSubjectToRule(user), user.IsRetired,
            today, FiscalYearHelper.GetFiscalYearStartYear(today), JoinedOn(user), user.Positions);
    }

    /// <summary>The first day of the earliest admission month (Leitão, Caloiro, Tuno; a missing month is January), if any.</summary>
    internal static DateTime? JoinedOn(ApplicationUser user) =>
        new[] { (user.YearLeitao, user.MonthLeitao), (user.YearCaloiro, user.MonthCaloiro), (user.YearTuno, user.MonthTuno) }
            .Where(m => m.Item1 is >= 1 and <= 9999)
            .Select(m => (DateTime?)new DateTime(m.Item1!.Value, m.Item2 is >= 1 and <= 12 ? m.Item2.Value : 1, 1))
            .Min();

    private static Task<List<InstrumentType>> RegisteredAsync(ApplicationDbContext db, string userId) =>
        db.MemberInstruments.AsNoTracking()
            .Where(i => i.MemberId == userId)
            .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Id)
            .Select(i => i.InstrumentType)
            .ToListAsync();

    /// <summary>
    /// The instruments an answer may name, as the event and rehearsal pages offer them (EventAgendaService /
    /// RehearsalAgendaService): the member's own, primary first; a Leitão with none may pick any; anyone else with none, none.
    /// </summary>
    private static async Task<(List<InstrumentType> Options, InstrumentType? Primary)> InstrumentOptionsAsync(ApplicationDbContext db, PendingActionMember member)
    {
        var registered = await RegisteredAsync(db, member.UserId);
        var options = registered.Count > 0 ? registered
            : member.Categories.Contains(MemberCategory.Leitao) ? Enum.GetValues<InstrumentType>().ToList() : [];
        return (options, registered.Count > 0 ? registered[0] : null);
    }
}

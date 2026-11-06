using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// "As minhas ações pendentes" (task 071, docs/react-pending-actions.md). Composes the registered
/// <see cref="IPendingActionSource"/>s: each panel is one module's own query, filtered for the member and paged in SQL.
/// No inbox table: the only thing stored is a member's own dismissal of an optional action
/// (<see cref="PendingActionDismissal"/>).
/// </summary>
public sealed class PendingActionsService : IPendingActionsService
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

        var panels = new List<PendingActionPanelDto>();
        foreach (var source in _sources.Where(s => s.AppliesTo(member)))
        {
            panels.Add(await PanelAsync(db, source, member, null, 1));
        }

        var status = !member.IsSubjectToActivityRule ? "none" : member.IsRetired ? "retired" : "active";
        return EventResult<PendingActionsPageDto>.Ok(new PendingActionsPageDto(
            member.Today.ToString("yyyy-MM-dd"), member.FiscalYear, status, panels));
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
        var source = _sources.FirstOrDefault(s => s.Panel == panel && s.AppliesTo(member));
        return source is null
            ? EventResult<PendingActionPanelDto>.Fail(EventResultStatus.NotFound)
            : EventResult<PendingActionPanelDto>.Ok(await PanelAsync(db, source, member, filter, page));
    }

    public async Task<EventResult<bool>> DismissAsync(ClaimsPrincipal user, PendingActionDismissInput input)
    {
        if (EventsAuthorization.UserId(user) is not { } userId)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        var kind = input.Kind?.Trim() ?? "";
        var sourceKey = input.SourceKey?.Trim() ?? "";
        if (kind.Length is 0 or > 64 || sourceKey.Length > 64)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await LoadMemberAsync(db, userId) is not { } member)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        // Only an action that is pending for this member now, from a panel that applies to them: whatever the request says,
        // the row (and whether it is required or optional) is the server's.
        foreach (var source in _sources.Where(s => s.AppliesTo(member)))
        {
            if (await source.FindAsync(db, member, kind, sourceKey) is not { } found)
            {
                continue;
            }

            if (found.Row.Required || !found.Row.Dismissible)
            {
                return EventResult<bool>.Fail(EventResultStatus.Forbidden);
            }

            var exists = await db.PendingActionDismissals.AnyAsync(d =>
                d.UserId == userId && d.ActionKind == kind && d.SourceKey == sourceKey && d.Period == found.Period);
            if (!exists)
            {
                db.PendingActionDismissals.Add(new PendingActionDismissal
                {
                    UserId = userId, ActionKind = kind, SourceKey = sourceKey, Period = found.Period,
                    DismissedAt = _time.GetUtcNow().UtcDateTime,
                });
                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // A concurrent click stored the same row (unique index): the outcome is the same.
                }
            }

            return EventResult<bool>.Ok(true);
        }

        return EventResult<bool>.Fail(EventResultStatus.NotFound);
    }

    public async Task<int> CountPendingAsync(string userId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        if (await LoadMemberAsync(db, userId) is not { } member)
        {
            return 0;
        }

        var total = 0;
        foreach (var source in _sources.Where(s => s.AppliesTo(member)))
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
            today, FiscalYearHelper.GetFiscalYearStartYear(today));
    }
}

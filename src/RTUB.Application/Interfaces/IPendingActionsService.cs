using System.Security.Claims;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// "As minhas ações pendentes" (task 071, docs/react-pending-actions.md): the member's grouped actions, queried from the
/// real modules on every call (no stored inbox), each panel paged on its own. Every rule is decided here from the
/// database, not from the cookie: categories, activity status and roles are read fresh, so a role or mandate change shows
/// on the next load.
/// </summary>
public interface IPendingActionsService
{
    Task<EventResult<PendingActionsPageDto>> GetPageAsync(ClaimsPrincipal user);

    Task<EventResult<PendingActionPanelDto>> GetPanelAsync(ClaimsPrincipal user, string panel, string? filter, int page);

    /// <summary>
    /// The member's own "não" on an optional action that is pending for them now. Required actions → Forbidden; an action
    /// that is not (or no longer) in the member's list → NotFound. Idempotent: a repeat (double click, retry) finds nothing
    /// pending, answers NotFound and stores nothing; concurrent repeats meet the unique index and still leave one row.
    /// </summary>
    Task<EventResult<bool>> DismissAsync(ClaimsPrincipal user, PendingActionDismissInput input);

    /// <summary>
    /// How many actions are pending for the member across every panel and filter (dismissed ones excluded); 0 for an
    /// unknown account. The shared query for the scheduled reminder (#259): counts only, no rows.
    /// </summary>
    Task<int> CountPendingAsync(string userId);
}

/// <summary>Who the page is for, read from the database for this call.</summary>
public sealed record PendingActionMember(
    string UserId,
    IReadOnlyList<MemberCategory> Categories,
    IReadOnlySet<string> Roles,
    bool IsSubjectToActivityRule,
    bool IsRetired,
    DateTime Today,
    int FiscalYearStart)
{
    /// <summary>The canonical active member: someone the activity rule covers who is not Reformado (docs/member-activity-status.md).</summary>
    public bool IsActive => IsSubjectToActivityRule && !IsRetired;

    public DateTime FiscalYearStartDate => new(FiscalYearStart, 9, 1);

    public string FiscalYear => $"{FiscalYearStart}-{FiscalYearStart + 1}";
}

/// <summary>
/// One panel of the page, backed by one module's own data (task 071). A source filters what the member may not see and
/// what they dismissed in the query, before counting and paging, and pages in SQL: only the rows shown leave the database.
/// Registered in DI; the service asks every source in registration order.
/// </summary>
public interface IPendingActionSource
{
    string Panel { get; }
    string Title { get; }
    /// <summary>"participation", "tasks" or "responsibilities".</summary>
    string Category { get; }
    /// <summary>The sub-filters in order; the first is the default. Empty when the panel has none.</summary>
    IReadOnlyList<(string Key, string Label)> Filters { get; }

    bool AppliesTo(PendingActionMember member);

    Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter);

    Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take);

    /// <summary>The action as it is pending for the member now, or null; with the period a dismissal of it is kept for.</summary>
    Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey);
}

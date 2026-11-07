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
    /// The member's own in-page answer to an activity pending for them now (task 072): Vou / Não vou, Fui / Não fui.
    /// Written through the module's domain service, create-only: an activity already answered (here, in the module, or
    /// by an Admin), cancelled, out of the window or not the member's to answer → NotFound; a concurrent first answer →
    /// Conflict; an instrument the page did not offer or a long note → Invalid.
    /// </summary>
    Task<EventResult<bool>> RespondAsync(ClaimsPrincipal user, PendingActionResponseInput input);

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
    int FiscalYearStart,
    DateTime? JoinedOn = null,
    IReadOnlyList<Position>? Positions = null)
{
    /// <summary>The member's current Órgãos Sociais positions (task 074): <c>ApplicationUser.Positions</c>, the mandate in office.</summary>
    public bool Holds(params Position[] positions) => Positions?.Any(positions.Contains) == true;

    /// <summary>A current member of the Direção (Magister, Vice-Magister, Secretário, 1.º / 2.º Tesoureiro), task 087.</summary>
    public bool HoldsDirecao => Holds(RTUB.Core.Helpers.OrgaoSocialHelper.GetPositionsForGroup(RTUB.Core.Enums.OrgaoSocialGroup.Direcao).ToArray());

    /// <summary>The canonical active member: someone the activity rule covers who is not Reformado (docs/member-activity-status.md).</summary>
    public bool IsActive => IsSubjectToActivityRule && !IsRetired;

    public DateTime FiscalYearStartDate => new(FiscalYearStart, 9, 1);

    /// <summary>
    /// Where past prompts start (task 072): 1 September of the current fiscal year, or the first day of the member's
    /// admission month (earliest of Leitão / Caloiro / Tuno) when that is later - activities before someone joined are not
    /// their missing answers.
    /// </summary>
    public DateTime PastFrom => JoinedOn is { } joined && joined > FiscalYearStartDate ? joined : FiscalYearStartDate;

    public string FiscalYear => $"{FiscalYearStart}-{FiscalYearStart + 1}";

    /// <summary>
    /// The member as a signed-in principal carrying the roles read from the database for this call (task 073), so a module
    /// service called from the page applies its own rules (LogisticsAuthorization, QuestionsAuthorization) to the
    /// member's current roles, not to the cookie's.
    /// </summary>
    public ClaimsPrincipal ToPrincipal() => new(new ClaimsIdentity(
        Roles.Select(r => new Claim(ClaimTypes.Role, r)).Prepend(new Claim(ClaimTypes.NameIdentifier, UserId)), "pending-actions"));
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

/// <summary>
/// A source whose rows are answered in the page (task 072: Atuações, Ensaios). The service has already checked the
/// member, the answer and the instrument; the responder checks the activity is still pending for the member and writes
/// through the module's own domain service, create-only.
/// </summary>
public interface IPendingActionResponder
{
    string Kind { get; }

    int NotesMax { get; }

    Task<EventResult<bool>> RespondAsync(ApplicationDbContext db, PendingActionMember member, string sourceKey, bool yes,
        InstrumentType? instrument, string? otherInstruments, string? notes);
}

/// <summary>
/// A source whose rows carry one-word commands run in place (task 073: Logística "done", Questões "close"). The service
/// has checked the member and that the answer is one of <see cref="Answers"/>; the command re-checks the row is pending for
/// the member and runs the module's own service, whose rules decide (and may refuse).
/// </summary>
public interface IPendingActionCommand
{
    string Kind { get; }

    IReadOnlySet<string> Answers { get; }

    Task<EventResult<bool>> RunAsync(ApplicationDbContext db, PendingActionMember member, string sourceKey, string answer);
}

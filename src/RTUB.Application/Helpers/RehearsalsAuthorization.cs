using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Rehearsals rules (React track 014, docs/react-rehearsals.md), enforced server-side by
/// <c>RehearsalAgendaService</c> and <c>RehearsalAdminService</c>. Roles inherit: Owner includes Admin.
///
/// - Visitors: nothing (the Blazor page was [Authorize]).
/// - Any signed-in member: every rehearsal and who said they go, their own presença (Vou / Não vou, instrument,
///   note) on an upcoming rehearsal, removing their own presença, statistics.
/// - Mod, Admin or Owner (<see cref="CanEdit"/>): create (one or a range) and edit a rehearsal.
/// - Admin or Owner (<see cref="CanManage"/>, was IsInRole("Admin") only): everything above plus cancel / reactivate,
///   delete, push notice, approve or remove anyone's presença and add a member once the rehearsal can be approved.
/// </summary>
public static class RehearsalsAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanEdit(ClaimsPrincipal user) => CanManage(user) || (IsMember(user) && user.IsInRole("Mod"));

    public static bool CanManage(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static string? UserId(ClaimsPrincipal user) =>
        IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}

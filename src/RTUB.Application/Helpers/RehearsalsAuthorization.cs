using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Rehearsals rules (React track 014, docs/react-rehearsals.md), enforced server-side by
/// <c>RehearsalAgendaService</c> and <c>RehearsalAdminService</c>. Roles inherit: Owner includes Admin.
///
/// - Visitors: nothing (the Blazor page was [Authorize]).
/// - Any signed-in member: every rehearsal and who said they go, their own presença (Vou / Não vou, instrument,
///   note) on an upcoming rehearsal, removing their own presença, statistics.
/// - Admin or Owner (was IsInRole("Admin") only): create, edit, cancel / reactivate, delete, push notice,
///   approve or remove anyone's presença and add a member once the rehearsal can be approved.
///   Mod has no rehearsal rights (as before).
/// </summary>
public static class RehearsalsAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanManage(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static string? UserId(ClaimsPrincipal user) =>
        IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}

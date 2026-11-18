using System.Security.Claims;
using RTUB.Core.Enums;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Rehearsals rules (docs/react-rehearsals.md), enforced server-side by
/// <c>RehearsalAgendaService</c> and <c>RehearsalAdminService</c>. Roles inherit: Owner includes Admin.
///
/// - Visitors: nothing.
/// - Any signed-in member: every rehearsal and who said they go, their own presença (Vou / Não vou, instrument,
///   note) on an upcoming rehearsal, removing their own presença, statistics.
/// - Mod, Admin or Owner (<see cref="CanEdit"/>): create (one or a range) and edit a rehearsal.
/// - Admin or Owner (<see cref="CanManage"/>, was IsInRole("Admin") only): everything above plus cancel / reactivate,
///   delete, push notice, approve or remove anyone's presença and add a member once the rehearsal can be approved.
/// - <see cref="CanApprove"/>: the current Ensaiador or Magister also approve and refuse pending presenças
///   once a rehearsal can be approved, even without Admin - only that, no other rehearsal management, and never their own
///   presença (only Admin / Owner may).
/// </summary>
public static class RehearsalsAuthorization
{
    public static bool CanEdit(ClaimsPrincipal user) => CanManage(user) || (user.IsMember() && user.IsInRole("Mod"));

    public static bool CanManage(ClaimsPrincipal user) => user.IsMember() && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    /// <summary>The positions whose holders review presenças.</summary>
    public static readonly Position[] Approvers = [Position.Ensaiador, Position.Magister];

    /// <summary>
    /// Approve or refuse a pending presença: Admin / Owner, or the current Ensaiador / Magister (<paramref name="positions"/>
    /// read from the database: the mandate in office).
    /// </summary>
    public static bool CanApprove(ClaimsPrincipal user, IEnumerable<Position>? positions) =>
        CanManage(user) || (user.IsMember() && positions?.Any(Approvers.Contains) == true);
}

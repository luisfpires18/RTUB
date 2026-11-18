using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Enums;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Events rules (docs/react-events.md), enforced server-side by
/// <c>EventAgendaService</c>. Roles inherit:
/// Owner includes Admin, Admin includes Mod.
///
/// - Visitors: the public agenda only (no descriptions, counts, repertoire or enrollments).
/// - Any signed-in member: the member view, who is going, and their own enrollment (expelled
///   members arrive anonymous: the cookie validator drops their session).
/// - Creating and editing an event (its fields and image, <see cref="CanEdit"/>): Mod and above.
/// - Managing events (delete, cancel / reactivate, notices, prizes, videos, repertoire, adding or removing others'
///   enrollments, <see cref="CanManage"/>): Admin or Owner. Tracking who was contacted (/events/{id}/contacts): Mod and
///   above, and any Leitão (by category).
/// </summary>
public static class EventsAuthorization
{
    public static bool CanEdit(ClaimsPrincipal user) => CanManage(user) || (user.IsMember() && user.IsInRole("Mod"));

    public static bool CanManage(ClaimsPrincipal user) => user.IsMember() && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static bool CanTrackContacts(ClaimsPrincipal user) => CanManage(user) || (user.IsMember() && user.IsInRole("Mod"));

    /// <summary>Contacts: Mod and above, or a member whose categories include Leitão (read from the database).</summary>
    public static async Task<bool> CanTrackContactsAsync(ApplicationDbContext db, ClaimsPrincipal user) =>
        CanTrackContacts(user)
        || (user.UserId() is { } id && await db.Users.AnyAsync(u => u.Id == id && u.Categories.Contains(MemberCategory.Leitao)));

    /// <summary>The Owner only (not inherited by Admin): deletes anyone's discussion posts and comments.</summary>
    public static bool IsOwner(ClaimsPrincipal user) => user.IsMember() && user.IsInRole("Owner");
}

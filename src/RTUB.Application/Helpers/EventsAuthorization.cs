using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Enums;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Events rules (React track 011, docs/react-events.md), enforced server-side by
/// <c>EventAgendaService</c> and used by the remaining Blazor event pages. Roles inherit:
/// Owner includes Admin, Admin includes Mod.
///
/// - Visitors: the public agenda only (no descriptions, counts, repertoire or enrollments).
/// - Any signed-in member: the member view, who is going, and their own enrollment (expelled
///   members arrive anonymous: the cookie validator drops their session).
/// - Managing events (the React agenda and event page tools, adding or removing others' enrollments):
///   Admin or Owner. Tracking who was contacted (/events/{id}/contacts): Mod and above, and any Leitão (by category).
/// </summary>
public static class EventsAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanManage(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static bool CanTrackContacts(ClaimsPrincipal user) => CanManage(user) || (IsMember(user) && user.IsInRole("Mod"));

    /// <summary>Contacts: Mod and above, or a member whose categories include Leitão (read from the database).</summary>
    public static async Task<bool> CanTrackContactsAsync(ApplicationDbContext db, ClaimsPrincipal user) =>
        CanTrackContacts(user)
        || (UserId(user) is { } id && await db.Users.AnyAsync(u => u.Id == id && u.Categories.Contains(MemberCategory.Leitao)));

    /// <summary>The Owner only (not inherited by Admin): deletes anyone's discussion posts and comments (013).</summary>
    public static bool IsOwner(ClaimsPrincipal user) => IsMember(user) && user.IsInRole("Owner");

    public static string? UserId(ClaimsPrincipal user) =>
        IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}

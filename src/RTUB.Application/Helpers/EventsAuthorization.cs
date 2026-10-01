using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Events rules (React track 011, docs/react-events.md), taken from the retired public Blazor
/// /events and enforced server-side by <c>EventAgendaService</c>.
///
/// - Visitors: the public agenda only (no descriptions, counts, repertoire or enrollments).
/// - Any signed-in member: the member view, and their own enrollment (expelled members arrive
///   anonymous: the cookie validator drops their session).
/// - Managing events stays on the Blazor /member/events, which checks the Admin role only (an Owner
///   without Admin has no management tools there); <see cref="CanManage"/> mirrors that, it only
///   decides whether the React pages show the "manage" link.
/// </summary>
public static class EventsAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanManage(ClaimsPrincipal user) => IsMember(user) && user.IsInRole("Admin");

    public static string? UserId(ClaimsPrincipal user) =>
        IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}

using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The /trintuna rules (docs/react-trintuna.md), enforced by <c>TrintunaService</c>. <c>listed</c> is whether
/// the caller has a <c>TrintunaAccess</c> row. Owner only manages (Admin is not Owner here).
///
/// - Owner: everything (gatherings, the access list, settling any date).
/// - A listed member: sees the page, votes and answers Vou / Não vou.
/// - The organizer of a gathering (while listed): edits it and settles its date.
/// - Anyone else: the page does not exist for them.
/// </summary>
public static class TrintunaAuthorization
{
    public static bool CanManage(ClaimsPrincipal user) => user.IsMember() && user.IsInRole("Owner");

    public static bool CanView(ClaimsPrincipal user, bool listed) => CanManage(user) || (user.IsMember() && listed);

    public static bool CanEdit(ClaimsPrincipal user, bool listed, string? organizerUserId) =>
        CanManage(user) || (CanView(user, listed) && organizerUserId is not null && user.UserId() == organizerUserId);

    public static bool CanAnswer(ClaimsPrincipal user, bool listed) => CanView(user, listed);
}

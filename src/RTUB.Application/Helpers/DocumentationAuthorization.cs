using System.Security.Claims;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Helpers;

/// <summary>
/// The documentation rules (React track 022, docs/react-documentation.md), enforced server-side by
/// <c>DocumentationService</c>. Same as the Blazor /documentation ([Authorize]; Leitões sent away; folder filter and
/// download check in the page; management buttons for Owner only):
///
/// - Visitors: nothing (401); the page sends them to sign in.
/// - Leitões: nothing (403), unless Owner.
/// - "Atas CV…" folders: Veterano, Tunossauro or Magister; "Atas AG…" folders: everyone but Leitões; any other folder
///   (Logistics boards included): every member. Owner sees every folder.
/// - Any member who sees a folder uploads into it (the old page offered "Carregar" to everyone).
/// - Owner only: create and delete folders, delete documents, replace a document by uploading the same name. Admin and
///   Mod have no extra rights here, as before.
/// </summary>
public static class DocumentationAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool IsOwner(ClaimsPrincipal user) => IsMember(user) && user.IsInRole("Owner");

    public static string? UserId(ClaimsPrincipal user) => IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    public static bool CanOpen(ApplicationUser member, bool isOwner) => isOwner || !member.IsLeitao();

    public static bool CanSeeFolder(ApplicationUser member, bool isOwner, string folder)
    {
        if (isOwner)
        {
            return true;
        }

        if (folder.StartsWith("Atas CV", StringComparison.OrdinalIgnoreCase))
        {
            return member.CurrentRole is "VETERANO" or "TUNOSSAURO" || member.Positions.Contains(Position.Magister);
        }

        return !folder.StartsWith("Atas AG", StringComparison.OrdinalIgnoreCase) || !member.IsLeitao();
    }
}

using System.Security.Claims;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Helpers;

/// <summary>
/// The documentation rules (docs/react-documentation.md), enforced server-side by
/// <c>DocumentationService</c>:
///
/// - Visitors: nothing; the page sends them to sign in.
/// - Leitões: nothing, unless Owner.
/// - "Atas CV…" folders: Veterano, Tunossauro or Magister; "Atas AG…" folders: everyone but Leitões; any other folder
///   (Logistics boards included): every member. Owner sees every folder.
/// - Any member who sees a folder uploads into it ("Carregar" is offered to everyone).
/// - Owner, Admin or Mod: create and delete folders, delete documents, replace a document by uploading the same name.
///   Admin and Mod otherwise see what a member sees.
/// </summary>
public static class DocumentationAuthorization
{
    public static bool IsOwner(ClaimsPrincipal user) => user.IsMember() && user.IsInRole("Owner");

    public static bool CanManage(ClaimsPrincipal user) => user.IsMember() && (user.IsInRole("Owner") || user.IsInRole("Admin") || user.IsInRole("Mod"));

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

using System.Security.Claims;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;

namespace RTUB.Application.Helpers;

/// <summary>
/// Which groups of the React member menu the signed-in caller sees. The same rules as
/// the pages behind each link. It only decides which links are shown: every page and
/// API still enforces its own rule, so a hidden link is a convenience, never the protection.
///
/// - <see cref="Management"/> (Reuniões, Documentação, Questões): the navbar's "Gestão": Admin, Owner or Mod, or any
///   member who is not a Leitão.
/// - <see cref="Logistics"/> (Logística): every signed-in member, Leitões included, as the Logistics API
///   allows. A Leitão's "Gestão" then shows Logística only: <see cref="Management"/> and <see cref="Treasury"/> are not
///   widened for it.
/// - <see cref="Treasury"/> (Tesouraria): the navbar's rule, Admin, Mod or Tuno, with Owner inheriting Admin as
///   everywhere else.
/// - <see cref="Admin"/> (Pedidos): the Admin role.
/// - <see cref="Owner"/> (Utilizadores, Auditoria, Base de dados, Emails, Notificações): the Owner role, as the five
///   pages require (Emails and Notificações included).
/// </summary>
public sealed record MemberMenuAccess(bool Management, bool Logistics, bool Treasury, bool Admin, bool Owner)
{
    public static MemberMenuAccess For(ClaimsPrincipal principal, ApplicationUser user)
    {
        var admin = principal.IsInRole("Admin");
        var owner = principal.IsInRole("Owner");
        var mod = principal.IsInRole("Mod");

        return new MemberMenuAccess(
            Management: admin || owner || mod || !user.IsLeitao(),
            Logistics: true,
            Treasury: admin || owner || mod || user.IsTuno(),
            Admin: admin,
            Owner: owner);
    }
}

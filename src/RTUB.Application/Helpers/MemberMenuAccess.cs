using System.Security.Claims;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;

namespace RTUB.Application.Helpers;

/// <summary>
/// Which groups of the React member menu (task 030) the signed-in caller sees. The same rules as the Blazor navbar
/// (<c>Shared/MainLayout.razor</c>, retired in 037) and the pages behind each link. It only decides which links are shown: every page and
/// API still enforces its own rule, so a hidden link is a convenience, never the protection.
///
/// - <see cref="Management"/> (Reuniões, Documentação, Questões): the navbar's "Gestão": Admin, Owner or Mod, or any
///   member who is not a Leitão.
/// - <see cref="LogisticsAndTreasury"/> (Logística, Tesouraria): the navbar's rule, Admin, Mod or Tuno, with Owner
///   inheriting Admin as everywhere else on the React track.
/// - <see cref="Admin"/> (Pedidos): the Admin role.
/// - <see cref="Owner"/> (Utilizadores, Auditoria, Base de dados, Emails, Notificações): the Owner role, as the five
///   pages require (task 035: Emails and Notificações were Admin pages and are Owner only now).
/// </summary>
public sealed record MemberMenuAccess(bool Management, bool LogisticsAndTreasury, bool Admin, bool Owner)
{
    public static MemberMenuAccess For(ClaimsPrincipal principal, ApplicationUser user)
    {
        var admin = principal.IsInRole("Admin");
        var owner = principal.IsInRole("Owner");
        var mod = principal.IsInRole("Mod");

        return new MemberMenuAccess(
            Management: admin || owner || mod || !user.IsLeitao(),
            LogisticsAndTreasury: admin || owner || mod || user.IsTuno(),
            Admin: admin,
            Owner: owner);
    }
}

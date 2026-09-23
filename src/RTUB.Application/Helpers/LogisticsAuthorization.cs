using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The single rule for who may manage Logistics: create, edit, delete, move and finish boards,
/// lists and cards. Admin and Mod have the same Logistics capabilities; every other role only
/// reads boards and creates reminders.
///
/// This grants Mod nothing outside Logistics. Admin-only pages keep their own role checks.
/// </summary>
public static class LogisticsAuthorization
{
    public static bool CanManage(ClaimsPrincipal user) =>
        user.IsInRole("Admin") || user.IsInRole("Mod");
}

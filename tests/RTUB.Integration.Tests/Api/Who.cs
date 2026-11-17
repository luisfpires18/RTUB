// Who: test class moved here from MeetingsApiTests.cs.
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
/// <summary>
/// Who a test member is. Veterano / Tunossauro / Tuno are by time (CurrentRole, from YearTuno / MonthTuno), as the rules
/// read them; Leitão and Caloiro are categories; positions are the member's own.
/// </summary>
internal static class Who
{
    public static readonly Action<ApplicationUser> Leitao = u => u.Categories = [MemberCategory.Leitao];
    public static readonly Action<ApplicationUser> Caloiro = u => u.Categories = [MemberCategory.Caloiro];
    public static readonly Action<ApplicationUser> TunoCategory = u => u.Categories = [MemberCategory.Tuno];
    public static readonly Action<ApplicationUser> Tuno = TunoFor(1);
    public static readonly Action<ApplicationUser> Veterano = TunoFor(3);
    public static readonly Action<ApplicationUser> Tunossauro = TunoFor(7);

    /// <summary>The Tuno category, Tuno since this month <paramref name="years"/> years ago.</summary>
    public static Action<ApplicationUser> TunoFor(int years) => u =>
    {
        u.Categories = [MemberCategory.Tuno];
        u.YearTuno = DateTime.Now.Year - years;
        u.MonthTuno = DateTime.Now.Month;
    };

    public static Action<ApplicationUser> Holding(params Position[] positions) => u => u.Positions = positions.ToList();
}

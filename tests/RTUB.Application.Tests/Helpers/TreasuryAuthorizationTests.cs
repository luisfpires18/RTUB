using FluentAssertions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// The Tesouraria rules (docs/react-treasury.md), one row per kind of member. "role" is the system role ("" for
/// none); "category" is what the member is in the tuna; "position" is the seat they hold (null for none).
/// </summary>
public class TreasuryAuthorizationTests
{
    [Theory]
    [InlineData("", MemberCategory.Tuno, true)]
    [InlineData("", MemberCategory.Veterano, true)]
    [InlineData("", MemberCategory.Caloiro, false)]
    [InlineData("", MemberCategory.Leitao, false)]
    [InlineData("Mod", MemberCategory.Caloiro, true)]
    [InlineData("Admin", MemberCategory.Leitao, true)]
    [InlineData("Owner", MemberCategory.Caloiro, true)]
    public void CanView_IsForManagers_AndEveryMemberWhoIsNeitherCaloiroNorLeitao(string role, MemberCategory category, bool expected) =>
        TreasuryAuthorization.CanView(Member(category), Session(role)).Should().Be(expected);

    [Theory]
    [InlineData("Admin", null, true)]
    [InlineData("Owner", null, true)]
    [InlineData("Mod", null, false)]
    [InlineData("Mod", Position.PrimeiroTesoureiro, true)]
    [InlineData("Mod", Position.SegundoTesoureiro, true)]
    [InlineData("Mod", Position.Magister, false)]
    [InlineData("", Position.PrimeiroTesoureiro, false)]
    [InlineData("", null, false)]
    public void CanManageReports_IsForAdminOwner_OrAModOfTheTreasuryTeam(string role, Position? position, bool expected) =>
        TreasuryAuthorization.CanManageReports(Member(MemberCategory.Tuno, position), Session(role)).Should().Be(expected);

    [Theory]
    [InlineData("Owner", null, true)]
    [InlineData("Admin", null, false)]
    [InlineData("Mod", null, false)]
    [InlineData("", Position.PrimeiroTesoureiro, true)]
    [InlineData("", Position.SegundoTesoureiro, true)]
    [InlineData("Admin", Position.SegundoTesoureiro, true)]
    [InlineData("", null, false)]
    public void CanPublish_IsForOwner_OrTheTreasuryTeam(string role, Position? position, bool expected) =>
        TreasuryAuthorization.CanPublish(Member(MemberCategory.Tuno, position), Session(role)).Should().Be(expected);

    [Fact]
    public void ACaloiroTreasurerWithoutARole_SeesNothing_SoPublishesNothing()
    {
        var caloiro = Member(MemberCategory.Caloiro, Position.PrimeiroTesoureiro);

        TreasuryAuthorization.CanView(caloiro, Session("")).Should().BeFalse();
        TreasuryAuthorization.CanPublish(caloiro, Session("")).Should().BeFalse();
        TreasuryAuthorization.CanManageReports(caloiro, Session("")).Should().BeFalse();
    }

    [Theory]
    [InlineData("", false, false, false)]
    [InlineData("Mod", false, true, false)]
    [InlineData("Admin", true, true, false)]
    [InlineData("Owner", true, true, true)]
    public void RoleOnlyRules_HistoryIsAdminAndOwner_RecordsAreModAndAbove_TransferEditsAreOwner(
        string role, bool history, bool records, bool editTransfers)
    {
        var session = Session(role);

        TreasuryAuthorization.CanSeeHistory(session).Should().Be(history);
        TreasuryAuthorization.CanManageRecords(session).Should().Be(records);
        TreasuryAuthorization.CanEditTransfers(session).Should().Be(editTransfers);
    }

    private static System.Security.Claims.ClaimsPrincipal Session(string role) =>
        role.Length == 0 ? TestPrincipal.SignedIn("member") : TestPrincipal.SignedIn("member", role);

    private static ApplicationUser Member(MemberCategory category, Position? position = null) => new()
    {
        Categories = [category],
        Positions = position is { } seat ? [seat] : [],
    };
}

using FluentAssertions;
using RTUB.Application.Helpers;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// The Ensaios rules (docs/react-rehearsals.md): a Mod creates and edits, Admin and Owner manage, and the current
/// Ensaiador and Magister also approve presenças. "position" is the seat the member holds (null for none).
/// </summary>
public class RehearsalsAuthorizationTests
{
    [Theory]
    [InlineData("", false, false)]
    [InlineData("Mod", true, false)]
    [InlineData("Admin", true, true)]
    [InlineData("Owner", true, true)]
    public void ModEdits_AdminAndOwnerAlsoManage(string role, bool edit, bool manage)
    {
        var session = TestPrincipal.WithRole(role);

        RehearsalsAuthorization.CanEdit(session).Should().Be(edit);
        RehearsalsAuthorization.CanManage(session).Should().Be(manage);
    }

    [Theory]
    [InlineData("", Position.Ensaiador, true)]
    [InlineData("", Position.Magister, true)]
    [InlineData("", Position.ViceMagister, false)]
    [InlineData("", null, false)]
    [InlineData("Mod", null, false)]
    [InlineData("Mod", Position.Ensaiador, true)]
    [InlineData("Admin", null, true)]
    [InlineData("Owner", null, true)]
    public void Presencas_AreApprovedByAdminAndOwner_AndByTheEnsaiadorAndTheMagister(string role, Position? position, bool expected)
    {
        Position[] positions = position is { } seat ? [seat] : [];

        RehearsalsAuthorization.CanApprove(TestPrincipal.WithRole(role), positions).Should().Be(expected);
    }

    [Fact]
    public void CanApprove_WithNoPositionsRead_IsTheRoleRuleAlone()
    {
        RehearsalsAuthorization.CanApprove(TestPrincipal.WithRole(""), positions: null).Should().BeFalse();
        RehearsalsAuthorization.CanApprove(TestPrincipal.WithRole("Admin"), positions: null).Should().BeTrue();
    }

    [Fact]
    public void WithoutASignIn_NothingIsAllowed_NotEvenForAMagister()
    {
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            RehearsalsAuthorization.CanEdit(session).Should().BeFalse();
            RehearsalsAuthorization.CanManage(session).Should().BeFalse();
            RehearsalsAuthorization.CanApprove(session, [Position.Magister, Position.Ensaiador]).Should().BeFalse();
        }
    }
}

using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// The Órgãos Sociais rules (docs/react-governance.md). The RGI rule asks for a system role, "Member" included: a
/// signed-in account with no role at all does not read it.
/// </summary>
public class GovernanceAuthorizationTests
{
    [Theory]
    [InlineData("", false, false)]
    [InlineData("Member", false, true)]
    [InlineData("Mod", true, true)]
    [InlineData("Admin", true, true)]
    [InlineData("Owner", true, true)]
    public void ModAdminAndOwnerManage_AndEveryRoleReadsTheRgi(string role, bool manage, bool viewRgi)
    {
        var session = TestPrincipal.WithRole(role);

        GovernanceAuthorization.CanManage(session).Should().Be(manage);
        GovernanceAuthorization.CanViewRgi(session).Should().Be(viewRgi);
    }

    [Fact]
    public void WithoutASignIn_NothingIsAllowed_WhateverTheClaimsSay()
    {
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            GovernanceAuthorization.CanManage(session).Should().BeFalse();
            GovernanceAuthorization.CanViewRgi(session).Should().BeFalse();
        }
    }
}

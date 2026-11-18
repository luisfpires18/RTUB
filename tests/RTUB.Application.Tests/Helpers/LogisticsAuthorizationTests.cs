using FluentAssertions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Helpers;

/// <summary>The Logística rules (docs/react-logistics.md): who manages the boards, and that an expelled member sees none.</summary>
public class LogisticsAuthorizationTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("Mod", true)]
    [InlineData("Admin", true)]
    [InlineData("Owner", true)]
    public void ModAdminAndOwner_ManageTheBoards(string role, bool expected) =>
        LogisticsAuthorization.CanManage(TestPrincipal.WithRole(role)).Should().Be(expected);

    [Theory]
    [InlineData("", false, true)]
    [InlineData("", true, false)]
    [InlineData("Mod", true, true)]
    [InlineData("Admin", true, true)]
    [InlineData("Owner", true, true)]
    public void EveryMemberSeesTheBoards_AnExpelledOneOnlyWithARoleThatManages(string role, bool expelled, bool expected) =>
        LogisticsAuthorization.CanView(new ApplicationUser { IsExpelled = expelled }, TestPrincipal.WithRole(role)).Should().Be(expected);

    [Fact]
    public void WithoutASignIn_NobodyManages_WhateverTheClaimsSay()
    {
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            LogisticsAuthorization.CanManage(session).Should().BeFalse();
            LogisticsAuthorization.CanView(new ApplicationUser { IsExpelled = true }, session).Should().BeFalse();
        }
    }
}

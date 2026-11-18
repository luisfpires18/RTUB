using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>The Classificação rule (docs/react-leaderboard.md): Admin and Owner delete anyone's comment.</summary>
public class LeaderboardAuthorizationTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("Mod", false)]
    [InlineData("Admin", true)]
    [InlineData("Owner", true)]
    public void AdminAndOwner_Manage_AModDoesNot(string role, bool expected) =>
        LeaderboardAuthorization.CanManage(TestPrincipal.WithRole(role)).Should().Be(expected);

    [Fact]
    public void WithoutASignIn_NobodyManages_WhateverTheClaimsSay()
    {
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            LeaderboardAuthorization.CanManage(session).Should().BeFalse();
        }
    }
}

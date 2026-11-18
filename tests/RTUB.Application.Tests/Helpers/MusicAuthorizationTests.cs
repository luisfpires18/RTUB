using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// The Música rules (docs/react-music.md). "manage" is create and edit albums and songs; "admin" is delete and the
/// per-member statistics; "owner" is the exclusive albums and the Aprender charts.
/// </summary>
public class MusicAuthorizationTests
{
    [Theory]
    [InlineData("", false, false, false)]
    [InlineData("Mod", true, false, false)]
    [InlineData("Admin", true, true, false)]
    [InlineData("Owner", true, true, true)]
    public void ModManages_AdminAndOwnerAlsoDeleteAndSeeDetail_OnlyTheOwnerHasExclusiveAlbumsAndCharts(string role, bool manage, bool admin, bool owner)
    {
        var session = TestPrincipal.WithRole(role);

        MusicAuthorization.CanManage(session).Should().Be(manage);
        MusicAuthorization.IsAdmin(session).Should().Be(admin);
        MusicAuthorization.CanDelete(session).Should().Be(admin);
        MusicAuthorization.CanSeeDetailedStatistics(session).Should().Be(admin);
        MusicAuthorization.IsOwner(session).Should().Be(owner);
        MusicAuthorization.CanManageExclusive(session).Should().Be(owner);
        MusicAuthorization.CanManagePlayAlong(session).Should().Be(owner);
    }

    [Fact]
    public void WithoutASignIn_NothingIsAllowed_WhateverTheClaimsSay()
    {
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            MusicAuthorization.CanManage(session).Should().BeFalse();
            MusicAuthorization.IsAdmin(session).Should().BeFalse();
            MusicAuthorization.CanDelete(session).Should().BeFalse();
            MusicAuthorization.CanSeeDetailedStatistics(session).Should().BeFalse();
            MusicAuthorization.IsOwner(session).Should().BeFalse();
            MusicAuthorization.CanManageExclusive(session).Should().BeFalse();
            MusicAuthorization.CanManagePlayAlong(session).Should().BeFalse();
        }
    }
}

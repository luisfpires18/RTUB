using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>The Novidades rules (docs/react-news.md): a Mod writes and publishes; only Admin and Owner delete.</summary>
public class NewsAuthorizationTests
{
    [Theory]
    [InlineData("", false, false)]
    [InlineData("Mod", true, false)]
    [InlineData("Admin", true, true)]
    [InlineData("Owner", true, true)]
    public void ModWrites_AdminAndOwnerAlsoManage(string role, bool write, bool manage)
    {
        var session = TestPrincipal.WithRole(role);

        NewsAuthorization.CanWrite(session).Should().Be(write);
        NewsAuthorization.CanManage(session).Should().Be(manage);
    }

    [Fact]
    public void WithoutASignIn_NothingIsAllowed_WhateverTheClaimsSay()
    {
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            NewsAuthorization.CanWrite(session).Should().BeFalse();
            NewsAuthorization.CanManage(session).Should().BeFalse();
        }
    }
}

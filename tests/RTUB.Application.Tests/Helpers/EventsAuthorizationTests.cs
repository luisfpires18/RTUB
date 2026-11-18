using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// The Atuações rules (docs/react-events.md) that read only the session. <c>CanTrackContactsAsync</c> also lets any
/// Leitão in by category, read from the database: it is not covered here.
/// </summary>
public class EventsAuthorizationTests
{
    [Theory]
    [InlineData("", false, false, false)]
    [InlineData("Mod", true, false, false)]
    [InlineData("Admin", true, true, false)]
    [InlineData("Owner", true, true, true)]
    public void ModEditsAndTracksContacts_AdminAndOwnerManage_OnlyTheOwnerIsTheOwner(string role, bool edit, bool manage, bool owner)
    {
        var session = TestPrincipal.WithRole(role);

        EventsAuthorization.CanEdit(session).Should().Be(edit);
        EventsAuthorization.CanTrackContacts(session).Should().Be(edit);
        EventsAuthorization.CanManage(session).Should().Be(manage);
        EventsAuthorization.IsOwner(session).Should().Be(owner);
    }

    [Fact]
    public void WithoutASignIn_NothingIsAllowed_WhateverTheClaimsSay()
    {
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            EventsAuthorization.CanEdit(session).Should().BeFalse();
            EventsAuthorization.CanTrackContacts(session).Should().BeFalse();
            EventsAuthorization.CanManage(session).Should().BeFalse();
            EventsAuthorization.IsOwner(session).Should().BeFalse();
        }
    }
}

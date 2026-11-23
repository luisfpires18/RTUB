using FluentAssertions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Tests;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// The /trintuna rules (docs/react-trintuna.md): the Owner manages; a listed member sees, votes and answers; the
/// organizer (while listed) edits their gathering. Admin and Mod are not the Owner here.
/// </summary>
public class TrintunaAuthorizationTests
{
    [Theory]
    [InlineData("", false, false, false)]
    [InlineData("", true, false, true)]
    [InlineData("Mod", false, false, false)]
    [InlineData("Admin", false, false, false)]
    [InlineData("Admin", true, false, true)]
    [InlineData("Owner", false, true, true)]
    [InlineData("Owner", true, true, true)]
    public void OwnerManages_AndOnlyTheOwnerOrAListedMemberSees(string role, bool listed, bool manage, bool view)
    {
        var session = TestPrincipal.WithRole(role);

        TrintunaAuthorization.CanManage(session).Should().Be(manage);
        TrintunaAuthorization.CanView(session, listed).Should().Be(view);
        TrintunaAuthorization.CanAnswer(session, listed).Should().Be(view);
    }

    [Theory]
    [InlineData("", true, "user-", true)]
    [InlineData("", true, "someone-else", false)]
    [InlineData("", true, null, false)]
    [InlineData("", false, "user-", false)]
    [InlineData("Admin", true, "someone-else", false)]
    [InlineData("Owner", false, "someone-else", true)]
    [InlineData("Owner", false, null, true)]
    public void TheListedOrganizerOrTheOwnerEdits(string role, bool listed, string? organizer, bool edit)
    {
        TrintunaAuthorization.CanEdit(TestPrincipal.WithRole(role), listed, organizer).Should().Be(edit);
    }

    [Fact]
    public void WithoutASignIn_NothingIsAllowed_WhateverTheClaimsSay()
    {
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            TrintunaAuthorization.CanManage(session).Should().BeFalse();
            TrintunaAuthorization.CanView(session, true).Should().BeFalse();
            TrintunaAuthorization.CanEdit(session, true, "user-forged").Should().BeFalse();
        }
    }

    [Theory]
    [InlineData("", false, false)]
    [InlineData("", true, true)]
    [InlineData("Admin", false, false)]
    [InlineData("Owner", false, true)]
    public void SessionMenuFlag_IsTheOwnerOrTheList(string role, bool listed, bool trintuna)
    {
        var session = TestPrincipal.WithRole(role);

        MemberMenuAccess.For(session, new ApplicationUser(), listed).Trintuna.Should().Be(trintuna);
        MemberMenuAccess.For(session, new ApplicationUser()).Trintuna.Should().Be(role == "Owner");
    }
}

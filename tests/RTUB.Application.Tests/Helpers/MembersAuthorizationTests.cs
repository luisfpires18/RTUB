using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// The Membros rules (docs/react-members.md): a Mod edits the limited member information, Admin and Owner manage
/// members, only the Owner deletes any member.
/// </summary>
public class MembersAuthorizationTests
{
    [Theory]
    [InlineData("", false, false, false)]
    [InlineData("Mod", true, false, false)]
    [InlineData("Admin", true, true, false)]
    [InlineData("Owner", true, true, true)]
    public void ModEditsInfo_AdminAndOwnerManage_OnlyTheOwnerDeletesAnyMember(string role, bool editInfo, bool manage, bool deleteAny)
    {
        var session = TestPrincipal.WithRole(role);

        MembersAuthorization.CanEditInfo(session).Should().Be(editInfo);
        MembersAuthorization.CanManage(session).Should().Be(manage);
        MembersAuthorization.CanDeleteAnyMember(session).Should().Be(deleteAny);
    }

    [Fact]
    public void WithoutASignIn_NothingIsAllowed_WhateverTheClaimsSay()
    {
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            MembersAuthorization.CanEditInfo(session).Should().BeFalse();
            MembersAuthorization.CanManage(session).Should().BeFalse();
            MembersAuthorization.CanDeleteAnyMember(session).Should().BeFalse();
        }
    }
}

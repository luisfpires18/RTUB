using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>Every area asks these two questions of the session first, so every permission rests on them.</summary>
public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void Visitor_IsNotAMember_AndHasNoUserId()
    {
        var visitor = TestPrincipal.Visitor();

        visitor.IsMember().Should().BeFalse();
        visitor.UserId().Should().BeNull();
    }

    [Fact]
    public void SignedInMember_IsAMember_WithTheirUserId()
    {
        var member = TestPrincipal.SignedIn("user-1");

        member.IsMember().Should().BeTrue();
        member.UserId().Should().Be("user-1");
    }

    [Fact]
    public void ClaimsWithoutASignIn_AreNotTrusted()
    {
        var forged = TestPrincipal.NotAuthenticated("user-1", "Owner");

        forged.IsMember().Should().BeFalse();
        forged.UserId().Should().BeNull("the id claim counts only on an authenticated identity");
    }
}

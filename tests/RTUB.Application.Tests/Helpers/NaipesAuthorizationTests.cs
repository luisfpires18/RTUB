using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>The Naipes rules (docs/react-naipes.md): members edit what they added; Admin and Owner edit everything.</summary>
public class NaipesAuthorizationTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("Mod", false)]
    [InlineData("Admin", true)]
    [InlineData("Owner", true)]
    public void AdminAndOwner_ManageEverything_AModHasNoExtraRight(string role, bool expected)
    {
        var session = TestPrincipal.WithRole(role);

        NaipesAuthorization.CanManage(session).Should().Be(expected);
        NaipesAuthorization.CanEdit(session, createdByUserId: "someone-else").Should().Be(expected);
    }

    [Fact]
    public void AMember_EditsWhatTheyAdded_AndNobodyElses()
    {
        var author = TestPrincipal.SignedIn("author");

        NaipesAuthorization.CanEdit(author, "author").Should().BeTrue();
        NaipesAuthorization.CanEdit(author, "someone-else").Should().BeFalse();
    }

    [Fact]
    public void WithoutASignIn_NothingIsEdited_NotEvenWithTheAuthorsId()
    {
        NaipesAuthorization.CanEdit(TestPrincipal.NotAuthenticated("author"), "author").Should().BeFalse();
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            NaipesAuthorization.CanManage(session).Should().BeFalse();
            NaipesAuthorization.CanEdit(session, "author").Should().BeFalse();
        }
    }
}

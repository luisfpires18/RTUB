using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>The Galeria rules (docs/react-gallery.md): the uploader, Admin and Owner edit and delete an item.</summary>
public class GalleryAuthorizationTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("Mod", false)]
    [InlineData("Admin", true)]
    [InlineData("Owner", true)]
    public void AdminAndOwner_EditEveryItem_AModHasNoExtraRight(string role, bool expected)
    {
        var session = TestPrincipal.WithRole(role);

        GalleryAuthorization.CanManageAll(session).Should().Be(expected);
        GalleryAuthorization.CanEdit(session, uploaderId: "someone-else").Should().Be(expected);
    }

    [Fact]
    public void TheUploader_EditsTheirOwnItem_AndNobodyElses()
    {
        var uploader = TestPrincipal.SignedIn("uploader");

        GalleryAuthorization.CanEdit(uploader, "uploader").Should().BeTrue();
        GalleryAuthorization.CanEdit(uploader, "someone-else").Should().BeFalse();
    }

    [Fact]
    public void WithoutASignIn_NothingIsEdited_NotEvenWithTheUploadersId()
    {
        GalleryAuthorization.CanEdit(TestPrincipal.NotAuthenticated("uploader"), "uploader").Should().BeFalse();
        foreach (var session in TestPrincipal.NotSignedIn())
        {
            GalleryAuthorization.CanManageAll(session).Should().BeFalse();
            GalleryAuthorization.CanEdit(session, "uploader").Should().BeFalse();
        }
    }
}

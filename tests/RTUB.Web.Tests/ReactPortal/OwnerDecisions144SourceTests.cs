using FluentAssertions;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>Task 144 small fixes, pinned at the source: no JS runner, and these are one-line contracts.</summary>
public class OwnerDecisions144SourceTests
{
    private static readonly string Web = Path.Combine(RepoPaths.Root(), "src", "RTUB.Web");
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(parts));

    [Theory]
    [InlineData("PushEndpoints.cs")]
    [InlineData("ReleaseEndpoints.cs")]
    public void MemberOnlyGroups_AnswerA401WithAProblemBody_NeverAnEmptyOne(string file)
    {
        Read(Web, "Endpoints", file).Should().NotContain("Results.Unauthorized()").And.Contain("EndpointResults.SignInRequired()");
    }

    [Fact]
    public void PublicGalleryGets_RunUnderTheNoStoreFilter()
    {
        Read(Web, "Endpoints", "GalleryEndpoints.cs").Should()
            .Contain("app.MapGroup(\"/api/gallery\").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);")
            .And.NotContain("NoStore(context)");
    }

    [Fact]
    public void RolesPage_AsksForManagementOnlyWhenTheSessionSaysSo()
    {
        var page = Read(Web, "portal", "src", "pages", "Governance.tsx");
        page.Should().Contain("user.menu.governance").And.Contain("if (!mayManage) return setManage(null);")
            .And.Contain("[mayManage, year]");
        Read(Web, "portal", "src", "data", "apiSession.ts").Should().Contain("governance: boolean");
    }

    [Fact]
    public void HtmlThemeColor_MatchesTheManifest()
    {
        Read(Web, "wwwroot", "manifest.webmanifest").Should().Contain("\"theme_color\": \"#3F2A86\"").And.Contain("\"background_color\": \"#000000\"");
        Read(Web, "portal", "index.html").Should().Contain("name=\"theme-color\" content=\"#3F2A86\"")
            .And.NotContain("name=\"theme-color\" content=\"#000000\"");
    }
}

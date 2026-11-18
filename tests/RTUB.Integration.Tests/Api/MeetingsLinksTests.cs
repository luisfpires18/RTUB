using FluentAssertions;

namespace RTUB.Integration.Tests.Api;
/// <summary>The menus link the React Meetings page. What is gone is in RetiredRoutesTests.</summary>
public class MeetingsLinksTests
{

    [Fact]
    public void TheReactPage_IsLinked_AndSaysNoMigration()
    {
        var portal = Path.Combine(RepoPaths.Root(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(portal, "data/content.ts")).Should().Contain("meetings: '/meetings'");
        File.ReadAllText(Path.Combine(portal, "components/MemberShell.tsx")).Should().Contain("href: portal.meetings,").And.NotContain("blazor.meetings");
        File.ReadAllText(Path.Combine(portal, "main.tsx")).Should().Contain("'/meetings': lazy(");

        // Every file of the feature is checked.
        var files = PortalSource.Files(portal, "pages/Meetings.tsx", "components/Meeting*.tsx", "data/meetingsApi.ts", "data/meeting*.ts");
        foreach (var file in files.Distinct())
        {
            File.ReadAllText(file).Should().NotContainAny(new[] { "migra", "Migra" }, file);
        }
    }
}

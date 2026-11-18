using FluentAssertions;

namespace RTUB.Integration.Tests.Api;
/// <summary>The menus link the React Meetings page. What is gone is in RetiredRoutesTests.</summary>
public class MeetingsLinksTests
{

    [Fact]
    public void TheReactPage_IsLinked_AndSaysNoMigration()
    {
        var portal = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
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

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "RTUB.Web")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}

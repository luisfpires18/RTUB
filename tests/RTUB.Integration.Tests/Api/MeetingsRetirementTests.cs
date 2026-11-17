// MeetingsRetirementTests: test class moved here from MeetingsApiTests.cs.
using FluentAssertions;

namespace RTUB.Integration.Tests.Api;
/// <summary>034: the Blazor Meetings page and what only it used are gone; the menus link the React page.</summary>
public class MeetingsRetirementTests
{
    [Fact]
    public void TheBlazorPage_AndWhatOnlyItUsed_AreGone()
    {
        var src = Path.Combine(RepoRoot(), "src");
        foreach (var gone in new[]
                 {
                     "RTUB.Web/Pages/Activities/Meetings.razor", "RTUB.Shared/Components/Cards/MeetingCard.razor",
                     "RTUB.Shared/Components/Cards/MeetingRequestCard.razor", "RTUB.Shared/Components/Modals/MeetingParticipationModal.razor",
                     "RTUB.Web/wwwroot/css/3-components/meeting-card.css",
                 })
        {
            File.Exists(Path.Combine(src, gone)).Should().BeFalse("{0} was retired in 034", gone);
        }

        // 037 retired the Blazor host, its stylesheets and its navbar with it (BlazorHostRetirementTests).

        typeof(RTUB.Program).Assembly.GetType("RTUB.Pages.Activities.Meetings").Should().BeNull();
    }

    [Fact]
    public void TheReactPage_IsLinked_AndSaysNoMigration()
    {
        var portal = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(portal, "data/content.ts")).Should().Contain("meetings: '/meetings'");
        File.ReadAllText(Path.Combine(portal, "components/MemberShell.tsx")).Should().Contain("href: portal.meetings,").And.NotContain("blazor.meetings");
        File.ReadAllText(Path.Combine(portal, "main.tsx")).Should().Contain("'/meetings': lazy(");

        // MeetingDialogs.tsx, MeetingAta.tsx and meetingsApi.ts were split (113): every file of the feature is checked.
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

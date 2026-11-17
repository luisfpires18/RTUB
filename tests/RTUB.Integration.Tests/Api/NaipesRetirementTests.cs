using FluentAssertions;

namespace RTUB.Integration.Tests.Api;

/// <summary>033: the Blazor Naipes pages and what only they used are gone; the menus link the React pages.</summary>
public class NaipesRetirementTests
{
    [Fact]
    public void BlazorPages_AndWhatOnlyTheyUsed_AreGone()
    {
        var src = Path.Combine(RepoRoot(), "src");
        foreach (var gone in new[]
                 {
                     "RTUB.Web/Pages/Activities/Naipes.razor", "RTUB.Web/Pages/Activities/NaipesConfig.razor",
                     "RTUB.Shared/Components/Cards/NaipeCard.razor", "RTUB.Shared/Components/Naipes/NaipeCommentItem.razor",
                     "RTUB.Shared/Components/Modals/DetailsModal.razor", "RTUB.Shared/Components/Modals/InfoSection.razor",
                     "RTUB.Shared/Components/Profile/ProfileField.razor", "RTUB.Web/wwwroot/css/3-components/naipe-card.css",
                     "RTUB.Web/wwwroot/css/3-components/details-modal.css", "RTUB.Web/wwwroot/css/4-pages/naipes-config.css",
                 })
        {
            File.Exists(Path.Combine(src, gone)).Should().BeFalse("{0} was retired in 033", gone);
        }

        var web = typeof(RTUB.Program).Assembly;
        web.GetType("RTUB.Pages.Activities.Naipes").Should().BeNull();
        web.GetType("RTUB.Pages.Activities.NaipesConfig").Should().BeNull();
    }

    [Fact]
    public void ReactPages_AreLinked_AndSayNoMigration()
    {
        var portal = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(portal, "data/content.ts")).Should().Contain("naipes: '/naipes'").And.Contain("naipesConfig: '/naipes/config'");
        var shell = File.ReadAllText(Path.Combine(portal, "components/MemberShell.tsx"));
        shell.Should().Contain("href: portal.naipes,").And.NotContain("blazor.naipes");
        var main = File.ReadAllText(Path.Combine(portal, "main.tsx"));
        main.Should().Contain("'/naipes': lazy(").And.Contain("'/naipes/config': lazy(");
        File.ReadAllText(Path.Combine(portal, "pages/Naipes.tsx")).Should().Contain("href={portal.naipesConfig}");

        foreach (var file in new[] { "pages/Naipes.tsx", "pages/NaipesConfig.tsx", "data/naipesApi.ts" })
        {
            File.ReadAllText(Path.Combine(portal, file)).Should().NotContainAny(new[] { "migra", "Migra" }, file);
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

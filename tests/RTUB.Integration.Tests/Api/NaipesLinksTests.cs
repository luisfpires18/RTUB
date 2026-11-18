using FluentAssertions;

namespace RTUB.Integration.Tests.Api;

/// <summary>The menus link the React Naipes pages. What is gone is in RetiredRoutesTests.</summary>
public class NaipesLinksTests
{

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

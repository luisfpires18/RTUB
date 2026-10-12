using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 035 (docs/react-owner-operations.md): /emails, /notifications, /users, /owner/tracing and /owner/db are React
/// pages over an Owner-only /api/owner; their Blazor pages and page-only components are gone, and the Blazor navbar
/// shows them to the Owner only, as full navigations. Who gets in is covered by OwnerOperationsApiTests.
/// </summary>
public class OwnerOperationsSourceTests
{
    private static readonly string Root = GetProjectRoot();
    private static readonly string Web = Path.Combine(Root, "src", "RTUB.Web");
    private static readonly string Portal = Path.Combine(Web, "portal", "src");

    private static readonly string[] Routes = ["/emails", "/notifications", "/users", "/owner/tracing", "/owner/db"];

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(path)).Replace("\r\n", "\n");

    [Theory]
    [InlineData("src/RTUB.Web/Pages/Operations/Emails.razor")]
    [InlineData("src/RTUB.Web/Pages/Operations/Notifications.razor")]
    [InlineData("src/RTUB.Web/Pages/Operations/AuditLog.razor")]
    [InlineData("src/RTUB.Web/Pages/Operations/DatabaseViewer.razor")]
    [InlineData("src/RTUB.Web/Pages/Management/UserRoles.razor")]
    [InlineData("src/RTUB.Shared/Components/Email/EmailRecipientsPreview.razor")]
    [InlineData("src/RTUB.Shared/Components/Cards/UserCard.razor")]
    [InlineData("src/RTUB.Shared/Components/Cards/TracingCard.razor")]
    [InlineData("src/RTUB.Shared/Components/UI/LoginStatisticsButton.razor")]
    [InlineData("src/RTUB.Shared/Components/UI/SyncMemberStatusButton.razor")]
    [InlineData("src/RTUB.Web/wwwroot/css/3-components/user-card.css")]
    [InlineData("src/RTUB.Web/wwwroot/css/3-components/tracing-card.css")]
    public void TheBlazorPagesAndTheirOwnComponents_AreGone(string file)
    {
        File.Exists(Path.Combine(Root, file)).Should().BeFalse("{0} was retired by task 035", file);
    }

    [Fact]
    public void NoBlazorPage_DeclaresAnOwnerRoute()
    {
        var routes = Directory.GetFiles(Web, "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"@page ""([^""]+)""").Select(m => m.Groups[1].Value));

        routes.Should().NotIntersectWith(Routes);
    }

    [Fact]
    public void TheReactPortal_MapsTheFiveOwnerPages()
    {
        var main = Read(Portal, "main.tsx");
        var program = Read(Web, "Program.cs");

        foreach (var (route, page) in new[]
                 {
                     ("/emails", "OwnerEmails"), ("/notifications", "OwnerNotifications"), ("/users", "OwnerUsers"),
                     ("/owner/tracing", "OwnerTracing"), ("/owner/db", "OwnerDatabase"),
                 })
        {
            main.Should().Contain($"'{route}': lazy(() => import('./{page}')),");
            File.Exists(Path.Combine(Portal, page + ".tsx")).Should().BeTrue();
        }

        program.Should().Contain("app.MapOwnerEndpoints();")
            .And.Contain("new[] { \"/emails\", \"/notifications\", \"/users\", \"/owner/tracing\", \"/owner/db\" }");
    }

    [Fact]
    public void EveryOwnerCall_GoesThroughTheOwnerGate_BeforeTheAntiforgeryCheck()
    {
        var endpoints = Read(Web, "Endpoints", "OwnerEndpoints.cs");

        endpoints.Should().Contain("var owner = app.MapGroup(\"/api/owner\").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore).AddEndpointFilter(RequireOwner);")
            .And.Contain("var writes = owner.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);")
            .And.Contain("if (!user.IsInRole(\"Owner\"))");
        Regex.Matches(endpoints, @"\bapp\.Map(Get|Post|Put|Delete)\(").Should().BeEmpty("every route hangs off the gated group");
        Regex.Matches(endpoints, @"\bowner\.Map(Post|Put|Delete)\(").Should().BeEmpty("every write needs the antiforgery token");
    }

    [Fact]
    public void TheBlazorNavbar_ShowsTheOwnerTools_ToTheOwnerOnly_AsFullNavigations()
    {
        var layout = Read(Web, "Shared", "MainLayout.razor");
        var dropdown = Regex.Match(layout, @"<AuthorizeView Roles=""Owner"">\s*<Authorized Context=""ownerAuth"">.*?</AuthorizeView>", RegexOptions.Singleline).Value;

        dropdown.Should().NotBeEmpty();
        foreach (var route in Routes)
        {
            dropdown.Should().Contain($"href=\"{route}\" data-enhance-nav=\"false\"");
            layout.Should().NotContain($"<NavLink class=\"dropdown-item\" href=\"{route}\"");
        }

        layout.Should().NotContain("<AuthorizeView Roles=\"Admin\">", "Emails and Notificações are no longer Admin pages");
    }

    [Fact]
    public void TheOwnerPages_SayNothingAboutTheMove_AndLinkNoRemovedModule()
    {
        foreach (var file in Directory.GetFiles(Portal, "Owner*.tsx").Append(Path.Combine(Portal, "ownerApi.ts")))
        {
            var text = File.ReadAllText(file);
            var visible = Regex.Replace(text, @"/\*\*.*?\*/|//[^\n]*", string.Empty, RegexOptions.Singleline);
            visible.Should().NotContainEquivalentOf("migra", "{0} is app-facing", Path.GetFileName(file));
            visible.Should().NotContainEquivalentOf("blazor");
            foreach (var removed in new[] { "/games", "/bets", "/my-tuno", "/messages", "'/images'", "/labels" })
            {
                text.Should().NotContain(removed);
            }
        }
    }

    private static string GetProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find project root directory");
    }
}

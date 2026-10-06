using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 030: the React member shell. Visitors keep the public header (five sections, "Pedir atuação", Login); signed-in
/// members get no public call to action, their identity, and one member menu - a collapsible rail on wide screens and a
/// drawer on phones (MemberShell.tsx). The menu lists only live routes, never the retired modules, and the server decides
/// which groups a member sees (MemberMenuAccess; its rules are covered by AccountEndpointTests).
/// </summary>
public class MemberShellTests
{
    private static readonly string Root = GetProjectRoot();
    private static readonly string Src = Path.Combine(Root, "src", "RTUB.Web", "portal", "src");

    private static string Read(string file) => File.ReadAllText(Path.Combine(Src, file)).Replace("\r\n", "\n");

    private static string Function(string file, string name) =>
        Regex.Match(Read(file), $@"\nfunction {name}\(.*?\n\}}\n", RegexOptions.Singleline).Value;

    [Fact]
    public void Header_ShowsVisitorsPedirAtuacaoAndLogin_AndMembersTheirIdentityAndMenu()
    {
        var header = Function("App.tsx", "Header");
        header.Should().NotBeEmpty();

        // {shell ? ( member branch ) : ( visitor branch )}
        var split = Regex.Match(header, @"\{shell \? \((.*?)\) : \((.*?)\)\}", RegexOptions.Singleline);
        split.Success.Should().BeTrue("the header renders one branch for members and one for visitors");
        var member = split.Groups[1].Value;
        var visitor = split.Groups[2].Value;

        visitor.Should().Contain("Pedir atuação").And.Contain("<LoginLink").And.Contain("<MobileMenu");
        member.Should().Contain("<MemberIdentity").And.Contain("<MemberDrawer")
            .And.NotContain("Pedir atuação").And.NotContain("LoginLink").And.NotContain("MobileMenu");
        header.Should().Contain("{!shell && (", "the public section nav is the visitors' header only")
            .And.NotContain("AccountLink", "the header never says Membros or A minha conta");

        Function("App.tsx", "LoginLink").Should().Contain("href={portal.login}").And.Contain(">Login<");
        Function("App.tsx", "MobileMenu").Should().Contain("<LoginLink").And.Contain("Pedir uma atuação").And.NotContain("AccountLink");
    }

    [Fact]
    public void Layout_ShowsTheRailOnlyInTheMemberShell()
    {
        var layout = Regex.Match(Read("App.tsx"), @"export function Layout\(.*?\n\}\n", RegexOptions.Singleline).Value;

        layout.Should().Contain("{shell && <MemberRail");
        Read("App.tsx").Should().Contain("if (user?.authenticated) return { kind: 'member', user };")
            .And.Contain("if (user || failed || !hint) return { kind: 'visitor' };", "a visitor never waits for the session");
        Read("api.ts").Should().Contain("rememberMemberShell(false);\n    const form = document.createElement('form');",
            "signing out drops the layout hint before leaving");
    }

    [Theory]
    [InlineData("portal.profile")]
    [InlineData("portal.events")]
    [InlineData("portal.myEnrollments")]
    [InlineData("portal.rehearsals")]
    [InlineData("portal.music")]
    [InlineData("portal.gallery")]
    [InlineData("portal.news")]
    [InlineData("portal.members")]
    [InlineData("portal.membersHierarchy")]
    [InlineData("portal.membersMap")]
    [InlineData("portal.roles")]
    [InlineData("portal.leaderboard")]
    [InlineData("portal.inventory")]
    [InlineData("portal.shop")]
    [InlineData("portal.documentation")]
    [InlineData("portal.logistics")]
    [InlineData("portal.treasury")]
    [InlineData("portal.requests")]
    [InlineData("portal.questions")]
    [InlineData("portal.hallOfFame")]
    [InlineData("portal.naipes")]
    [InlineData("portal.meetings")]
    [InlineData("blazor.emails")]
    [InlineData("blazor.notifications")]
    [InlineData("blazor.users")]
    [InlineData("blazor.tracing")]
    [InlineData("blazor.database")]
    public void Menu_ListsEachActiveRoute(string href)
    {
        MenuLinks().Select(l => l.Href).Should().Contain(href);
    }

    [Fact]
    public void Menu_NeverListsARetiredModule()
    {
        var shell = Read("MemberShell.tsx");
        var menu = string.Join("\n", MenuLinks().Select(l => l.Line));

        foreach (var label in new[] { "Jogos", "Mensagens", "Imagens", "Conteúdo" })
        {
            menu.Should().NotContain($"'{label}'", "{0} was removed (026 / 027 / 029A)", label);
        }

        foreach (var url in new[] { "/games", "/bets", "/my-tuno", "/messages", "'/images'", "/labels" })
        {
            shell.Should().NotContain(url);
            Read("App.tsx").Should().NotContain(url);
        }
    }

    [Fact]
    public void Menu_EveryLink_OpensALiveRoute()
    {
        var content = Read("content.ts");
        var main = Read("main.tsx");
        var blazor = Regex.Match(Read("MemberShell.tsx"), @"const blazor = \{(.*?)\} as const;", RegexOptions.Singleline).Groups[1].Value;
        var pages = Directory.GetFiles(Path.Combine(Root, "src", "RTUB.Web", "Pages"), "*.razor", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .SelectMany(text => Regex.Matches(text, @"@page ""([^""]+)""").Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (href, _) in MenuLinks())
        {
            var key = href.Split('.')[1];
            if (href.StartsWith("portal.", StringComparison.Ordinal))
            {
                var path = Regex.Match(content, $@"\n  {key}: '([^']+)',").Groups[1].Value;
                path.Should().NotBeEmpty("{0} is in content.ts", href);
                main.Should().Contain($"'{path}': lazy(", "{0} ({1}) is a React page the server maps", href, path);
            }
            else
            {
                var path = Regex.Match(blazor, $@"\n  {key}: '([^']+)',").Groups[1].Value;
                pages.Should().Contain(path, "{0} ({1}) is a live Blazor page", href, path);
            }
        }
    }

    [Theory]
    [InlineData("blazor.users", "owner")]
    [InlineData("blazor.tracing", "owner")]
    [InlineData("blazor.database", "owner")]
    [InlineData("portal.requests", "admin")]
    [InlineData("blazor.emails", "admin")]
    [InlineData("blazor.notifications", "admin")]
    [InlineData("portal.logistics", "logisticsAndTreasury")]
    [InlineData("portal.treasury", "logisticsAndTreasury")]
    [InlineData("portal.documentation", "management")]
    [InlineData("portal.meetings", "management")]
    [InlineData("portal.questions", "management")]
    [InlineData("portal.events", null)]
    [InlineData("portal.members", null)]
    [InlineData("portal.leaderboard", null)]
    [InlineData("portal.membersMap", null)]
    [InlineData("portal.hallOfFame", null)]
    [InlineData("portal.naipes", null)]
    [InlineData("portal.profile", null)]
    public void Menu_GatesEachRestrictedLinkOnTheServersMenuFlag(string href, string? flag)
    {
        var line = MenuLinks().Single(l => l.Href == href).Line;

        if (flag is null)
        {
            line.Should().NotContain("when:", "{0} is for every member", href);
        }
        else
        {
            line.Should().Contain($"when: '{flag}'");
            Read("api.ts").Should().Contain($"{flag}: boolean", "the flag comes from /api/account/me");
        }
    }

    [Fact]
    public void Drawer_IsAModalDialog_ThatClosesOnEveryChoice_AndRespectsTheSafeArea()
    {
        var drawer = Regex.Match(Read("MemberShell.tsx"), @"export function MemberDrawer\(.*?\n\}\n", RegexOptions.Singleline).Value;
        drawer.Should().Contain("showModal()").And.Contain("<MemberNav member={member} onNavigate={hide} />")
            .And.Contain("if (e.target === e.currentTarget) hide();", "a tap on the backdrop closes it");

        var css = Read("styles.css");
        Regex.Match(css, @"\.drawer__panel \{[^}]*\}").Value.Should().Contain("env(safe-area-inset-bottom)").And.Contain("env(safe-area-inset-top)");
        Regex.Match(css, @"\.drawer \.mnav__link \{[^}]*\}").Value.Should().Contain("min-height: 48px", "thumb-sized targets on phones");
        Regex.Match(css, @"\n\.rail \{[^}]*\}").Value.Should().Contain("display: none", "phones get the drawer, not the rail");
        css.Should().Contain("@keyframes drawer-in");
        Regex.Match(css, @"@media \(prefers-reduced-motion: no-preference\) \{.*?@keyframes drawer-in", RegexOptions.Singleline).Success
            .Should().BeTrue("the drawer only animates when motion is welcome");
    }

    private static List<(string Href, string Line)> MenuLinks() =>
        Regex.Matches(Read("MemberShell.tsx"), @"\{ href: ((?:portal|blazor)\.\w+), label: '[^']+'.*?\}")
            .Select(m => (m.Groups[1].Value, m.Value))
            .ToList();

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

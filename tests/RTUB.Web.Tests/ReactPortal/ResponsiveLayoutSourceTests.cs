using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// UI / PWA batch #229 + #224 + #225: no page scrolls sideways on a phone, fixed at the element responsible and never
/// masked globally; Órgãos Sociais and Galeria / Música use the shared page head; the leaderboard levels open by
/// default. The rendered check is tests/responsive/responsive_audit.py (Playwright, run against a local instance).
/// </summary>
public class ResponsiveLayoutSourceTests
{
    private static readonly string Root = GetProjectRoot();
    private static readonly string Portal = Path.Combine(Root, "src", "RTUB.Web", "portal");

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine([Portal, .. path])).Replace("\r\n", "\n");

    private static string Rule(string css, string selector) =>
        Regex.Match(css, @"\n" + Regex.Escape(selector) + @" \{[^}]*\}").Value;

    [Fact]
    public void HorizontalOverflow_IsNeverMaskedGlobally()
    {
        var css = Read("src", "styles.css");
        foreach (var root in new[] { "html", "body", "main", ".shell", ".shell__body" })
        {
            Rule(css, root).Should().NotMatchRegex(@"overflow(-x)?:\s*(hidden|clip)",
                $"{root} must not hide sideways overflow: fix the element that overflows (#229)");
        }
    }

    [Fact]
    public void HiddenElements_CannotBeWidenedByFieldRules()
    {
        // `.form__field input { width: 100% }` is more specific than `.sr-only`: the hidden file inputs of the image
        // pickers took the whole width and scrolled "Adicionar atuação" sideways.
        var srOnly = Rule(Read("src", "styles.css"), ".sr-only");
        srOnly.Should().Contain("width: 1px !important").And.Contain("min-height: 0 !important").And.Contain("padding: 0 !important");
    }

    [Fact]
    public void MemberHeader_FitsAPhone_AndPadsForTheStatusBar()
    {
        var css = Read("src", "styles.css");
        css.Should().Contain("--safe-top: env(safe-area-inset-top, 0px);").And.Contain("--header-h: calc(68px + var(--safe-top));");
        Rule(css, ".header").Should().Contain("padding-top: var(--safe-top);");
        css.Should().Contain("@media (max-width: 359px) {\n  .shell--member .brand__text {\n    display: none;",
            "at 320px the inbox, category, profile and menu controls overflowed by 40px");
        Regex.Match(css, @"\.header__category \.member-badge \{[^}]*\}").Value.Should().Contain("text-overflow: ellipsis");
    }

    [Fact]
    public void Governance_UsesTheSharedHead_AndOffersAYearOnlyWhenOneIsMissing()
    {
        var page = Read("src", "Governance.tsx");
        page.Should().Contain("<header className=\"page__head events-page__head\">");
        page.Should().Contain("const canAddYear = !!manage && manage.availableStartYears.length > 0;",
            "the server's list of start years still missing decides (#224), not a client rule");
        page.Should().Contain("{canAddYear && (");
        Read("src", "GovernanceManage.tsx").Should().Contain("Não falta criar nenhum ano letivo", "the dialog keeps its defensive message");

        foreach (var file in new[] { "Gallery.tsx", "Music.tsx" })
        {
            Read("src", file).Should().Contain("<header className=\"page__head events-page__head\">",
                $"{file}: a local 1fr auto grid inside .page__head's 44rem cap floated the actions mid-page");
        }

        Read("src", "styles.css").Should().NotContain(".governance-page__head").And.NotContain(".gallery-page__head").And.NotContain(".music__head");
    }

    [Fact]
    public void LeaderboardLevels_OpenByDefault_InABalancedGrid()
    {
        var page = Read("src", "Leaderboard.tsx");
        page.Should().Contain("<details className=\"leaderboard-levels\" open>");
        page.Should().Contain("me: Entry | null").And.Contain("l.level === me.level ? ' is-current'");

        var css = Read("src", "styles.css");
        Rule(css, ".leaderboard-level-list").Should().Contain("grid-template-columns: minmax(0, 1fr);").And.NotContain("auto-fill",
            "auto-fill made the accidental 8 + 4 grid (#225)");
        css.Should().Contain("grid-template-columns: repeat(4, minmax(0, 1fr));");
    }

    [Fact]
    public void ResponsiveAudit_CoversTheBatchScreens()
    {
        var audit = File.ReadAllText(Path.Combine(Root, "tests", "responsive", "responsive_audit.py"));
        audit.Should().Contain("PHONES = [320, 360, 375, 390, 414]");
        foreach (var scenario in new[] { "\"events-add\"", "\"roles\"", "\"roles-add-year\"", "\"leaderboard\"", "\"profile\"", "\"messages\"", "\"shop\"", "\"login\"" })
        {
            audit.Should().Contain($"Scenario({scenario}");
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

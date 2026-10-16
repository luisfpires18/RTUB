using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// #223: /members/hierarchy is one semantic WAI-ARIA tree drawn with CSS only - no graph or layout library. The
/// browser behaviour (layout at 320-1280 px, keyboard, search, "Focar ramo") is covered by the responsive audit
/// (tests/responsive/responsive_audit.py, hierarchy-* scenarios); these tests pin the contract in the source.
/// </summary>
public class MembersHierarchySourceTests
{
    private static readonly string Root = GetProjectRoot();
    private static readonly string Web = Path.Combine(Root, "src", "RTUB.Web");

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine([Web, .. path])).Replace("\r\n", "\n");

    private static readonly string Tree = Read("portal", "src", "MembersHierarchy.tsx");

    [Fact]
    public void NoGraphOrLayoutDependency_WasAdded()
    {
        var package = Read("package.json");
        foreach (var library in new[] { "xyflow", "reactflow", "react-flow", "\"d3", "d3-", "react-d3-tree", "cytoscape", "vis-network", "elkjs", "dagre" })
        {
            package.Should().NotContain(library);
            Tree.Should().NotContain($"from '{library.Trim('"')}");
        }

        Regex.Matches(Tree, @"^import .* from '([^']+)';", RegexOptions.Multiline).Select(m => m.Groups[1].Value)
            .Should().OnlyContain(m => m == "react" || m.StartsWith("./"), "only React and the portal's own modules");
    }

    [Fact]
    public void TheTree_IsOneWaiAriaTree_WithLevelsSetSizesAndARovingTabStop()
    {
        Tree.Should().Contain("role=\"tree\"").And.Contain("role=\"treeitem\"").And.Contain("role=\"group\"")
            .And.Contain("aria-level={level}").And.Contain("aria-setsize={size}").And.Contain("aria-posinset={pos}")
            .And.Contain("aria-expanded={count > 0 ? open : undefined}").And.Contain("aria-selected=")
            .And.Contain("tabIndex={ctx.active === node.id ? 0 : -1}", "one tab stop: the active member");
        Tree.Should().Contain("aria-labelledby={`hname-${node.id}`}", "the member's name, not the buttons, names the treeitem");
        Regex.Matches(Tree, "role=\"tree\"").Count.Should().Be(1, "the same DOM for every width, no hidden duplicate tree");
    }

    [Theory]
    [InlineData("ArrowDown")]
    [InlineData("ArrowUp")]
    [InlineData("ArrowRight")]
    [InlineData("ArrowLeft")]
    [InlineData("Home")]
    [InlineData("End")]
    [InlineData("Enter")]
    public void TheKeyboard_FollowsTheTreeViewPattern(string key)
    {
        Tree.Should().Contain($"case '{key}':");
        Tree.Should().Contain("Type-ahead");
    }

    [Fact]
    public void MemberNavigation_StaysTheMembersPage_AndIsSeparateFromTheExpandButton()
    {
        Tree.Should().Contain("const memberHref = (id: string) => `/members?member=${encodeURIComponent(id)}`;");
        Tree.Should().Contain("window.location.assign(memberHref(node.id));", "Enter opens the member");
        var branch = Tree[Tree.IndexOf("function Branch(", StringComparison.Ordinal)..];
        branch.Should().Contain("<a className=\"hnode__card\" href={memberHref(node.id)} tabIndex={-1}>")
            .And.Contain("className=\"hnode__toggle\"").And.Contain("tabIndex={-1}")
            .And.Contain("aria-label={`${open ? 'Recolher' : 'Mostrar'}");
        branch.IndexOf("</a>", StringComparison.Ordinal).Should().BeLessThan(branch.IndexOf("hnode__toggle", StringComparison.Ordinal),
            "the expand button is not inside the member link");
    }

    [Fact]
    public void SearchFocusAndPath_ExpandAncestorsSelectAndOfferAWayBack()
    {
        Tree.Should().Contain("setExpanded((prev) => new Set([...prev, ...above]));", "choosing a member opens the way to it")
            .And.Contain("moveTo(id);")
            .And.Contain("fold(n.displayName).includes(q) || fold(n.fullName).includes(q)", "nickname or full name, accent-insensitive")
            .And.Contain("Expandir tudo").And.Contain("Recolher tudo")
            .And.Contain("Focar ramo").And.Contain("Ver toda a hierarquia")
            .And.Contain("aria-label=\"Caminho até ao padrinho\"");
        Tree.Should().Contain("Sem ligações de padrinho", "members with no links are grouped, not shown as empty trees");
        Tree.Should().Contain("prefers-reduced-motion: reduce");
    }

    [Fact]
    public void TheCss_IsIndentedOnPhones_LeftToRightOnWideScreens_WithoutPageOverflow()
    {
        var css = Read("portal", "src", "styles.css");
        var start = css.IndexOf("/* ---------- /members/hierarchy (#223)", StringComparison.Ordinal);
        var end = css.IndexOf("/* ---------- member admin (React track 018)", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        var tree = css[start..end];

        tree.Should().Contain(".hnode__children {\n  display: grid;\n  gap: 6px;\n  margin: 6px 0 0 6px;\n  padding-left: 10px;",
            "16px a level on phones");
        tree.Should().Contain("@media (min-width: 900px) {\n  .hnode-item--root {\n    overflow-x: auto;",
            "on wide screens a too-deep family scrolls inside its own card, never the page");
        tree.Should().Contain("min-width: 44px;\n  min-height: 44px;", "phone touch targets");
        tree.Should().NotContain("!important").And.NotContain("100vw").And.NotContain("touch-action");
        css.Should().NotContain(".family__person").And.NotContain(".family__children");
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

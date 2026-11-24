using FluentAssertions;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// #366 (task 154): one select in the portal. Every dropdown is <c>SelectField</c> (<c>components/ui/SearchTools.tsx</c>);
/// no page or component writes its own <c>&lt;select&gt;</c> or <c>control--select</c> wrapper, and the old bare-select
/// box rules (<c>.form__field select</c>) are gone so they cannot draw a second box or focus ring inside the control.
/// </summary>
public class SharedSelectSourceTests
{
    private static readonly string Src = Path.Combine(RepoPaths.Root(), "src", "RTUB.Web", "portal", "src");
    private static readonly string Kit = Path.Combine(Src, "components", "ui", "SearchTools.tsx");

    private static string Read(string file) => File.ReadAllText(file).Replace("\r\n", "\n");

    [Fact]
    public void OnlySelectFieldWritesASelect()
    {
        var offenders = Directory.EnumerateFiles(Src, "*.tsx", SearchOption.AllDirectories)
            .Where(f => !string.Equals(Path.GetFullPath(f), Path.GetFullPath(Kit), StringComparison.OrdinalIgnoreCase))
            .Where(f => Read(f) is var s && (s.Contains("<select", StringComparison.Ordinal) || s.Contains("control--select", StringComparison.Ordinal)))
            .Select(f => Path.GetRelativePath(Src, f))
            .ToList();

        offenders.Should().BeEmpty("every select is the shared SelectField");
        Read(Kit).Should().Contain("<select").And.Contain("'control control--select'");
    }

    [Fact]
    public void TheBareSelectBoxRulesAreGone()
    {
        var css = PortalCss.Read(Src).Replace("\r\n", "\n");

        css.Should().NotContain(".form__field select");
        css.Should().NotContain(".lx-template__fields select");
        css.Should().Contain(".form__field--error .control {");
    }

    [Fact]
    public void TrintunaForm_UsesTheSharedSelect_WithoutOptionalLabels()
    {
        var form = Read(Path.Combine(Src, "components", "TrintunaForm.tsx"));

        form.Should().Contain("<SelectField").And.NotContain("(opcional)");
        form.Should().Contain("label=\"Descrição\"").And.Contain("label=\"Organizador\"");
    }
}

using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.Security;

/// <summary>
/// Guards the CSP readiness work done in unit 024, the style-side counterpart to
/// <see cref="InlineScriptPolicyTests"/>: browser-served application markup must not carry
/// inline CSS, because a strict <c>style-src</c> without <c>'unsafe-inline'</c> blocks both
/// &lt;style&gt; elements and <c>style="..."</c> attributes.
///
/// What is and is not a violation was established empirically against the CSP the app will
/// ship, not assumed:
///   BLOCKED  - a style attribute in markup, <c>setAttribute("style", ...)</c>, a &lt;style&gt;
///              element (authored or injected), and a style attribute inside an HTML string
///              assigned to innerHTML.
///   ALLOWED  - CSSOM writes: <c>el.style.prop = ...</c> and
///              <c>el.style.setProperty("--x", ...)</c>. CSP does not govern the CSSOM, so
///              those are deliberately NOT banned here - js/dynamicStyle.js depends on it.
///
/// The repository-wide scans are aggregated Facts, not Theories: they sweep every file and
/// report all violations in one failure rather than adding hundreds of per-file cases.
/// </summary>
public class InlineStylePolicyTests
{
    /// <summary>
    /// Matches an HTML inline style attribute. The negative lookbehind keeps
    /// <c>data-style=</c> and similar hyphenated/word-prefixed attributes out of the match,
    /// and the lowercase-only name avoids matching a PascalCase Blazor component parameter.
    /// </summary>
    private static readonly Regex InlineStyleAttribute =
        new(@"(?<![\w-])style\s*=\s*[""']", RegexOptions.Compiled);

    /// <summary>Matches an opening &lt;style&gt; element.</summary>
    private static readonly Regex StyleElement =
        new(@"<style[\s>]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Script-side sinks that are genuinely blocked by a strict <c>style-src</c>, plus
    /// <c>cssText</c>. cssText goes through the CSSOM and is not itself blocked, but it is
    /// an inline-style-shaped bulk write with no remaining use in this repo, so it is pinned
    /// shut rather than left as a tempting escape hatch.
    /// </summary>
    private static readonly Regex BlockedStyleSink =
        new(@"\.style\.cssText"
            + @"|setAttribute\(\s*[""']style[""']"
            + @"|createElement\(\s*[""']style[""']",
            RegexOptions.Compiled);

    /// <summary>Matches a style attribute written into an HTML string built in JavaScript.</summary>
    private static readonly Regex StyleAttributeInJsMarkup =
        new(@"(?<![\w-])style\s*=\s*\\?[""']", RegexOptions.Compiled);

    /// <summary>
    /// Email templates are rendered to an HTML string and delivered over SMTP. They are never
    /// an HTTP response from this app, so no CSP applies to them - and mail clients strip
    /// &lt;style&gt; blocks, which makes inline styles a hard requirement there. They are the
    /// only markup excluded from the sweeps below.
    /// </summary>
    private const string EmailTemplates = "/EmailTemplates/";

    [Fact]
    public void BrowserServedMarkup_HasNoInlineStyleAttributes()
    {
        var violations = ScanAllMarkup(InlineStyleAttribute);

        violations.Should().BeEmpty(
            "style=\"...\" attributes must move to a CSS class so a strict CSP style-src can drop " +
            $"'unsafe-inline'.{Environment.NewLine}{Describe(violations)}");
    }

    [Fact]
    public void BrowserServedMarkup_HasNoInlineStyleElements()
    {
        var violations = ScanAllMarkup(StyleElement);

        violations.Should().BeEmpty(
            "inline <style> blocks must move into the wwwroot/css structure or a .razor.css file; " +
            $"a strict CSP style-src blocks them.{Environment.NewLine}{Describe(violations)}");
    }

    [Fact]
    public void ApplicationScripts_DoNotWriteInlineStyleSinks()
    {
        var violations = ScanAllScripts(BlockedStyleSink);

        violations.Should().BeEmpty(
            "application JavaScript must not set the style attribute or inject a <style> element; " +
            "both are blocked by a strict style-src. Use a CSS class, or el.style.setProperty for a " +
            $"genuinely dynamic value - the CSSOM is not CSP-governed.{Environment.NewLine}{Describe(violations)}");
    }

    [Fact]
    public void ApplicationScripts_DoNotBuildMarkupContainingStyleAttributes()
    {
        var violations = ScanAllScripts(StyleAttributeInJsMarkup);

        violations.Should().BeEmpty(
            "HTML built in JavaScript must not carry style=\"...\"; the browser parses it as an " +
            $"inline style attribute and a strict style-src blocks it.{Environment.NewLine}{Describe(violations)}");
    }

    /// <summary>
    /// The exclusion above must stay narrow. If inline styles ever appear in markup that IS
    /// served to a browser, the sweeps must catch it - so pin that the email templates really
    /// are the only excluded markup, and that they really do still carry inline styles (i.e.
    /// the exclusion is load-bearing rather than dead).
    /// </summary>
    [Fact]
    public void EmailTemplates_AreTheOnlyMarkupExcludedFromTheStyleSweeps()
    {
        var excluded = EnumerateAllMarkup()
            .Where(f => f.Replace('\\', '/').Contains(EmailTemplates, StringComparison.OrdinalIgnoreCase))
            .ToList();

        excluded.Should().NotBeEmpty("the email templates must still exist");
        excluded.Should().Contain(f => InlineStyleAttribute.IsMatch(File.ReadAllText(f)),
            "email HTML requires inline styles because mail clients strip <style>; if that stopped " +
            "being true the exclusion should be removed rather than left as dead configuration");
    }

    /// <summary>
    /// The offline fallback page is plain static HTML served by the service worker, so it gets
    /// explicit assertions rather than relying only on the repository-wide sweeps.
    /// </summary>
    [Fact]
    public void OfflinePage_LoadsItsStylesheetExternally()
    {
        var content = ReadRepoFile("src", "RTUB.Web", "wwwroot", "offline.html");

        StyleElement.IsMatch(content).Should().BeFalse("offline.html must have no inline <style> block");
        InlineStyleAttribute.IsMatch(content).Should().BeFalse("offline.html must have no inline style attribute");
        content.Should().Contain("/css/offline.css", "offline.html must reference its external stylesheet");
    }

    /// <summary>
    /// offline.html is served from the service worker cache while the network is down, so the
    /// stylesheet it now depends on has to be precached too - otherwise the fallback page renders
    /// unstyled. The shared script/style branch's cross-cache fallback (pinned by
    /// <see cref="InlineScriptPolicyTests"/>) is what makes the precached copy reachable.
    /// </summary>
    [Fact]
    public void ServiceWorker_PrecachesOfflinePageStylesheet()
    {
        var content = ReadRepoFile("src", "RTUB.Web", "wwwroot", "service-worker.js");

        var staticAssets = Regex.Match(content, @"const STATIC_ASSETS\s*=\s*\[(.*?)\]", RegexOptions.Singleline);

        staticAssets.Success.Should().BeTrue("service-worker.js should declare a STATIC_ASSETS precache list");
        staticAssets.Groups[1].Value.Should().Contain("/css/offline.css",
            "the offline page's stylesheet must be precached so it is available with no network");
    }

    /// <summary>
    /// The service-worker update toast is built in JavaScript. Its appearance moved to
    /// css/3-components/sw-update-toast.css, including the slide-up keyframes that used to be
    /// injected as a &lt;style&gt; element.
    /// </summary>
    [Fact]
    public void ServiceWorkerUpdateToast_IsStyledByClassesFromAnExternalStylesheet()
    {
        var register = ReadRepoFile("src", "RTUB.Web", "wwwroot", "js", "sw-register.js");

        register.Should().Contain("rtub-sw-toast",
            "the toast must carry the class its external stylesheet targets");

        var toastCss = ReadRepoFile("src", "RTUB.Web", "wwwroot", "css", "3-components", "sw-update-toast.css");

        toastCss.Should().Contain("@keyframes rtub-toast-slide-up",
            "the slide-up animation must live in the stylesheet, not be injected as a <style> element");

        ReadRepoFile("src", "RTUB.Web", "wwwroot", "css", "site.css")
            .Should().Contain("sw-update-toast.css", "site.css must import the toast stylesheet");
    }

    /// <summary>
    /// Runtime-valued styles travel as validated data-* attributes and are applied through the
    /// CSSOM, which CSP does not govern. The bridge must stay narrow: it accepts a percentage
    /// and a six-digit hex colour, not arbitrary CSS - otherwise it would hand back exactly the
    /// capability a strict style-src removes.
    /// </summary>
    [Fact]
    public void DynamicStyleBridge_OnlyAcceptsValidatedValues()
    {
        var content = ReadRepoFile("src", "RTUB.Web", "wwwroot", "js", "dynamicStyle.js");

        content.Should().Contain("setProperty",
            "the bridge must apply values through the CSSOM, which is not CSP-governed");
        content.Should().NotContain("cssText",
            "the bridge must set named custom properties, never a whole declaration block");
        content.Should().MatchRegex(@"\^\\d\{1,3\}\(\\\.\\d\+\)\?\$",
            "data-fill-pct must be validated as a plain percentage");
        content.Should().MatchRegex(@"\^#\[0-9a-fA-F\]\{6\}\$",
            "data-swatch must be validated as a six-digit hex colour");

        ReadRepoFile("src", "RTUB.Web", "Shared", "MainLayout.razor")
            .Should().Contain("/js/dynamicStyle.js", "MainLayout must load the bridge on every page");
    }

    /// <summary>
    /// Values that used to sit in an inline style attribute and are now carried by a class.
    /// The component tests assert the class is emitted; this pins the value behind it, so the
    /// migration cannot silently change what those elements look like.
    /// </summary>
    [Fact]
    public void MigratedValues_ArePreservedInTheStylesheetThatReplacedThem()
    {
        var dynamicClasses = ReadRepoFile("src", "RTUB.Web", "wwwroot", "css", "9-overrides", "dynamic-style-classes.css");

        dynamicClasses.Should().Contain("background-color: #007bff",
            "the Member role badge's blue moved from RoleBadge's inline style into .role-badge--member");
        dynamicClasses.Should().Contain("max-height: 400px",
            "EmailRecipientsPreview's MaxHeight=\"400px\" moved into .subscriber-list--h400");
        dynamicClasses.Should().Contain("max-height: 300px", "...and MaxHeight=\"300px\"");
        dynamicClasses.Should().Contain("max-height: 200px", "...and the MaxHeight default");

        ReadRepoFile("src", "RTUB.Web", "wwwroot", "css", "site.css")
            .Should().Contain("9-overrides/dynamic-style-classes.css",
                "site.css must import the classes that replaced dynamic inline styles");
    }

    // ---------- helpers ----------

    private sealed record Violation(string File, int Line, string Snippet);

    private static List<Violation> ScanAllMarkup(Regex pattern) =>
        Scan(pattern, EnumerateBrowserServedMarkup());

    private static List<Violation> ScanAllScripts(Regex pattern) =>
        Scan(pattern, EnumerateApplicationScripts());

    /// <summary>
    /// Collects every match rather than stopping at the first, so one failure reports the whole
    /// picture. Matching runs against the whole file because an opening tag may span lines; the
    /// line number is derived from the match offset afterwards purely for reporting.
    /// </summary>
    private static List<Violation> Scan(Regex pattern, IEnumerable<string> files)
    {
        var root = GetProjectRoot();
        var violations = new List<Violation>();

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var content = File.ReadAllText(file);

            foreach (Match match in pattern.Matches(content))
            {
                violations.Add(new Violation(relative, LineOf(content, match.Index), Excerpt(content, match.Index)));
            }
        }

        return violations;
    }

    private static int LineOf(string content, int index) =>
        content.AsSpan(0, index).Count('\n') + 1;

    private static string Excerpt(string content, int index)
    {
        var start = Math.Max(0, index - 30);
        var length = Math.Min(120, content.Length - start);
        var raw = content.Substring(start, length);

        return string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Describe(IReadOnlyCollection<Violation> violations)
    {
        if (violations.Count == 0)
        {
            return string.Empty;
        }

        var report = new StringBuilder();
        report.AppendLine($"Found {violations.Count} violation(s):");

        foreach (var v in violations)
        {
            report.AppendLine($"  {v.File}:{v.Line}  {v.Snippet}");
        }

        return report.ToString();
    }

    /// <summary>All .razor/.cshtml/.html under src/, excluding third-party bundles and build output.</summary>
    private static IEnumerable<string> EnumerateAllMarkup()
    {
        var src = Path.Combine(GetProjectRoot(), "src");
        var extensions = new[] { "*.razor", "*.cshtml", "*.html" };

        return extensions
            .SelectMany(ext => Directory.EnumerateFiles(src, ext, SearchOption.AllDirectories))
            .Where(f => !IsExcludedPath(f))
            .OrderBy(f => f, StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateBrowserServedMarkup() =>
        EnumerateAllMarkup()
            .Where(f => !f.Replace('\\', '/').Contains(EmailTemplates, StringComparison.OrdinalIgnoreCase));

    /// <summary>Application-owned JavaScript under wwwroot, excluding vendored libraries.</summary>
    private static IEnumerable<string> EnumerateApplicationScripts()
    {
        var wwwroot = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "wwwroot");

        return Directory.EnumerateFiles(wwwroot, "*.js", SearchOption.AllDirectories)
            .Where(f => !IsExcludedPath(f))
            .OrderBy(f => f, StringComparer.Ordinal);
    }

    private static bool IsExcludedPath(string path)
    {
        var normalized = path.Replace('\\', '/');

        return normalized.Contains("/wwwroot/lib/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRepoFile(params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { GetProjectRoot() }.Concat(segments).ToArray()));

    private static string GetProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new InvalidOperationException("Could not find project root directory");
        }

        return directory.FullName;
    }
}

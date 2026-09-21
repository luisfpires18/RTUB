using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.Security;

/// <summary>
/// Guards the CSP readiness work done in unit 023: application-owned markup must not
/// execute JavaScript inline, because a strict <c>script-src</c> without
/// <c>'unsafe-inline'</c> blocks both inline &lt;script&gt; blocks and inline HTML event
/// handler attributes such as onclick/onerror.
///
/// The two repository-wide scans are aggregated Facts, not Theories: they sweep every
/// markup file and report all violations in a single failure. Exposing one xUnit case per
/// scanned file would add hundreds of cases that say nothing individually.
///
/// Blazor directives (@onclick, @onchange, @oninput) compile to server-side delegates and
/// are never emitted as HTML attributes, so they are not violations and are excluded.
/// External &lt;script src="..."&gt; tags are likewise fine.
/// </summary>
public class InlineScriptPolicyTests
{
    /// <summary>
    /// Matches an opening &lt;script&gt; tag that carries no src attribute, i.e. one whose
    /// body is executable inline JavaScript.
    /// </summary>
    private static readonly Regex InlineScriptTag =
        new(@"<script(?![^>]*\ssrc\s*=)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches an HTML inline event handler attribute (onclick="...", onerror='...').
    /// Deliberately case-sensitive and lowercase-only: HTML attribute names in this repo are
    /// lowercase, whereas Blazor component parameters are PascalCase (OnClose, OnConfirm),
    /// and matching those would be a false positive. The negative lookbehind for '@' keeps
    /// Blazor's @onclick bindings out of the match.
    /// </summary>
    private static readonly Regex InlineEventHandler =
        new(@"(?<![@\w-])on[a-z]+\s*=\s*[""']", RegexOptions.Compiled);

    [Fact]
    public void ApplicationMarkup_HasNoInlineExecutableScripts()
    {
        var violations = ScanAllMarkup(InlineScriptTag);

        violations.Should().BeEmpty(
            "inline <script> blocks must move to an external file under wwwroot/js so a strict " +
            $"CSP script-src can drop 'unsafe-inline'.{Environment.NewLine}{Describe(violations)}");
    }

    [Fact]
    public void ApplicationMarkup_HasNoInlineJavaScriptEventHandlers()
    {
        var violations = ScanAllMarkup(InlineEventHandler);

        violations.Should().BeEmpty(
            "inline HTML event handler attributes such as onclick=/onerror= must become a Blazor " +
            "@on... binding or an addEventListener in external JS; a strict CSP script-src blocks " +
            $"them exactly as it blocks inline <script>.{Environment.NewLine}{Describe(violations)}");
    }

    /// <summary>
    /// The offline fallback page is plain static HTML served by the service worker, so it gets
    /// explicit assertions rather than relying only on the repository-wide sweeps above.
    /// </summary>
    [Fact]
    public void OfflinePage_LoadsItsScriptExternallyAndHasNoInlineHandler()
    {
        var content = ReadRepoFile("src", "RTUB.Web", "wwwroot", "offline.html");

        InlineScriptTag.IsMatch(content).Should().BeFalse("offline.html must have no inline <script>");
        InlineEventHandler.IsMatch(content).Should().BeFalse("offline.html must have no inline on* attribute");
        content.Should().Contain("/js/offline.js", "offline.html must reference its external behaviour script");
    }

    /// <summary>
    /// offline.html is served from the service worker cache while the network is down, so the
    /// script it depends on has to be precached too - otherwise the fallback page loads without
    /// its connection-status behaviour.
    /// </summary>
    [Fact]
    public void ServiceWorker_PrecachesOfflinePageScript()
    {
        var content = ReadRepoFile("src", "RTUB.Web", "wwwroot", "service-worker.js");

        var staticAssets = Regex.Match(content, @"const STATIC_ASSETS\s*=\s*\[(.*?)\]", RegexOptions.Singleline);

        staticAssets.Success.Should().BeTrue("service-worker.js should declare a STATIC_ASSETS precache list");
        staticAssets.Groups[1].Value.Should().Contain("/js/offline.js",
            "the offline page's script must be precached so it is available with no network");
    }

    /// <summary>
    /// Precaching alone is not enough: the service worker's script branch reads DYNAMIC_CACHE,
    /// which only holds scripts already fetched once online. Its network-failure path must fall
    /// back to a cross-cache lookup, or the offline page loads without its script for any user
    /// who never opened it while online.
    /// </summary>
    [Fact]
    public void ServiceWorker_ScriptFallbackReachesPrecachedAssetsWhenOffline()
    {
        var content = ReadRepoFile("src", "RTUB.Web", "wwwroot", "service-worker.js");

        content.Should().Contain("cached || caches.match(request)",
            "the script branch's offline fallback must search all caches, not just DYNAMIC_CACHE, " +
            "so precached STATIC_ASSETS such as /js/offline.js are reachable with no network");
    }

    /// <summary>
    /// The Blazor startup call was moved out of MainLayout's inline script. The externalized file
    /// must still run after blazor.web.js, which holds only while blazor.web.js keeps
    /// autostart="false" and both remain classic scripts.
    /// </summary>
    [Fact]
    public void MainLayout_KeepsManualBlazorStartOrdering()
    {
        var content = ReadRepoFile("src", "RTUB.Web", "Shared", "MainLayout.razor");

        var frameworkIndex = content.IndexOf("blazor.web.js", StringComparison.Ordinal);
        var startupIndex = content.IndexOf("/js/blazorStartup.js", StringComparison.Ordinal);

        frameworkIndex.Should().BeGreaterThan(-1, "MainLayout must still load blazor.web.js");
        startupIndex.Should().BeGreaterThan(-1, "MainLayout must load the externalized startup script");
        startupIndex.Should().BeGreaterThan(frameworkIndex,
            "blazorStartup.js must be referenced after blazor.web.js so the Blazor global exists");
    }

    [Fact]
    public void MainLayout_KeepsBlazorAutostartDisabled()
    {
        var content = ReadRepoFile("src", "RTUB.Web", "Shared", "MainLayout.razor");

        content.Should().Contain("autostart=\"false\"",
            "manual Blazor.start() in blazorStartup.js requires blazor.web.js to keep autostart=\"false\"");
    }

    [Fact]
    public void BlazorStartup_IsTheOnlyCallerOfBlazorStart()
    {
        var jsRoot = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "wwwroot", "js");

        var callers = Directory.EnumerateFiles(jsRoot, "*.js", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("Blazor.start(", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        callers.Should().BeEquivalentTo(new[] { "blazorStartup.js" },
            "calling Blazor.start() more than once throws; exactly one file may call it");
    }

    /// <summary>
    /// The delegated avatar fallback listener replaced per-element inline onerror handlers.
    /// It has to be attached from &lt;head&gt;, before any avatar image can fail to load.
    /// </summary>
    [Fact]
    public void AvatarFallbackScript_IsLoadedBeforeMarkupIsParsed()
    {
        var app = ReadRepoFile("src", "RTUB.Web", "App.razor");

        var headEnd = app.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        var scriptIndex = app.IndexOf("/js/avatarFallback.js", StringComparison.Ordinal);

        scriptIndex.Should().BeGreaterThan(-1, "App.razor must reference the avatar fallback script");
        scriptIndex.Should().BeLessThan(headEnd,
            "the fallback listener must be attached from <head>, before any avatar <img> is parsed");

        EnumerateApplicationMarkup()
            .Count(f => File.ReadAllText(f).Contains("data-avatar-fallback", StringComparison.Ordinal))
            .Should().BeGreaterThan(0,
                "avatar images should opt into the delegated fallback via data-avatar-fallback");
    }

    /// <summary>
    /// App.razor used to duplicate the service worker registration in an inline head script, with
    /// the same script URL and scope as sw-register.js. Unit 023 dropped the duplicate; page-load
    /// registration is now sw-register.js alone.
    ///
    /// push-notifications.js also calls register() on demand when the user opts into push. That is
    /// a separate, pre-existing path and is out of scope here.
    /// </summary>
    [Fact]
    public void AppRazor_DoesNotDuplicateServiceWorkerRegistration()
    {
        ReadRepoFile("src", "RTUB.Web", "App.razor")
            .Should().NotContain("serviceWorker.register",
                "App.razor must not re-register the service worker; sw-register.js owns page-load registration");

        ReadRepoFile("src", "RTUB.Web", "wwwroot", "js", "sw-register.js")
            .Should().Contain("serviceWorker.register(",
                "sw-register.js must still perform the page-load registration App.razor no longer duplicates");

        ReadRepoFile("src", "RTUB.Web", "Shared", "MainLayout.razor")
            .Should().Contain("/js/sw-register.js",
                "MainLayout must keep loading sw-register.js on every page");
    }

    // ---------- helpers ----------

    private sealed record Violation(string File, int Line, string Snippet);

    /// <summary>
    /// Scans every application markup file and collects every match, rather than stopping at the
    /// first one, so a single failure reports the whole picture.
    ///
    /// Matching runs against the whole file, not line by line, because an opening tag may span
    /// several lines (MainLayout's leaflet tag does). The line number is derived from the match
    /// offset afterwards purely for reporting.
    /// </summary>
    private static List<Violation> ScanAllMarkup(Regex pattern)
    {
        var root = GetProjectRoot();
        var violations = new List<Violation>();

        foreach (var file in EnumerateApplicationMarkup())
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var content = File.ReadAllText(file);

            foreach (Match match in pattern.Matches(content))
            {
                violations.Add(new Violation(relative, LineOf(content, match.Index), Excerpt(match.Value)));
            }
        }

        return violations;
    }

    private static int LineOf(string content, int index) =>
        content.AsSpan(0, index).Count('\n') + 1;

    private static string Excerpt(string value)
    {
        var excerpt = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return excerpt.Length > 120 ? excerpt[..120] + "..." : excerpt;
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

    /// <summary>
    /// All .razor/.cshtml/.html under src/, excluding third-party bundles in wwwroot/lib.
    /// </summary>
    private static IEnumerable<string> EnumerateApplicationMarkup()
    {
        var src = Path.Combine(GetProjectRoot(), "src");
        var extensions = new[] { "*.razor", "*.cshtml", "*.html" };

        return extensions
            .SelectMany(ext => Directory.EnumerateFiles(src, ext, SearchOption.AllDirectories))
            .Where(f => !f.Replace('\\', '/').Contains("/wwwroot/lib/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal);
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

using System.Text.RegularExpressions;
using FluentAssertions;

namespace RTUB.Web.Tests.Pwa;

/// <summary>
/// Guards the PWA reliability work done in unit 026. These are source-level scans rather than
/// runtime tests: the behaviour they protect lives in a service worker, which has no origin,
/// no DOM and no test host, so the only place to pin it cheaply is the source itself.
///
/// The header contract for <c>/service-worker.js</c> (no Content-Security-Policy) is pinned
/// separately and over the wire by
/// <c>RTUB.Integration.Tests.SecurityHeaderTests.NonDocumentResponse_CarriesNoContentSecurityPolicy</c>.
/// </summary>
public partial class ServiceWorkerReliabilityTests
{
    private static string ServiceWorker => ReadRepoFile("src", "RTUB.Web", "wwwroot", "service-worker.js");

    // ---------- B: one registration owner ----------

    /// <summary>
    /// Until 026 the worker was registered twice: by sw-register.js (scope '/', updateViaCache
    /// 'none') and again by push-notifications.js with no options at all. sw-register.js is now
    /// the sole owner; everything else adopts its registration.
    /// </summary>
    [Fact]
    public void ApplicationSource_ContainsExactlyOneServiceWorkerRegistration()
    {
        var registrationCall = new Regex(@"serviceWorker\s*\.\s*register\s*\(");

        var callers = EnumerateApplicationSource()
            .Select(file => (Source: Relative(file), Count: registrationCall.Matches(File.ReadAllText(file)).Count))
            .Where(entry => entry.Count > 0)
            .ToList();

        callers.Should().BeEquivalentTo(
            new[] { (Source: "src/RTUB.Web/wwwroot/js/sw-register.js", Count: 1) },
            "exactly one intentional navigator.serviceWorker.register call may exist; a second " +
            "registration duplicates the update lifecycle and can raise duplicate toasts. Found: " +
            string.Join(", ", callers.Select(c => $"{c.Source} x{c.Count}")));
    }

    /// <summary>
    /// 038: the React push client (portal/src/push.ts) replaced push-notifications.js. It adopts sw-register.js's
    /// registration through navigator.serviceWorker.ready (which also guarantees an active worker before
    /// pushManager.subscribe()) and never registers one itself.
    /// </summary>
    [Fact]
    public void ReactPushClient_AdoptsTheExistingRegistrationInsteadOfRegistering()
    {
        var push = ReadRepoFile("src", "RTUB.Web", "portal", "src", "data/push.ts");

        push.Should().MatchRegex(@"navigator\s*\.\s*serviceWorker\s*\.\s*ready");
        push.Should().NotMatchRegex(@"serviceWorker\s*\.\s*register\s*\(");
        File.Exists(Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "wwwroot", "js", "push-notifications.js")).Should().BeFalse();
        File.Exists(Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "wwwroot", "js", "pwa-helper.js")).Should().BeFalse();
    }

    /// <summary>
    /// navigator.serviceWorker.ready never resolves if nothing ever registers. Since 037 the React shell is the only
    /// page shell left, so it must keep loading the owner on every page.
    /// </summary>
    [Fact]
    public void PortalShell_LoadsTheRegistrationOwner()
    {
        ReadRepoFile("src", "RTUB.Web", "portal", "index.html")
            .Should().Contain("/js/sw-register.js");
    }

    // ---------- C: cache safety ----------

    [Theory]
    [InlineData("/api/")]
    [InlineData("/auth/")]
    [InlineData("/hubs/")]
    [InlineData("/health")]
    public void ServiceWorker_DeclaresPathAsNeverCached(string prefix)
    {
        NeverCacheList().Should().Contain($"'{prefix}'",
            "{0} is authenticated, real-time or a liveness probe; a cached copy is either " +
            "private data on disk or a stale answer", prefix);
    }

    /// <summary>
    /// Task 037 retired the Blazor host: /_framework/blazor.web.js is no longer served, so a precache entry for it
    /// would fail cache.addAll and leave the offline page uncached; /_blazor no longer exists to exclude.
    /// </summary>
    [Fact]
    public void ServiceWorker_KnowsNoBlazorAssetOrPath()
    {
        ServiceWorker.Should().NotContain("_framework").And.NotContain("_blazor").And.NotContain("blazor.web.js");
    }

    /// <summary>
    /// The guard has to run before any branch that opens a cache, or the exclusion list is
    /// decorative. Position in the fetch handler is the whole contract.
    /// </summary>
    [Fact]
    public void ServiceWorker_AppliesTheNeverCacheGuardBeforeAnyCacheBranch()
    {
        var fetchHandler = FetchHandler();

        var guard = fetchHandler.IndexOf("isNeverCached(url.pathname)", StringComparison.Ordinal);
        var firstCacheUse = fetchHandler.IndexOf("caches.", StringComparison.Ordinal);

        guard.Should().BeGreaterThan(-1, "the fetch handler must consult the never-cache list");
        firstCacheUse.Should().BeGreaterThan(-1, "the fetch handler is expected to use caches somewhere");
        guard.Should().BeLessThan(firstCacheUse,
            "the never-cache guard must short-circuit to the network before any cache is read or written");
    }

    [Fact]
    public void ServiceWorker_PassesNonGetRequestsStraightToTheNetwork()
    {
        var fetchHandler = FetchHandler();

        var methodGuard = fetchHandler.IndexOf("request.method !== 'GET'", StringComparison.Ordinal);
        var firstCacheUse = fetchHandler.IndexOf("caches.", StringComparison.Ordinal);

        methodGuard.Should().BeGreaterThan(-1, "non-GET requests must be excluded explicitly");
        methodGuard.Should().BeLessThan(firstCacheUse,
            "the non-GET guard must run before any cache branch");
    }

    /// <summary>
    /// The regression this unit exists to close. Up to v2.6.0 every 200 navigation response was
    /// written into DYNAMIC_CACHE, so a signed-in user's rendered HTML stayed on disk and could
    /// be served back offline - or after logout, to whoever held the device next.
    /// </summary>
    [Fact]
    public void ServiceWorker_DoesNotPersistApplicationHtml()
    {
        var branch = DocumentBranch();

        branch.Should().NotContain("cache.put",
            "authenticated application HTML must never become a persistent cache entry");
        branch.Should().NotContain("caches.open",
            "the document branch must not open a cache to write into");
    }

    /// <summary>
    /// '/' renders differently for every signed-in user, so precaching it stores one session's
    /// HTML for everyone. offline.html is the only document the offline experience needs.
    /// </summary>
    [Fact]
    public void ServiceWorker_DoesNotPrecacheTheApplicationRoot()
    {
        StaticAssets().Should().NotMatchRegex(@"(^|[\s,])'/'\s*,",
            "the application root is user-specific HTML and must not be precached");
    }

    // ---------- helpers ----------

    private static string FetchHandler() =>
        Section(ServiceWorker, "addEventListener('fetch'", "addEventListener('push'");

    private static string NeverCacheList() =>
        Capture(ServiceWorker, @"const NEVER_CACHE_PREFIXES\s*=\s*\[(.*?)\]", "NEVER_CACHE_PREFIXES");

    private static string StaticAssets() =>
        Capture(ServiceWorker, @"const STATIC_ASSETS\s*=\s*\[(.*?)\]", "STATIC_ASSETS");

    private static string DocumentBranch() =>
        Section(ServiceWorker, "Documents / navigations:", "Fallback for any other requests");

    private static string SwRegister() =>
        ReadRepoFile("src", "RTUB.Web", "wwwroot", "js", "sw-register.js");

    private static string Capture(string content, string pattern, string name)
    {
        var match = Regex.Match(content, pattern, RegexOptions.Singleline);
        match.Success.Should().BeTrue($"service-worker.js must declare {name}");
        return match.Groups[1].Value;
    }

    /// <summary>Text between two anchors, so an assertion applies to one branch and not the file.</summary>
    private static string Section(string content, string startAnchor, string endAnchor)
    {
        var start = content.IndexOf(startAnchor, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"expected to find '{startAnchor}'");

        var end = content.IndexOf(endAnchor, start, StringComparison.Ordinal);
        return end < 0 ? content[start..] : content[start..end];
    }

    private static IEnumerable<string> EnumerateApplicationSource()
    {
        var src = Path.Combine(GetProjectRoot(), "src");

        return new[] { "*.js", "*.razor", "*.cshtml", "*.html", "*.cs", "*.ts" }
            .SelectMany(ext => Directory.EnumerateFiles(src, ext, SearchOption.AllDirectories))
            .Where(f =>
            {
                var path = f.Replace('\\', '/');
                return !path.Contains("/wwwroot/lib/", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(f => f, StringComparer.Ordinal);
    }

    private static string Relative(string file) =>
        Path.GetRelativePath(GetProjectRoot(), file).Replace('\\', '/');

    private static string ReadRepoFile(params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { GetProjectRoot() }.Concat(segments).ToArray()));

    private static string GetProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find project root directory");
    }
}

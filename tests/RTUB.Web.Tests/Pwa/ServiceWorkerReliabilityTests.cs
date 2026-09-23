using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

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
public class ServiceWorkerReliabilityTests
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

    [Fact]
    public void PushNotificationsManager_AdoptsTheExistingRegistrationInsteadOfRegistering()
    {
        var push = ReadRepoFile("src", "RTUB.Web", "wwwroot", "js", "push-notifications.js");

        push.Should().MatchRegex(@"navigator\s*\.\s*serviceWorker\s*\.\s*ready",
            "PushNotificationsManager must wait for the registration sw-register.js owns; " +
            "navigator.serviceWorker.ready also guarantees an active worker, which iOS Safari " +
            "requires before pushManager.subscribe()");

        push.Should().NotMatchRegex(@"serviceWorker\s*\.\s*register\s*\(",
            "PushNotificationsManager must not register a second time");
    }

    /// <summary>
    /// push-notifications.js waits on navigator.serviceWorker.ready, which never resolves if
    /// nothing ever registers. MainLayout must keep loading the owner on every page.
    /// </summary>
    [Fact]
    public void MainLayout_LoadsTheRegistrationOwner()
    {
        ReadRepoFile("src", "RTUB.Web", "Shared", "MainLayout.razor")
            .Should().Contain("/js/sw-register.js");
    }

    // ---------- C: cache safety ----------

    [Theory]
    [InlineData("/api/")]
    [InlineData("/auth/")]
    [InlineData("/_blazor")]
    [InlineData("/hubs/")]
    [InlineData("/health")]
    public void ServiceWorker_DeclaresPathAsNeverCached(string prefix)
    {
        NeverCacheList().Should().Contain($"'{prefix}'",
            "{0} is authenticated, real-time or a liveness probe; a cached copy is either " +
            "private data on disk or a stale answer", prefix);
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

    // ---------- Offline ----------

    [Fact]
    public void ServiceWorker_FallsBackToOfflinePageForFailedNavigations()
    {
        DocumentBranch().Should().Contain("caches.match('/offline.html')",
            "offline.html is the document fallback when the network is unreachable");
    }

    [Theory]
    [InlineData("/offline.html")]
    [InlineData("/js/offline.js")]
    [InlineData("/css/offline.css")]
    public void ServiceWorker_PrecachesOfflineAsset(string asset)
    {
        StaticAssets().Should().Contain($"'{asset}'",
            "the offline page must render with its own script and stylesheet from cache alone");
    }

    // ---------- D: update lifecycle ----------

    /// <summary>
    /// install() used to call self.skipWaiting() unconditionally, so every new worker activated
    /// at once, claimed clients and forced a reload - which made the "Nova versao disponivel /
    /// Atualizar" prompt unreachable in practice. skipWaiting is now reached only through the
    /// SKIP_WAITING message the user's "Atualizar" click posts.
    /// </summary>
    [Fact]
    public void ServiceWorker_SkipsWaitingOnlyOnUserRequest()
    {
        var content = ServiceWorker;

        Section(content, "addEventListener('install'", "addEventListener('activate'")
            .Should().NotContain("skipWaiting",
                "a new worker must wait so the user can choose when to update");

        var messageHandler = Section(content, "addEventListener('message'", "addEventListener('pushsubscriptionchange'");
        messageHandler.Should().Contain("SKIP_WAITING");
        messageHandler.Should().Contain("self.skipWaiting()",
            "the SKIP_WAITING message posted by the update toast is the only activation trigger");
    }

    [Fact]
    public void SwRegister_PostsSkipWaitingOnlyFromTheUpdateButton()
    {
        var register = SwRegister();

        Regex.Matches(register, @"SKIP_WAITING").Count.Should().Be(1,
            "exactly one place may ask the waiting worker to activate");

        Section(register, "updateBtn.addEventListener('click'", "dismissBtn.addEventListener")
            .Should().Contain("SKIP_WAITING",
                "the only SKIP_WAITING post must be the 'Atualizar' click handler");
    }

    /// <summary>
    /// controllerchange fires both for a real update and for the first-ever install, where
    /// clients.claim() takes control of a page that was never controlled. Reloading in the
    /// latter case is a pointless extra navigation, and an unguarded reload is how a
    /// controllerchange loop starts.
    /// </summary>
    [Fact]
    public void SwRegister_HasOneGuardedReloadPath()
    {
        var register = SwRegister();

        Regex.Matches(register, @"location\.reload\(").Count.Should().Be(1,
            "exactly one reload path may exist");

        var handler = Section(register, "addEventListener('controllerchange'", "})();");
        handler.Should().Contain("if (refreshing) return;",
            "the reload must be guarded against firing twice");
        handler.Should().Contain("hadControllerAtStartup",
            "a first-ever install must not trigger a reload");
    }

    /// <summary>
    /// registerServiceWorker() is invoked twice (immediately, then on window load). register()
    /// itself is idempotent, but each call attached another updatefound listener, another
    /// visibilitychange listener and another update-check timer - which is how one update
    /// produced two toasts.
    /// </summary>
    [Fact]
    public void SwRegister_WiresTheUpdateLifecycleOnlyOnce()
    {
        SwRegister().Should().Contain("registrationStarted",
            "registerServiceWorker must be single-shot so its listeners are attached once");
    }

    // ---------- A: MobileBottomNav ----------

    /// <summary>
    /// MobileBottomNav is pinned with position: fixed; bottom: 0 and pads itself out of the
    /// iOS home-indicator area. Losing either half puts the nav under the system gesture bar.
    /// </summary>
    [Fact]
    public void MobileBottomNav_KeepsFixedBottomAndSafeAreaContract()
    {
        var css = ReadRepoFile("src", "RTUB.Shared", "Components", "UI", "MobileBottomNav.razor.css");

        css.Should().MatchRegex(@"position:\s*fixed");
        css.Should().MatchRegex(@"bottom:\s*0");
        css.Should().MatchRegex(@"left:\s*0");
        css.Should().MatchRegex(@"right:\s*0");
        css.Should().MatchRegex(@"padding-bottom:\s*env\(safe-area-inset-bottom",
            "the nav must clear the iOS home indicator");
    }

    /// <summary>
    /// The 026 root cause. 1-base/mobile.css deliberately uses <c>overflow-x: clip</c> on
    /// html/body because <c>overflow-x: hidden</c> makes iOS Safari treat them as the scroll
    /// container, and a position: fixed bottom nav then drifts into the middle of the viewport.
    /// A later sheet (2-layout/navbar.css, inside a display-mode: standalone query) reintroduced
    /// <c>hidden</c> at equal specificity and silently won on source order - visible only in the
    /// installed PWA, which is why browser-tab testing never caught it.
    ///
    /// No stylesheet may set overflow-x: hidden on html or body again, in any media query.
    /// </summary>
    [Fact]
    public void NoStylesheet_SetsOverflowHiddenOnTheViewportRoot()
    {
        var cssRoot = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "wwwroot", "css");

        var rootRule = new Regex(
            @"(?:^|[};/*\s])(?:html|body)\s*(?:,\s*(?:html|body)\s*)?\{[^}]*overflow-x\s*:\s*hidden",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        var violations = Directory.EnumerateFiles(cssRoot, "*.css", SearchOption.AllDirectories)
            .Where(f => !Relative(f).Contains("/lib/", StringComparison.OrdinalIgnoreCase))
            .Where(f => rootRule.IsMatch(File.ReadAllText(f)))
            .Select(Relative)
            .ToList();

        violations.Should().BeEmpty(
            "overflow-x on html/body must be `clip`, never `hidden`: `hidden` makes iOS Safari " +
            "treat the viewport root as a scroll container and MobileBottomNav (position: fixed) " +
            "drifts away from the bottom edge. Offending file(s): " + string.Join(", ", violations));
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

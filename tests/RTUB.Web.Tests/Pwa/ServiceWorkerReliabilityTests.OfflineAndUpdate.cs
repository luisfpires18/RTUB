// Service worker offline fallback and update lifecycle
using System.Text.RegularExpressions;
using FluentAssertions;

namespace RTUB.Web.Tests.Pwa;

public partial class ServiceWorkerReliabilityTests
{
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
    /// at once, claimed clients and forced a reload mid-use. Since 006 updates are silent: a new
    /// worker waits and activates on its own once every RTUB window is closed. Nothing may
    /// activate it early - no skipWaiting anywhere, no SKIP_WAITING message to ask for it.
    /// </summary>
    [Fact]
    public void ServiceWorker_NeverForcesActivation()
    {
        ServiceWorker.Should().NotContain("skipWaiting",
            "a new worker must wait for every RTUB window to close instead of taking over mid-use");
        ServiceWorker.Should().NotContain("SKIP_WAITING");
        SwRegister().Should().NotContain("SKIP_WAITING");
    }

    /// <summary>
    /// The "Nova versão disponível! / Atualizar / Depois" toast was retired in 006: React assets
    /// are hashed, Blazor assets carry ?v=, and documents are network-only, so there is no stale
    /// page to warn about. No prompt, and no reload path that could fire mid-use.
    /// </summary>
    [Fact]
    public void SwRegister_UpdatesSilently()
    {
        var register = SwRegister();

        register.Should().NotContain("Nova vers").And.NotContain("Atualizar").And.NotContain("Depois")
            .And.NotContain("rtub-sw-toast", "the update prompt is gone");
        register.Should().NotContain("location.reload(", "an update must never reload an open page");
        register.Should().Contain(".update()", "update checks still fetch new workers in the background");

        File.Exists(Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "wwwroot", "css", "3-components", "sw-update-toast.css"))
            .Should().BeFalse("the toast stylesheet was only used by the retired prompt");
    }

    /// <summary>
    /// registerServiceWorker() is invoked twice (immediately, then on window load). register()
    /// itself is idempotent, but each call attached another updatefound listener, another
    /// visibilitychange listener and another update-check timer.
    /// </summary>
    [Fact]
    public void SwRegister_WiresTheUpdateLifecycleOnlyOnce()
    {
        SwRegister().Should().Contain("registrationStarted",
            "registerServiceWorker must be single-shot so its listeners are attached once");
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
}

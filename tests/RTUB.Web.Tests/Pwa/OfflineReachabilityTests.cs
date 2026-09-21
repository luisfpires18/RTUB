using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.Pwa;

/// <summary>
/// Pins the offline page's origin-reachability probe, the one correctness defect found during
/// unit 026's own offline validation.
///
/// The bug: offline.js treated <c>navigator.onLine === true</c> as proof that RTUB was
/// reachable. With the origin stopped but the device still on a network, the service worker
/// served offline.html correctly, navigator.onLine stayed true, and the page redirected to '/'
/// a second later - which failed, landed back on offline.html, and bounced again.
///
/// The fix probes <c>/health</c>, which service-worker.js keeps network-only, so a cached 200
/// can never stand in for a live one. These are source-level scans for the same reason the
/// neighbouring service-worker tests are: the behaviour runs on a page served from a cache
/// with no origin, which no test host reproduces. The runtime proof is the browser run
/// recorded in STATE.md.
/// </summary>
public class OfflineReachabilityTests
{
    private static string OfflineScript =>
        ReadRepoFile("src", "RTUB.Web", "wwwroot", "js", "offline.js");

    /// <summary>
    /// The defect itself. Every redirect must be reached through the probe's success path, so
    /// no navigation may sit in a branch that only tested navigator.onLine.
    /// </summary>
    [Fact]
    public void OfflineScript_DoesNotRedirectOnNavigatorOnLineAlone()
    {
        var script = OfflineScript;

        var redirects = Regex.Matches(script, @"location\s*\.\s*href\s*=");
        redirects.Count.Should().Be(1, "exactly one navigation away from the offline page may exist");

        // The single redirect must live in the function the probe's ok-branch calls, not in
        // anything reachable straight from the navigator.onLine test.
        var reachable = Section(script, "function originReachable()", "function originUnreachable()");
        reachable.Should().Contain("location.href = '/'",
            "the redirect must sit behind the successful-probe path");

        var onLineTest = script.IndexOf("navigator.onLine", StringComparison.Ordinal);
        onLineTest.Should().BeGreaterThan(-1, "navigator.onLine is still the cheap short-circuit");
        Section(script, "if (!navigator.onLine)", "}")
            .Should().NotContain("location.href",
                "a true navigator.onLine must lead to a probe, never straight to a redirect");
    }

    /// <summary>
    /// /health is the agreed probe, and it must be the only endpoint this page calls - anything
    /// else would either be cached by the worker or carry authentication semantics.
    /// </summary>
    [Fact]
    public void OfflineScript_ProbesTheHealthEndpointAndNothingElse()
    {
        var fetches = Regex.Matches(OfflineScript, @"fetch\(\s*'([^']+)'")
            .Select(m => m.Groups[1].Value)
            .ToList();

        fetches.Should().BeEquivalentTo(new[] { "/health" },
            "the reachability probe is /health; no other endpoint may be called from offline.html. " +
            "Found: " + string.Join(", ", fetches));
    }

    /// <summary>
    /// A probe answered from the browser's HTTP cache proves nothing about the origin.
    /// </summary>
    [Fact]
    public void OfflineScript_ProbeBypassesTheBrowserHttpCache()
    {
        OfflineScript.Should().MatchRegex(
            @"fetch\(\s*'/health'\s*,\s*\{[^}]*cache:\s*'no-store'",
            "the /health probe must set cache: 'no-store' or a cached 200 can answer it");
    }

    /// <summary>
    /// The service worker is the other half of the contract: /health must stay network-only, or
    /// the probe can be satisfied from the worker's cache while the origin is down.
    /// </summary>
    [Fact]
    public void ServiceWorker_KeepsHealthNetworkOnly()
    {
        var worker = ReadRepoFile("src", "RTUB.Web", "wwwroot", "service-worker.js");

        var neverCache = Regex.Match(worker, @"const NEVER_CACHE_PREFIXES\s*=\s*\[(.*?)\]",
            RegexOptions.Singleline);
        neverCache.Success.Should().BeTrue("service-worker.js must declare NEVER_CACHE_PREFIXES");
        neverCache.Groups[1].Value.Should().Contain("'/health'",
            "the reachability probe must always reach the origin");

        Regex.Match(worker, @"const STATIC_ASSETS\s*=\s*\[(.*?)\]", RegexOptions.Singleline)
            .Groups[1].Value.Should().NotContain("/health",
                "a precached /health would answer the probe with a stale 200");
    }

    /// <summary>
    /// A failed, aborted or non-2xx probe must leave the user where they are. All three land in
    /// originUnreachable(), which only writes a status message.
    /// </summary>
    [Fact]
    public void OfflineScript_StaysOnThePageWhenTheProbeFails()
    {
        var script = OfflineScript;

        Section(script, "function originUnreachable()", "function checkConnection()")
            .Should().NotContain("location.",
                "an unreachable origin must not navigate anywhere");

        script.Should().Contain(".catch(function () {",
            "a rejected or aborted probe must be handled, not left to bubble");
        script.Should().MatchRegex(@"response\s*&&\s*response\.ok",
            "only a successful response may count as reachable");
        script.Should().Contain("new AbortController()",
            "an unreachable origin must not leave the probe pending for ever");
    }

    /// <summary>
    /// The accepted behaviour on a reachable origin is unchanged: the restored message, then a
    /// ~1s delay, then '/'.
    /// </summary>
    [Fact]
    public void OfflineScript_KeepsTheDelayedRedirectWhenTheOriginIsReachable()
    {
        var reachable = Section(OfflineScript, "function originReachable()", "function originUnreachable()");

        reachable.Should().Contain("STATUS_RESTORED");
        reachable.Should().MatchRegex(@"setTimeout\([\s\S]*?location\.href = '/'[\s\S]*?REDIRECT_DELAY_MS\)",
            "the redirect must stay behind the existing delay");

        OfflineScript.Should().MatchRegex(@"REDIRECT_DELAY_MS\s*=\s*1000",
            "the ~1 second delay is the accepted behaviour and is not being changed");
    }

    /// <summary>
    /// Three callers can fire a probe (first check, the 'online' event, the interval). Without a
    /// guard an unreachable origin would stack a new request every 3 seconds on top of one that
    /// has not timed out yet.
    /// </summary>
    [Fact]
    public void OfflineScript_AllowsOnlyOneProbeInFlightAndOneRedirect()
    {
        var script = OfflineScript;

        script.Should().Contain("probeInFlight",
            "overlapping probes from the initial check, the online event and the interval must be suppressed");
        Section(script, "function checkConnection()", "// \"Tentar Novamente\"")
            .Should().Contain("if (redirecting || probeInFlight)",
                "checkConnection must bail out while a probe is running or a redirect is pending");

        Section(script, "function originReachable()", "function originUnreachable()")
            .Should().Contain("if (redirecting)",
                "a second redirect must not be schedulable - that is what a bounce loop is made of");
    }

    /// <summary>
    /// The controls and the Portuguese copy 026 accepted are unchanged.
    /// </summary>
    [Fact]
    public void OfflineScript_KeepsItsExistingControlsAndCopy()
    {
        var script = OfflineScript;

        script.Should().Contain("window.addEventListener('online'");
        script.Should().Contain("window.addEventListener('offline'");
        script.Should().Contain("location.reload()", "the 'Tentar Novamente' button still reloads");
        script.Should().Contain("setInterval(checkConnection", "the periodic retry is preserved");
        script.Should().MatchRegex(@"POLL_INTERVAL_MS\s*=\s*3000");

        script.Should().Contain("A verificar ligação...");
        script.Should().Contain("Ainda offline");
        script.Should().Contain("Ligação restaurada! A recarregar...");
    }

    // ---------- helpers ----------

    /// <summary>Text between two anchors, so an assertion applies to one function and not the file.</summary>
    private static string Section(string content, string startAnchor, string endAnchor)
    {
        var start = content.IndexOf(startAnchor, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"expected to find '{startAnchor}'");

        var end = content.IndexOf(endAnchor, start + startAnchor.Length, StringComparison.Ordinal);
        return end < 0 ? content[start..] : content[start..end];
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

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find project root directory");
    }
}

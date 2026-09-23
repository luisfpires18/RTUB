using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for PWA Manifest configuration to ensure Chrome/Android can detect the manifest
/// </summary>
public class PwaManifestTests : IntegrationTestBase
{
    private readonly HttpClient _client;

    public PwaManifestTests(TestWebApplicationFactory factory) : base(factory)
    {
        _client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task ManifestWebmanifest_ReturnsSuccessStatusCode()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/manifest.webmanifest");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "manifest.webmanifest should be accessible");
    }

    [Fact]
    public async Task ManifestWebmanifest_HasCorrectContentType()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/manifest.webmanifest");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/manifest+json",
            "manifest.webmanifest must use application/manifest+json MIME type for Chrome/Android detection");
    }

    [Fact]
    public async Task ManifestWebmanifest_ContainsRtubName()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/manifest.webmanifest");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("RTUB", "manifest should contain RTUB name");
    }

    [Fact]
    public async Task ManifestWebmanifest_ContainsRequiredIcons()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/manifest.webmanifest");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("192x192", "manifest should contain 192x192 icon for Android");
        content.Should().Contain("512x512", "manifest should contain 512x512 icon for Android");
        content.Should().Contain("/icons/rtub-logo-192.png", "manifest should reference the 192x192 icon");
        content.Should().Contain("/icons/rtub-logo-512.png", "manifest should reference the 512x512 icon");
    }

    [Fact]
    public async Task ManifestWebmanifest_ContainsRequiredFieldsForTWA()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/manifest.webmanifest");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("\"id\"", "manifest should contain id field required for TWA");
        content.Should().Contain("\"name\"", "manifest should contain name field");
        content.Should().Contain("\"short_name\"", "manifest should contain short_name field");
        content.Should().Contain("\"start_url\"", "manifest should contain start_url field");
        content.Should().Contain("\"display\"", "manifest should contain display field");
        content.Should().Contain("\"scope\"", "manifest should contain scope field");
        content.Should().Contain("\"theme_color\"", "manifest should contain theme_color field");
        content.Should().Contain("\"background_color\"", "manifest should contain background_color field");
        content.Should().Contain("\"categories\"", "manifest should contain categories field for better discoverability");
    }

    /// <summary>
    /// The manifest id is the installed app's identity, and the spec resolves it against the
    /// start_url's origin: "/" already means "https://&lt;RTUB host&gt;/", unique to RTUB and never
    /// equal to an app on another origin. It has shipped since #452 (2025-12), so installed copies
    /// know RTUB by exactly this URL; any other id - or none, which falls back to start_url
    /// "/?utm_source=pwa" - describes a distinct application under the spec, not a replacement for
    /// the installed one. fix/030 (iOS Now Playing card opening another web app) checked it and kept
    /// it: RTUB-side identity causes were excluded; that routing is most likely iOS/WebKit behaviour.
    /// </summary>
    [Fact]
    public async Task Manifest_IdentityIsTheOriginRoot_AndStartUrlIsInScope()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/manifest.webmanifest");
        using var manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var json = manifest.RootElement;

        // Resolve as the manifest spec does: start_url and scope against the manifest URL,
        // id against the start_url's origin, a missing id falling back to start_url.
        var manifestUrl = response.RequestMessage!.RequestUri!;
        var origin = new Uri(manifestUrl, "/");
        var startUrl = new Uri(manifestUrl, json.GetProperty("start_url").GetString());
        var scope = new Uri(manifestUrl, json.GetProperty("scope").GetString());
        var id = json.TryGetProperty("id", out var idValue) && !string.IsNullOrEmpty(idValue.GetString())
            ? new Uri(new Uri(startUrl, "/"), idValue.GetString())
            : startUrl;

        // Assert
        id.Should().Be(origin,
            "every installed RTUB already carries the origin root as its identity; under the manifest spec " +
            "a different one describes a distinct application, not a replacement for the installed RTUB");
        scope.Should().Be(origin, "RTUB owns its whole origin, matching the service worker scope '/'");
        new Uri(startUrl, "/").Should().Be(origin, "a cross-origin start_url invalidates the manifest");
        startUrl.AbsolutePath.Should().StartWith(scope.AbsolutePath, "start_url must be inside scope");
    }

    [Fact]
    public async Task ServiceWorker_IsAccessible()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/service-worker.js");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "service-worker.js should be accessible");
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/javascript",
            "service worker should be served with correct content type");
    }

    [Fact]
    public async Task ServiceWorkerRegistration_IsAccessible()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/js/sw-register.js");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "sw-register.js should be accessible");
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/javascript");
    }

    [Fact]
    public async Task ServiceWorker_ContainsCachingStrategy()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/service-worker.js");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("install", "service worker should have install event");
        content.Should().Contain("activate", "service worker should have activate event");
        content.Should().Contain("fetch", "service worker should have fetch event for caching");
        content.Should().Contain("caches", "service worker should implement caching");
    }

    [Fact]
    public async Task ServiceWorker_HandlesAllFetchEventsWithRespondWith()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/service-worker.js");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verify fetch event listener exists
        content.Should().Contain("addEventListener('fetch'",
            "service worker must have fetch event listener for PWABuilder detection");

        // Verify event.respondWith is used (PWABuilder requirement)
        content.Should().Contain("event.respondWith",
            "service worker must use event.respondWith for all fetch events (PWABuilder requirement)");

        // Count event.respondWith occurrences to ensure all paths are covered
        var respondWithCount = System.Text.RegularExpressions.Regex.Matches(content, @"event\.respondWith").Count;
        respondWithCount.Should().BeGreaterThanOrEqualTo(5,
            "service worker should handle multiple fetch scenarios with event.respondWith");

        // Verify cache version is present
        content.Should().Contain("CACHE_VERSION", "service worker should have cache versioning");

        // Verify current cache version (v17 is the current version)
        content.Should().Contain("rtub-v",
            "service worker should use cache version to clear old caches");
    }

    [Fact]
    public async Task HomePage_ContainsManifestLink()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("<link rel=\"manifest\" href=\"/manifest.webmanifest\"",
            "home page should contain manifest link in head for Chrome/Android detection");
    }

    /// <summary>
    /// One manifest and one apple-mobile-web-app-title per page, the title agreeing with the
    /// manifest short_name, so RTUB declares a single identity and name to Safari and Chromium.
    /// </summary>
    [Fact]
    public async Task HomePage_DeclaresOneManifestAndOneAppleTitleMatchingShortName()
    {
        // Arrange & Act
        var html = await _client.GetStringAsync("/");
        using var manifest = JsonDocument.Parse(await _client.GetStringAsync("/manifest.webmanifest"));
        var shortName = manifest.RootElement.GetProperty("short_name").GetString();

        // Assert
        Regex.Matches(html, @"<link\b[^>]*\brel=""manifest""").Should().ContainSingle(
            "HTML uses only the first manifest link in tree order; a second one is dead or a silent override");

        var appleTitle = Regex.Matches(html, @"<meta\b[^>]*\bname=""apple-mobile-web-app-title""[^>]*>")
            .Should().ContainSingle("RTUB declares one Apple web app title").Which.Value;
        Regex.Match(appleTitle, @"\bcontent=""([^""]*)""").Groups[1].Value.Should().Be(shortName,
            "the Apple web app title and the manifest short_name must name the same app");
    }

    [Fact]
    public async Task ManifestIcons_192x192_IsAccessible()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/icons/rtub-logo-192.png");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "192x192 icon should be accessible");
        response.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
    }

    [Fact]
    public async Task ManifestIcons_512x512_IsAccessible()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/icons/rtub-logo-512.png");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "512x512 icon should be accessible");
        response.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
    }

    [Fact]
    public async Task AppleTouchIcon_IsAccessible()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/icons/rtub-logo-180.png");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Apple touch icon should still work");
        response.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
    }

    [Fact]
    public async Task HomePage_ContainsAppleTouchIconLink()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("apple-touch-icon",
            "home page should still contain Apple touch icon link (iOS compatibility maintained)");
    }
}

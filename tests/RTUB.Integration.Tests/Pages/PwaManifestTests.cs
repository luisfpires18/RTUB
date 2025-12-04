using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
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
        
        // Verify current cache version (v4 after the fix)
        content.Should().Contain("rtub-v4", 
            "service worker should use updated cache version to clear old caches");
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

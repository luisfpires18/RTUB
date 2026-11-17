// PwaManifestTests: ServiceWorker tests.
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

public partial class PwaManifestTests
{
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
    public async Task ServiceWorker_RespondsOnlyWhereItHasACache()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/service-worker.js");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verify fetch event listener exists
        content.Should().Contain("addEventListener('fetch'",
            "service worker must have fetch event listener for PWABuilder detection");

        // event.respondWith only where the worker has something to add (task 054): images (cache-first), scripts and
        // stylesheets (cache-first for hashed portal assets, stale-while-revalidate otherwise) and documents (network,
        // offline.html as the fallback). Everything else - /api, /auth, /hubs, /health, non-GET, non-http and the final
        // pass-through - returns without respondWith, so the browser fetches it as if no worker were installed and
        // the worker's start-up stays out of those requests. PWABuilder only needs respondWith to be present.
        var respondWithCount = System.Text.RegularExpressions.Regex.Matches(content, @"event\.respondWith").Count;
        respondWithCount.Should().Be(3, "images, scripts/styles and documents are the three branches that answer");
        content.Should().NotContain("event.respondWith(fetch(request))",
            "a bare pass-through through the worker only adds its start-up to the request");
        System.Text.RegularExpressions.Regex.IsMatch(content, @"isNeverCached\(url\.pathname\)\)\)\s*\{\s*return;")
            .Should().BeTrue("the never-cached guard must return without answering");

        // Verify cache version is present
        content.Should().Contain("CACHE_VERSION", "service worker should have cache versioning");

        // Verify current cache version (v17 is the current version)
        content.Should().Contain("rtub-v",
            "service worker should use cache version to clear old caches");
    }
}

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

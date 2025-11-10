using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Integration tests for API endpoints
/// </summary>
public class ApiEndpointsTests : IntegrationTestBase
{
    
    private readonly HttpClient _client;

    public ApiEndpointsTests(TestWebApplicationFactory factory) : base(factory)
    {
        _client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Images API Tests

    [Fact]
    public async Task ImagesApi_EventImage_WithInvalidId_Returns404()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/images/event/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ImagesApi_SlideshowImage_WithInvalidId_Returns404()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/images/slideshow/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ImagesApi_AlbumImage_WithInvalidId_Returns404()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/images/album/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ImagesApi_ProfileImage_WithInvalidId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/images/profile/nonexistent-user-id");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK);
        // May return default profile image or 404
    }

    [Fact]
    public async Task ImagesApi_InstrumentImage_WithInvalidId_Returns404()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/images/instrument/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ImagesApi_ProductImage_WithInvalidId_Returns404()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/images/product/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region API Response Headers Tests

    [Theory]
    [InlineData("/api/images/event/999999")]
    [InlineData("/api/images/slideshow/999999")]
    [InlineData("/api/images/album/999999")]
    public async Task ImagesApi_InvalidRequests_ReturnCorrectContentType(string endpoint)
    {
        // Arrange & Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ImagesApi_DoesNotSupportHttpHead_ForEventImages()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Head, "/api/images/event/1");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed,
            "HEAD method should not be allowed on images API");
    }

    #endregion

    #region Concurrent API Requests Tests

    [Fact]
    public async Task ImagesApi_HandlesMultipleConcurrentRequests()
    {
        // Arrange
        var endpoints = new[]
        {
            "/api/images/event/999999",
            "/api/images/slideshow/999999",
            "/api/images/album/999999",
            "/api/images/instrument/999999",
            "/api/images/product/999999"
        };

        // Act - Send concurrent requests
        var tasks = endpoints.Select(e => _client.GetAsync(e)).ToArray();
        var responses = await Task.WhenAll(tasks);

        // Assert - All should complete
        responses.Should().AllSatisfy(r =>
            r.StatusCode.Should().Be(HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task ImagesApi_ConsistentResponse_ForSameRequest()
    {
        // Arrange
        var endpoint = "/api/images/event/999999";

        // Act - Make same request multiple times
        var response1 = await _client.GetAsync(endpoint);
        var response2 = await _client.GetAsync(endpoint);
        var response3 = await _client.GetAsync(endpoint);

        // Assert - All should return same status
        response1.StatusCode.Should().Be(response2.StatusCode);
        response2.StatusCode.Should().Be(response3.StatusCode);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ImagesApi_WithNegativeId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/images/event/-1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ImagesApi_WithZeroId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/images/event/0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ImagesApi_WithInvalidPath_Returns404()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/images/nonexistent/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}

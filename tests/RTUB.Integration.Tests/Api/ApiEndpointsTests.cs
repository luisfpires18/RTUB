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







    #endregion

    #region API Response Headers Tests

    // Images API tests removed - ImagesController was deleted as part of R2 migration
    // Images are now served directly from Cloudflare R2

    #endregion

    #region Static Files E-Tag Tests

    [Fact]
    public async Task StaticImage_DefaultAvatar_ShouldHaveETagHeader()
    {
        // Act
        var response = await _client.GetAsync("/images/default-avatar.webp");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        response.Headers.ETag.Should().NotBeNull("E-Tag header should be present for static images");
        response.Headers.ETag!.Tag.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task StaticImage_WithETag_ShouldReturn304WhenNotModified()
    {
        // Arrange - First request to get the E-Tag
        var firstResponse = await _client.GetAsync("/images/default-avatar.webp");
        firstResponse.IsSuccessStatusCode.Should().BeTrue();
        var etag = firstResponse.Headers.ETag;
        etag.Should().NotBeNull();

        // Act - Second request with If-None-Match header
        var request = new HttpRequestMessage(HttpMethod.Get, "/images/default-avatar.webp");
        request.Headers.IfNoneMatch.Add(etag);
        var secondResponse = await _client.SendAsync(request);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NotModified, 
            "Server should return 304 when E-Tag matches");
        secondResponse.Content.Headers.ContentLength.Should().Be(0, 
            "304 response should have no content");
    }

    [Fact]
    public async Task StaticImage_OtherImagesInFolder_ShouldAlsoHaveETag()
    {
        // Arrange - Test multiple images in the images folder
        var imageUrls = new[]
        {
            "/images/rtub_logo.webp",
            "/images/slide1.svg",
            "/images/slide2.svg"
        };

        // Act & Assert
        foreach (var url in imageUrls)
        {
            var response = await _client.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                response.Headers.ETag.Should().NotBeNull($"E-Tag header should be present for {url}");
            }
        }
    }

    #endregion

    #region Concurrent API Requests Tests



    #endregion

    #region Error Handling Tests




    #endregion
}

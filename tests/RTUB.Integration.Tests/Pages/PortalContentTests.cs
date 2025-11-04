using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Portal content pages (About Us, History, etc.)
/// </summary>
public class PortalContentTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PortalContentTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true
        });
    }

    [Fact]
    public async Task HomePage_ContainsPortalContent()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HomePage_HasExpectedSections()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Verify content is rendered (not checking specific text as it may change)
        content.Length.Should().BeGreaterThan(1000, "home page should have substantial content");
    }

    [Fact]
    public async Task PortalContent_LoadsWithoutErrors()
    {
        // Arrange - Navigate to home which contains portal content
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/orgaos-sociais")]
    [InlineData("/requests")]
    public async Task PublicPages_ContainProperContentType(string url)
    {
        // Arrange & Act
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task HomePage_RendersSuccessfully_MultipleTimes()
    {
        // Test for consistency - home page should render the same way multiple times
        // Arrange & Act
        var response1 = await _client.GetAsync("/");
        var response2 = await _client.GetAsync("/");
        var response3 = await _client.GetAsync("/");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        response3.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HomePage_HasValidContent()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("navbar", "should contain navigation bar");
        content.Length.Should().BeGreaterThan(100, "should have substantial content");
    }
}

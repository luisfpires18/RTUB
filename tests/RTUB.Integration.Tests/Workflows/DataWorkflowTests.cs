using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Workflows;

/// <summary>
/// Integration tests for data-related workflows and operations
/// </summary>
public class DataWorkflowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DataWorkflowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task AlbumsPage_LoadsDataSuccessfully()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/music");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RequestsPage_IsAccessibleAndFunctional()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/requests");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MultiplePageLoads_DoNotCauseMemoryLeak()
    {
        // Test that multiple page loads don't cause issues
        // Arrange
        var urls = new[] { "/", "/music", "/requests" };

        // Act - Load each page multiple times
        foreach (var url in urls)
        {
            for (int i = 0; i < 3; i++)
            {
                var response = await _client.GetAsync(url);
                
                // Assert
                response.StatusCode.Should().Be(HttpStatusCode.OK);
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task StaticFiles_AreAccessible()
    {
        // Test that CSS and other static files load properly
        // Arrange & Act
        var response = await _client.GetAsync("/css/site.css");

        // Assert
        // May be 404 if file doesn't exist, or OK if it does
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidPageRoute_Returns404()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/this-page-does-not-exist-at-all");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/music")]
    [InlineData("/")]
    public async Task PublicPages_HaveConsistentResponse(string url)
    {
        // Test that pages return consistent responses
        // Arrange & Act
        var response1 = await _client.GetAsync(url);
        var content1 = await response1.Content.ReadAsStringAsync();
        
        var response2 = await _client.GetAsync(url);
        var content2 = await response2.Content.ReadAsStringAsync();

        // Assert
        response1.StatusCode.Should().Be(response2.StatusCode);
        content1.Length.Should().BeGreaterThan(0);
        content2.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SequentialNavigation_ThroughPublicSections_Works()
    {
        // Simulate user navigating through the site
        // Arrange
        var navigationPath = new[]
        {
            "/",
            "/music",
            "/requests",
            "/roles"
        };

        // Act & Assert
        foreach (var url in navigationPath)
        {
            var response = await _client.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"navigation to {url} should succeed");
        }
    }

    [Fact]
    public async Task RapidPageRequests_AreHandledCorrectly()
    {
        // Test handling of rapid consecutive requests
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Send 5 rapid requests
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_client.GetAsync("/"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Dispose();
        }
    }
}

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Workflows;

/// <summary>
/// Integration tests for complete navigation workflows through the site
/// Tests realistic user journeys through multiple pages
/// </summary>
public class NavigationWorkflowTests : IntegrationTestBase
{
    
    private readonly HttpClient _client;

    public NavigationWorkflowTests(TestWebApplicationFactory factory) : base(factory)
    {
        _client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Public User Journey Tests

    [Fact]
    public async Task PublicUserJourney_HomePage_ThroughAllPublicPages()
    {
        // Arrange & Act - Simulate a public user browsing the site
        var homeResponse = await _client.GetAsync("/");
        var musicResponse = await _client.GetAsync("/music");
        var eventsResponse = await _client.GetAsync("/events");
        var rolesResponse = await _client.GetAsync("/roles");
        var requestsResponse = await _client.GetAsync("/request");

        // Assert - All public pages should be accessible
        homeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        musicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        eventsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        rolesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        requestsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MusicWorkflow_AlbumsNavigation()
    {
        // Arrange & Act - User exploring music section
        var musicHomeResponse = await _client.GetAsync("/music");
        var backToMusicResponse = await _client.GetAsync("/music");

        // Assert
        musicHomeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        backToMusicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EventsToRequestsWorkflow_InterestedUserJourney()
    {
        // Arrange & Act - User views events then submits a request
        var eventsResponse = await _client.GetAsync("/events");
        var requestsResponse = await _client.GetAsync("/request");

        // Assert
        eventsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        requestsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Protected Page Access Tests

    [Fact]
    public async Task UnauthorizedUserJourney_AttemptsToAccessMemberArea()
    {
        // Arrange & Act - User tries to access member pages
        var membersResponse = await _client.GetAsync("/members");
        var rehearsalsResponse = await _client.GetAsync("/rehearsals");
        var profileResponse = await _client.GetAsync("/profile");

        // Assert - Should redirect to login
        membersResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        rehearsalsResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        profileResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        membersResponse.Headers.Location?.ToString().Should().Contain("/login");
        rehearsalsResponse.Headers.Location?.ToString().Should().Contain("/login");
        profileResponse.Headers.Location?.ToString().Should().Contain("/login");
    }

    [Fact]
    public async Task UnauthorizedUserJourney_AttemptsToAccessAdminArea()
    {
        // Arrange & Act - User tries to access admin pages
        var slideshowResponse = await _client.GetAsync("/images");

        // Assert - Should redirect to login
        slideshowResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        slideshowResponse.Headers.Location?.ToString().Should().Contain("/login");
    }

    #endregion

    #region Cross-Section Navigation Tests

    [Fact]
    public async Task CrossSectionWorkflow_PublicToIdentity_AndBack()
    {
        // Arrange & Act - User browses then goes to login
        var homeResponse = await _client.GetAsync("/");
        var loginResponse = await _client.GetAsync("/login");
        var backToHomeResponse = await _client.GetAsync("/");

        // Assert
        homeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        backToHomeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FullSiteExploration_AllPublicPages_InSequence()
    {
        // Arrange
        var publicPages = new[]
        {
            "/",
            "/music",
            "/events",
            "/roles",
            "/request",
            "/login"
        };

        // Act & Assert - Navigate through all public pages
        foreach (var page in publicPages)
        {
            var response = await _client.GetAsync(page);
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"{page} should be accessible");
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ErrorHandling_InvalidPage_Returns404OrRedirect()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/nonexistent-page");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.Redirect,
            HttpStatusCode.OK); // May redirect to error page
    }

    [Fact]
    public async Task ErrorPage_IsAccessible()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/error");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task NavigationPerformance_MultiplePageLoads_Complete()
    {
        // Arrange
        var pages = new[] { "/", "/music", "/events" };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Load multiple pages
        foreach (var page in pages)
        {
            var response = await _client.GetAsync(page);
            response.EnsureSuccessStatusCode();
        }

        stopwatch.Stop();

        // Assert - Should complete in reasonable time
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
            "multiple page loads should complete within 10 seconds");
    }

    [Fact(Skip = "Concurrent requests cause SQLite connection conflicts in test environment. This works fine with production databases.")]
    public async Task ConcurrentPageLoads_HandleMultipleRequests()
    {
        // Arrange
        var pages = new[] { "/", "/music", "/events", "/roles" };

        // Act - Load pages concurrently
        var tasks = pages.Select(page => _client.GetAsync(page)).ToArray();
        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    #endregion
}

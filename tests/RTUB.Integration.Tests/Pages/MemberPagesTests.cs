using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Member pages (requires authentication)
/// Tests page accessibility and content without full authentication
/// </summary>
public class MemberPagesTests : IntegrationTestBase
{
    
    private readonly HttpClient _client;

    public MemberPagesTests(TestWebApplicationFactory factory) : base(factory)
    {
        _client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Members Page Tests

    [Fact]
    public async Task MembersPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/members");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    [Fact]
    public async Task MembersPage_RedirectsWithReturnUrl()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/members");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.Should().Contain("/login");
        location.Should().Contain("ReturnUrl");
    }

    #endregion

    #region Hierarchy Page Tests

    [Fact]
    public async Task HierarchyPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/hierarchy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    #endregion

    #region Rehearsals Page Tests

    [Fact]
    public async Task RehearsalsPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/rehearsals");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    #endregion

    #region Inventory Page Tests

    [Fact]
    public async Task InventoryPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/inventory");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    [Fact]
    public async Task InventoryPage_RedirectsWithReturnUrl()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/inventory");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.Should().Contain("/login");
        location.Should().Contain("ReturnUrl");
    }

    #endregion

    #region Profile Page Tests

    [Fact]
    public async Task ProfilePage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    #endregion

    #region Event Discussion Page Tests

    [Fact]
    public async Task EventDiscussionPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/events/1/discussion");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(999)]
    public async Task EventDiscussionPage_WithVariousEventIds_RedirectsToLogin(int eventId)
    {
        // Arrange & Act
        var response = await _client.GetAsync($"/events/{eventId}/discussion");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    #endregion

    #region Authorization Tests

    [Theory]
    [InlineData("/members")]
    [InlineData("/hierarchy")]
    [InlineData("/rehearsals")]
    [InlineData("/profile")]
    [InlineData("/events/1/discussion")]
    public async Task MemberPages_RequireAuthentication(string url)
    {
        // Arrange & Act
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect,
            $"{url} should redirect unauthenticated users");
        response.Headers.Location?.ToString().Should().Contain("/login",
            $"{url} should redirect to login page");
    }

    [Fact]
    public async Task MemberPages_AllRequireAuthenticationInSequence()
    {
        // Arrange
        var memberUrls = new[] { "/members", "/hierarchy", "/rehearsals", "/profile", "/events/1/discussion" };

        // Act & Assert
        foreach (var url in memberUrls)
        {
            var response = await _client.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect,
                $"{url} should require authentication");
        }
    }

    #endregion
}

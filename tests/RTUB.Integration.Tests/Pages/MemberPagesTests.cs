using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
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
        location.Should().ContainEquivalentOf("returnUrl=%2Fmembers", "017: the React /members sends visitors to sign in and back");
    }

    #endregion

    #region Hierarchy Page Tests

    [Fact]
    public async Task HierarchyPage_IsRetired_AndRedirectsToTheReactHierarchy()
    {
        // 017: the Blazor /hierarchy is the React /members/hierarchy.
        var response = await _client.GetAsync("/hierarchy");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Be("/members/hierarchy");
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
        location.Should().Contain("returnUrl=%2Finventory", "the React /inventory sends visitors to sign in and back (020)");
    }

    #endregion

    #region Profile Page Tests

    [Fact]
    public async Task OldProfileUrl_RedirectsToTheReactProfile()
    {
        // The Blazor /member/profile was retired in 032: the React /profile edits the profile (and shows a visitor
        // how to sign in).
        var response = await _client.GetAsync("/member/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Be("/profile");
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
    [InlineData("/members/hierarchy")]
    [InlineData("/rehearsals")]
    [InlineData("/members/map")]
    [InlineData("/hall-of-fame")]
    [InlineData("/naipes")]
    [InlineData("/naipes/config")]
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
        var memberUrls = new[] { "/members", "/members/hierarchy", "/rehearsals", "/members/map", "/hall-of-fame", "/naipes", "/naipes/config", "/events/1/discussion" };

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

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Admin pages (requires Admin role)
/// </summary>
public class AdminPagesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AdminPagesTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Finance Page Tests

    [Fact]
    public async Task FinancePage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/admin/finance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    #endregion

    #region Roles Page Tests

    [Fact]
    public async Task RolesPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/admin/roles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    #endregion

    #region Slideshow Management Tests

    [Fact]
    public async Task SlideshowManagementPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/admin/slideshow-management");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    #endregion

    #region Labels Management Tests

    [Fact]
    public async Task LabelsPage_RedirectsToOwnerLabels()
    {
        // Arrange & Act - /admin/labels should redirect to /owner/labels
        var response = await _client.GetAsync("/admin/labels");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/owner/labels");
    }

    #endregion

    #region Requests Management Tests

    [Fact]
    public async Task AdminRequestsPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/admin/requests");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    #endregion

    #region Rehearsal Management Tests

    [Fact]
    public async Task RehearsalManagementPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/admin/rehearsals");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    #endregion

    #region Authorization Tests

    [Theory]
    [InlineData("/admin/finance")]
    [InlineData("/admin/roles")]
    [InlineData("/admin/slideshow-management")]
    [InlineData("/admin/requests")]
    [InlineData("/admin/rehearsals")]
    public async Task AdminPages_RequireAuthentication(string url)
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
    public async Task AdminPages_AllRequireAuthenticationInSequence()
    {
        // Arrange
        var adminUrls = new[]
        {
            "/admin/finance",
            "/admin/roles",
            "/admin/slideshow-management",
            "/admin/requests",
            "/admin/rehearsals",
        };

        // Act & Assert
        foreach (var url in adminUrls)
        {
            var response = await _client.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect,
                $"{url} should require authentication");
        }
    }

    [Fact]
    public async Task AdminNavigation_ThroughAllAdminPages_Works()
    {
        // Arrange
        var adminUrls = new[]
        {
            "/admin/finance",
            "/admin/roles",
            "/admin/slideshow-management"
        };

        // Act & Assert - All should redirect (requires auth)
        foreach (var url in adminUrls)
        {
            var response = await _client.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().Should().Contain("/login");
        }
    }

    #endregion
}

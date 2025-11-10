using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Admin pages (requires Admin role)
/// </summary>
public class AdminPagesTests : IntegrationTestBase
{
    
    private readonly HttpClient _client;

    public AdminPagesTests(TestWebApplicationFactory factory) : base(factory)
    {
        _client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Finance Page Tests

    [Fact]
    public async Task NewFinancePage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act - New /finance page requires authentication
        var response = await _client.GetAsync("/finance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    #endregion

    #region Roles Page Tests

    [Fact]
    public async Task RolesPage_WithoutAuth_ReturnsNotFound()
    {
        // Arrange & Act - /admin/roles has been removed, consolidated into /roles
        var response = await _client.GetAsync("/admin/roles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    #region Authorization Tests

    [Theory]
    [InlineData("/admin/slideshow-management")]
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
        // Arrange - Updated to use new finance route
        var adminUrls = new[]
        {
            "/admin/slideshow-management",
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
        var adminUrls = new[]
        {
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

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Owner pages (requires Owner role)
/// </summary>
public class OwnerPagesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public OwnerPagesTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region User Roles Page Tests

    [Fact]
    public async Task UserRolesPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/owner/user-roles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/Account/Login");
    }

    [Fact]
    public async Task UserRolesPage_RedirectsWithReturnUrl()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/owner/user-roles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.Should().Contain("/Account/Login");
        location.Should().Contain("ReturnUrl");
    }

    #endregion

    #region Audit Log/Tracing Page Tests

    [Fact]
    public async Task TracingPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/owner/tracing");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/Account/Login");
    }

    [Fact]
    public async Task TracingPage_RedirectsWithReturnUrl()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/owner/tracing");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.Should().Contain("/Account/Login");
        location.Should().Contain("ReturnUrl");
    }

    #endregion

    #region Authorization Tests

    [Theory]
    [InlineData("/owner/user-roles")]
    [InlineData("/owner/tracing")]
    public async Task OwnerPages_RequireAuthentication(string url)
    {
        // Arrange & Act
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect,
            $"{url} should redirect unauthenticated users");
        response.Headers.Location?.ToString().Should().Contain("/Account/Login",
            $"{url} should redirect to login page");
    }

    [Fact]
    public async Task OwnerPages_AllRequireAuthenticationInSequence()
    {
        // Arrange
        var ownerUrls = new[] { "/owner/user-roles", "/owner/tracing" };

        // Act & Assert
        foreach (var url in ownerUrls)
        {
            var response = await _client.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect,
                $"{url} should require authentication");
        }
    }

    [Fact]
    public async Task OwnerNavigation_BetweenOwnerPages_Works()
    {
        // Arrange & Act - Navigate between owner pages
        var userRolesResponse = await _client.GetAsync("/owner/user-roles");
        var tracingResponse = await _client.GetAsync("/owner/tracing");

        // Assert - Both should redirect (requires owner role)
        userRolesResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        tracingResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        userRolesResponse.Headers.Location?.ToString().Should().Contain("/Account/Login");
        tracingResponse.Headers.Location?.ToString().Should().Contain("/Account/Login");
    }

    #endregion
}

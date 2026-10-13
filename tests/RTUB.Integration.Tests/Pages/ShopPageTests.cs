using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Shop page (requires authentication)
/// Tests that /shop requires login and redirects unauthenticated users
/// </summary>
public class ShopPageTests : IntegrationTestBase
{

    private readonly HttpClient _client;

    public ShopPageTests(TestWebApplicationFactory factory) : base(factory)
    {
        _client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task ShopPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/shop");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    [Fact]
    public async Task ShopPage_RedirectsWithReturnUrl()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/shop");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.Should().Contain("/login");
        location.Should().Contain("returnUrl=%2Fshop", "the React /shop sends visitors to sign in and back (021)");
    }

    [Fact]
    public async Task ShopPage_RequiresAuthentication()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/shop");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect,
            "/shop should redirect unauthenticated users");
        response.Headers.Location?.ToString().Should().Contain("/login",
            "/shop should redirect to login page");
    }
}

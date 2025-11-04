using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Member pages (requires authentication)
/// Tests page accessibility and content without full authentication
/// </summary>
public class MemberPagesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MemberPagesTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
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
        response.Headers.Location?.ToString().Should().Contain("/Account/Login");
    }

    [Fact]
    public async Task MembersPage_RedirectsWithReturnUrl()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/members");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.Should().Contain("/Account/Login");
        location.Should().Contain("ReturnUrl");
    }

    #endregion

    #region Hierarquia Page Tests

    [Fact]
    public async Task HierarquiaPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/member/hierarquia");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/Account/Login");
    }

    #endregion

    #region Rehearsals Page Tests

    [Fact]
    public async Task RehearsalsPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/ensaios");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/Account/Login");
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
        response.Headers.Location?.ToString().Should().Contain("/Account/Login");
    }

    #endregion

    #region Authorization Tests

    [Theory]
    [InlineData("/members")]
    [InlineData("/member/hierarquia")]
    [InlineData("/ensaios")]
    [InlineData("/profile")]
    public async Task MemberPages_RequireAuthentication(string url)
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
    public async Task MemberPages_AllRequireAuthenticationInSequence()
    {
        // Arrange
        var memberUrls = new[] { "/members", "/member/hierarquia", "/ensaios", "/profile" };

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

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Identity pages (Login, ForgotPassword, etc.)
/// </summary>
public class IdentityPagesTests : IntegrationTestBase
{
    
    private readonly HttpClient _client;

    public IdentityPagesTests(TestWebApplicationFactory factory) : base(factory)
    {
        _client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Login Page Tests

    [Fact]
    public async Task LoginPage_ReturnsSuccessStatusCode()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/login");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginPage_ContainsLoginForm()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/login");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("Entrar", "page should contain Login/Entrar text");
        content.Should().Contain("form", "page should contain a form");
    }

    [Fact]
    public async Task LoginPage_HasCorrectContentType()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/login");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    #endregion

    #region Forgot Password Page Tests

    [Fact]
    public async Task ForgotPasswordPage_ReturnsSuccessStatusCode()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/forgot-password");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPasswordPage_ContainsExpectedContent()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/forgot-password");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("palavra-passe", "page should contain password reset text");
    }

    #endregion

    #region Reset Password Page Tests

    [Fact]
    public async Task ResetPasswordPage_ReturnsSuccessOrRedirect()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/reset-password");

        // Assert - May redirect if no token provided
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    #endregion

    #region Confirm Email Page Tests

    [Fact]
    public async Task ConfirmEmailPage_ReturnsSuccessOrRedirect()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/confirm-email");

        // Assert - May redirect if no token provided
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    #endregion

    #region Navigation Tests

    [Fact]
    public async Task IdentityPages_LoginToForgotPassword_Works()
    {
        // Arrange - Load login page first
        var loginResponse = await _client.GetAsync("/login");

        // Act - Navigate to forgot password
        var forgotPasswordResponse = await _client.GetAsync("/forgot-password");

        // Assert
        loginResponse.IsSuccessStatusCode.Should().BeTrue();
        forgotPasswordResponse.IsSuccessStatusCode.Should().BeTrue();
    }

    [Theory]
    [InlineData("/login")]
    [InlineData("/forgot-password")]
    public async Task IdentityPages_ArePubliclyAccessible(string url)
    {
        // Arrange & Act
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"{url} should be accessible without authentication");
    }

    #endregion
}

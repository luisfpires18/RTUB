using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// Integration tests for Finance pages (requires authentication)
/// Tests page accessibility and authorization
/// </summary>
public class FinancePagesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public FinancePagesTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Finance List Page Tests

    [Fact]
    public async Task FinancePage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/finance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    [Fact]
    public async Task FinancePage_RedirectsWithReturnUrl()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/finance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.Should().Contain("/login");
        location.Should().Contain("ReturnUrl");
    }

    #endregion

    #region Finance Report Details Page Tests

    [Fact]
    public async Task ReportDetailsPage_WithoutAuth_RedirectsToLogin()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/finance/report/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/login");
    }

    [Fact]
    public async Task ReportDetailsPage_RedirectsWithReturnUrl()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/finance/report/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.Should().Contain("/login");
        location.Should().Contain("ReturnUrl");
    }

    #endregion

    #region Old Admin Finance Page Tests

    [Fact]
    public async Task OldAdminFinancePage_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/admin/finance");

        // Assert
        // The old admin finance page should either redirect or return 404
        // since it was deleted and replaced with /finance
        (response.StatusCode == HttpStatusCode.NotFound || 
         response.StatusCode == HttpStatusCode.Redirect)
            .Should().BeTrue("Old admin finance page should not be accessible");
    }

    #endregion

    #region Authorization Tests

    [Theory]
    [InlineData("/finance")]
    [InlineData("/finance/report/1")]
    public async Task FinancePages_RequireAuthentication(string url)
    {
        // Arrange & Act
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect,
            $"{url} should redirect unauthenticated users");
        response.Headers.Location?.ToString().Should().Contain("/login",
            $"{url} should redirect to login page");
    }

    #endregion
}

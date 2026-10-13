using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// The public identity pages (React since task 036): /login, /forgot-password, /reset-password and /confirm-email are
/// open to visitors. The flows behind them are covered by AccountRecoveryApiTests.
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

    [Theory]
    [InlineData("/login")]
    [InlineData("/forgot-password")]
    [InlineData("/reset-password")]
    [InlineData("/confirm-email")]
    public async Task IdentityPages_ArePubliclyAccessibleReactPages(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url} should be accessible without authentication");
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync()).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");
    }

    [Theory]
    [InlineData("/forgot-password")]
    [InlineData("/reset-password")]
    [InlineData("/confirm-email")]
    public async Task IdentityPages_AreGetOrHeadOnly(string url)
    {
        (await _client.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// The Owner tools (React since task 035): a visitor is sent to sign in and comes back to the page. Who else gets in is
/// covered by <c>OwnerOperationsApiTests</c>.
/// </summary>
public class OwnerPagesTests : IntegrationTestBase
{
    private readonly HttpClient _client;

    public OwnerPagesTests(TestWebApplicationFactory factory) : base(factory)
    {
        _client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Theory]
    [InlineData("/users")]
    [InlineData("/owner/tracing")]
    [InlineData("/owner/db")]
    [InlineData("/emails")]
    [InlineData("/notifications")]
    public async Task OwnerPages_SendVisitorsToSignIn_AndBack(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, $"{url} should redirect unauthenticated users");
        response.Headers.Location?.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString(url));
    }

    [Theory]
    [InlineData("/users")]
    [InlineData("/owner/tracing")]
    [InlineData("/owner/db")]
    [InlineData("/emails")]
    [InlineData("/notifications")]
    public async Task OwnerPages_AreGetOrHeadOnly(string url)
    {
        (await _client.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}

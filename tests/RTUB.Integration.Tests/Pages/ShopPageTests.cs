using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

public class ShopPageTests : IntegrationTestBase
{
    private readonly HttpClient _client;

    public ShopPageTests(TestWebApplicationFactory factory) : base(factory)
    {
        _client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task ShopPage_IsAvailableToVisitorsAndMembers()
    {
        var visitorResponse = await _client.GetAsync("/shop");
        visitorResponse.EnsureSuccessStatusCode();
        (await visitorResponse.Content.ReadAsStringAsync()).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");

    }
}

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Task 163: with <c>App:BaseUrl</c> = https://www.rtub.test, a request to rtub.test (no www) is sent permanently (308)
/// to the www address with its path and query; www itself, and any other host, are answered normally.
/// </summary>
public class CanonicalHostTests : IClassFixture<AccountRecoveryFactory>
{
    private readonly AccountRecoveryFactory _factory;

    public CanonicalHostTests(AccountRecoveryFactory factory) => _factory = factory;

    private HttpClient Client(string host) =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"https://{host}"), AllowAutoRedirect = false });

    [Theory]
    [InlineData("/trintuna?almoco=5", "https://www.rtub.test/trintuna?almoco=5")]
    [InlineData("/login", "https://www.rtub.test/login")]
    [InlineData("/", "https://www.rtub.test/")]
    public async Task BareDomain_RedirectsPermanentlyToWww_KeepingPathAndQuery(string path, string expected)
    {
        var response = await Client("rtub.test").GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
        response.Headers.Location!.ToString().Should().Be(expected);
    }

    [Fact]
    public async Task BareDomain_KeepsTheMethod_OnAPost()
    {
        var response = await Client("rtub.test").PostAsync("/api/account/login", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect, "308 keeps POST, so a sign-in is not turned into a GET");
        response.Headers.Location!.ToString().Should().Be("https://www.rtub.test/api/account/login");
    }

    [Theory]
    [InlineData("www.rtub.test")]
    [InlineData("rtub-dev.azurewebsites.net")]
    [InlineData("localhost")]
    public async Task OtherHosts_AreNotRedirected(string host)
    {
        var response = await Client(host).GetAsync("/login");

        response.StatusCode.Should().NotBe(HttpStatusCode.PermanentRedirect);
        response.Headers.Location.Should().BeNull();
    }
}

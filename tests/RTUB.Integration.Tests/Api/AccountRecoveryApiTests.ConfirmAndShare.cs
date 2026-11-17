// AccountRecoveryApiTests: ConfirmAndShare tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests.Api;

public partial class AccountRecoveryApiTests
{
    // ---------- confirm-email ----------

    [Fact]
    public async Task Confirm_WithAValidToken_ConfirmsTheAddress_AndSignsNobodyIn()
    {
        var client = await VisitorAsync();
        var user = await UserAsync(confirmed: false);
        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            token = await users.GenerateEmailConfirmationTokenAsync((await users.FindByIdAsync(user.Id))!);
        }

        // The old page decoded the link's code twice (the query, then UrlDecode): the code arrives encoded once more.
        var response = await client.PostAsJsonAsync("/api/account/confirm-email", new { userId = user.Id, code = WebUtilityEncode(token) });
        var result = await AreaHttp.Json(response);
        result.GetProperty("succeeded").GetBoolean().Should().BeTrue();
        result.GetProperty("message").GetString().Should().Be("Obrigado por confirmar o seu email.");
        SetCookies(response).Should().NotContain(c => c.StartsWith(".AspNetCore.Identity.Application"));
        (await StoredAsync(user.Id)).EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_HandlesBadLinksSafely()
    {
        var client = await VisitorAsync();
        var user = await UserAsync(confirmed: false);

        (await AreaHttp.Json(await client.PostAsJsonAsync("/api/account/confirm-email", new { userId = (string?)null, code = (string?)null })))
            .GetProperty("message").GetString().Should().Be("Link de confirmação inválido.");
        (await AreaHttp.Json(await client.PostAsJsonAsync("/api/account/confirm-email", new { userId = "nao-existe", code = "x" })))
            .GetProperty("message").GetString().Should().Be("Utilizador não encontrado.");
        var bad = await AreaHttp.Json(await client.PostAsJsonAsync("/api/account/confirm-email", new { userId = user.Id, code = "nao-e-um-token" }));
        bad.GetProperty("succeeded").GetBoolean().Should().BeFalse();
        bad.GetProperty("message").GetString().Should().Be("Erro ao confirmar o seu email.");
        (await StoredAsync(user.Id)).EmailConfirmed.Should().BeFalse();
    }

    // ---------- /Error and /share ----------

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task Error_IsAFixedServerPage_ForAnyMethod_WithNoDetails(string method)
    {
        var client = AreaHttp.Anonymous(_factory, NextIp());

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/Error?secret=abc123"));

        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Algo correu mal").And.Contain("href=\"/\"")
            .And.NotContain("abc123").And.NotContain("Exception").And.NotContain("<script").And.NotContain("blazor.web.js");
    }

    /// <summary>053: /share is the React Web Share Target page, public, GET/HEAD only (the share_target method is GET).</summary>
    [Theory]
    [InlineData("/share")]
    [InlineData("/share?title=x&text=y&url=z")]
    public async Task Share_IsThePublicReactShell(string path)
    {
        var client = AreaHttp.Anonymous(_factory, NextIp());

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        response.Headers.CacheControl!.NoCache.Should().BeTrue("the shell must point at the current deploy's assets");
        (await response.Content.ReadAsStringAsync()).Should().Contain("id=\"root\"").And.NotContain("x&text=y", "the shell never echoes the query");
    }

    [Fact]
    public async Task Share_RefusesPost()
    {
        var client = AreaHttp.Anonymous(_factory, NextIp());

        (await client.PostAsync("/share", content: null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task TheInstalledApp_IsAShareTarget()
    {
        var client = AreaHttp.Anonymous(_factory, NextIp());

        using var manifest = JsonDocument.Parse(await client.GetStringAsync("/manifest.webmanifest"));
        var share = manifest.RootElement.GetProperty("share_target");
        share.GetProperty("action").GetString().Should().Be("/share");
        share.GetProperty("method").GetString().Should().Be("GET");
        (await client.GetStringAsync("/service-worker.js")).Should().NotContain("share", "a GET share target needs no worker handling");
    }
}

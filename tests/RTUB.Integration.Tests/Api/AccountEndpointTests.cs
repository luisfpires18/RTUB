using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Enums;
using Xunit;
using ApplicationUser = RTUB.Core.Entities.ApplicationUser;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// <c>GET /api/account/me</c> - session state for the React portal (React track 002). It describes
/// the caller only, with a fixed, non-sensitive set of fields, is never cached, and cannot be used
/// to change anything.
/// </summary>
public class AccountEndpointTests : IntegrationTestBase
{
    private const string Me = "/api/account/me";

    public AccountEndpointTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Anonymous_IsToldOnlyThatItIsNotSignedIn()
    {
        var response = await Factory.CreateClient().GetAsync(Me);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("authenticated");
        json.RootElement.GetProperty("authenticated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task SignedIn_GetsTheirOwnSummaryAndNothingSensitive()
    {
        var client = await SignInAsAdminAsync();

        var response = await client.GetAsync(Me);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue("a cached answer could outlive sign-out");
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        json.RootElement.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo("authenticated", "displayName", "fullName", "avatarUrl", "categories", "menu");
        json.RootElement.GetProperty("authenticated").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("displayName").GetString().Should().NotBeNullOrWhiteSpace();
        body.Should().NotContain("testadmin@test.com", "no contact details leave this endpoint");
        body.Should().NotContain("\"roles\"", "roles are not listed; menu only says which menu groups to show");
        Menu(json).Should().Equal(("management", true), ("logisticsAndTreasury", true), ("admin", true), ("owner", true));
    }

    /// <summary>
    /// 030: the React member menu shows a group only where the Blazor navbar did (MemberMenuAccess). The pages and APIs
    /// still enforce their own rules; this only hides links.
    /// </summary>
    [Theory]
    [InlineData(null, null, true, false, false, false)]
    [InlineData(null, MemberCategory.Leitao, false, false, false, false)]
    [InlineData(null, MemberCategory.Tuno, true, true, false, false)]
    [InlineData("Mod", MemberCategory.Leitao, true, true, false, false)]
    [InlineData("Admin", null, true, true, true, false)]
    [InlineData("Owner", null, true, true, false, true)]
    public async Task Menu_ShowsEachGroupOnlyWhereTheNavbarDid(
        string? role, MemberCategory? category, bool management, bool logisticsAndTreasury, bool admin, bool owner)
    {
        var name = $"menu{Guid.NewGuid():N}"[..20];
        var (client, user) = await CookieTestSession.SignInAsync(Factory, name, $"10.97.0.{Interlocked.Increment(ref _ip)}", role);
        if (category is { } c)
        {
            using var scope = Factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await users.FindByIdAsync(user.Id);
            stored!.Categories = [c];
            (await users.UpdateAsync(stored)).Succeeded.Should().BeTrue();
        }

        using var json = JsonDocument.Parse(await client.GetStringAsync(Me));

        Menu(json).Should().Equal(
            ("management", management), ("logisticsAndTreasury", logisticsAndTreasury), ("admin", admin), ("owner", owner));
    }

    private static int _ip;

    private static List<(string, bool)> Menu(JsonDocument json) =>
        json.RootElement.GetProperty("menu").EnumerateObject().Select(p => (p.Name, p.Value.GetBoolean())).ToList();

    [Fact]
    public async Task ExpelledMember_IsReportedAsSignedOut()
    {
        var client = await SignInAsAdminAsync();
        await SetExpelledAsync(true);
        try
        {
            using var json = JsonDocument.Parse(await client.GetStringAsync(Me));
            json.RootElement.GetProperty("authenticated").GetBoolean().Should().BeFalse();
        }
        finally
        {
            await SetExpelledAsync(false);
        }
    }

    [Fact]
    public async Task IsReadOnly()
    {
        var client = await SignInAsAdminAsync();

        var response = await client.PostAsync(Me, null);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    private async Task SetExpelledAsync(bool expelled)
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByNameAsync("testadmin");
        admin!.IsExpelled = expelled;
        (await users.UpdateAsync(admin)).Succeeded.Should().BeTrue();
    }

    private async Task<HttpClient> SignInAsAdminAsync()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var token = await AntiforgeryFormToken.FetchAsync(client);

        var response = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [AntiforgeryFormToken.FieldName] = token!,
            ["Username"] = "testadmin",
            ["Password"] = Factory.AdminPassword,
            ["RememberMe"] = "false"
        }));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "the seeded admin must sign in");
        return client;
    }
}

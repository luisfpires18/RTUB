using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace RTUB.Integration.Tests.Pages;

/// <summary>
/// The After Hours feature gate through the real host: real Identity, the real login form, the
/// real endpoint authorization and the prerendered layout. One host per configuration state,
/// because <c>AfterHours:Enabled</c> is read from configuration.
/// </summary>
public abstract class AfterHoursGateTestsBase
{
    /// <summary>Text only the landing page renders; the nav entry says just "After Hours".</summary>
    protected const string LandingMarker = "A text-based crime RPG for RTUB members.";

    protected const string NavLink = "href=\"/after-hours\"";

    /// <summary>
    /// Same endpoint, different spelling: case, trailing slash, query string. The number is the
    /// last octet of the client IP, giving each sign-in its own login rate-limiting partition.
    /// </summary>
    public static TheoryData<string, int> RouteVariants => new()
    {
        { "/after-hours", 11 },
        { "/after-hours/", 12 },
        { "/AFTER-HOURS", 13 },
        { "/After-Hours/", 14 },
        { "/after-hours?tab=home", 15 },
    };

    protected readonly TestWebApplicationFactory Factory;

    protected AfterHoursGateTestsBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
    }

    protected HttpClient AnonymousClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    protected static async Task<string> ReadBodyAsync(HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    /// <summary>
    /// A signed-in user refused by endpoint authorization: Forbid, which the cookie handler turns
    /// into a redirect to AccessDeniedPath (/login). The landing page must never be rendered.
    /// </summary>
    protected static async Task ShouldBeRefusedAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Forbidden);
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            response.Headers.Location!.ToString().Should().Contain("/login", "AccessDeniedPath is /login");
        }
        (await ReadBodyAsync(response)).Should().NotContain(LandingMarker);
    }
}

/// <summary>
/// Every behaviour that must hold while After Hours is disabled, run once per way of being
/// disabled (key missing, key false).
/// </summary>
public abstract class AfterHoursDisabledTestsBase : AfterHoursGateTestsBase
{
    private readonly string _ipPrefix;

    protected AfterHoursDisabledTestsBase(TestWebApplicationFactory factory, string ipPrefix) : base(factory)
    {
        _ipPrefix = ipPrefix;
    }

    [Fact]
    public async Task Anonymous_IsRedirectedToLogin()
    {
        var response = await AnonymousClient().GetAsync("/after-hours");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().Contain("/login");
        location.Should().Contain("ReturnUrl");
    }

    [Theory]
    [InlineData("Member", 1)]
    [InlineData("Admin", 2)]
    [InlineData("Owner", 3)]
    public async Task SignedInUser_IsRefused_WhateverTheRole(string role, int ip)
    {
        var (client, _) = await CookieTestSession.SignInAsync(
            Factory, $"ah-off-{role.ToLowerInvariant()}-{_ipPrefix.Replace('.', '-')}", $"{_ipPrefix}.{ip}", role);

        await ShouldBeRefusedAsync(await client.GetAsync("/after-hours"));
    }

    [Theory]
    [MemberData(nameof(RouteVariants))]
    public async Task RouteVariants_AreRefused(string path, int ip)
    {
        var (client, _) = await CookieTestSession.SignInAsync(
            Factory, $"ah-off-variant-{Guid.NewGuid():N}", $"{_ipPrefix}.{ip}", "Member");

        await ShouldBeRefusedAsync(await client.GetAsync(path));
    }

    [Theory]
    [InlineData("crimes", 5, "Lift a phone outside the bar")]
    [InlineData("cargo", 6, "Fence pays")]
    public async Task ChildRoutes_AreRefused(string route, int ip, string pageText)
    {
        var (client, _) = await CookieTestSession.SignInAsync(
            Factory, $"ah-off-{route}-{_ipPrefix.Replace('.', '-')}", $"{_ipPrefix}.{ip}", "Member");

        var response = await client.GetAsync($"/after-hours/{route}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Forbidden);
        (await ReadBodyAsync(response)).Should().NotContain(pageText);
    }

    [Fact]
    public async Task Navigation_HasNoAfterHoursEntry_ForASignedInMember()
    {
        var (client, _) = await CookieTestSession.SignInAsync(
            Factory, $"ah-off-nav-{_ipPrefix.Replace('.', '-')}", $"{_ipPrefix}.4", "Member");

        var response = await client.GetAsync("/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await ReadBodyAsync(response);
        html.Should().Contain("href=\"/my-tuno\"", "the Jogos menu itself is rendered for a member");
        html.Should().NotContain(NavLink);
    }
}

public class AfterHoursMissingConfigTests : AfterHoursDisabledTestsBase, IClassFixture<AfterHoursMissingConfigFactory>
{
    public AfterHoursMissingConfigTests(AfterHoursMissingConfigFactory factory) : base(factory, "10.50.1")
    {
    }

    /// <summary>Proves this host really runs with the key unset, not with appsettings' false.</summary>
    [Fact]
    public void Host_HasNoAfterHoursEnabledValue()
    {
        Factory.Services.GetRequiredService<IConfiguration>()["AfterHours:Enabled"].Should().BeNull();
    }
}

public class AfterHoursExplicitlyDisabledTests : AfterHoursDisabledTestsBase, IClassFixture<AfterHoursDisabledFactory>
{
    public AfterHoursExplicitlyDisabledTests(AfterHoursDisabledFactory factory) : base(factory, "10.50.2")
    {
    }
}

public class AfterHoursEnabledTests : AfterHoursGateTestsBase, IClassFixture<AfterHoursEnabledFactory>
{
    public AfterHoursEnabledTests(AfterHoursEnabledFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Anonymous_StillRequiresAuthentication()
    {
        var response = await AnonymousClient().GetAsync("/after-hours");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().Contain("/login");
        location.Should().Contain("ReturnUrl");
    }

    [Fact]
    public async Task Member_GetsTheLandingPage()
    {
        var (client, _) = await CookieTestSession.SignInAsync(Factory, "ah-on-member", "10.50.3.1", "Member");

        var response = await client.GetAsync("/after-hours");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadBodyAsync(response)).Should().Contain(LandingMarker);
    }

    /// <summary>
    /// The control for the disabled route-variant tests: every spelling really does reach the
    /// After Hours page, so their refusal comes from the gate and not from a missed route.
    /// </summary>
    [Theory]
    [MemberData(nameof(RouteVariants))]
    public async Task RouteVariants_ReachTheSamePage(string path, int ip)
    {
        var (client, _) = await CookieTestSession.SignInAsync(
            Factory, $"ah-on-variant-{Guid.NewGuid():N}", $"10.50.3.{ip}", "Member");

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadBodyAsync(response)).Should().Contain(LandingMarker);
    }

    [Fact]
    public async Task Navigation_HasTheAfterHoursEntry_ForASignedInMember()
    {
        var (client, _) = await CookieTestSession.SignInAsync(Factory, "ah-on-nav", "10.50.3.2", "Member");

        var response = await client.GetAsync("/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadBodyAsync(response)).Should().Contain(NavLink);
    }

    [Fact]
    public async Task Navigation_HasNoAfterHoursEntry_ForAnonymous()
    {
        var response = await AnonymousClient().GetAsync("/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadBodyAsync(response)).Should().NotContain(NavLink);
    }

    /// <summary>
    /// The only test in this class that creates a cycle, so "no active cycle" is observable first.
    /// </summary>
    [Fact]
    public async Task LandingPage_ShowsNoActiveCycle_ThenTheStartingState()
    {
        var (client, _) = await CookieTestSession.SignInAsync(Factory, "ah-on-state", "10.50.3.3", "Member");

        var before = await ReadBodyAsync(await client.GetAsync("/after-hours"));
        before.Should().Contain("No active cycle");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RTUB.Application.Data.ApplicationDbContext>();
            var fiscalYear = RTUB.Core.Entities.FiscalYear.Create(2900, 2901);
            db.FiscalYears.Add(fiscalYear);
            await db.SaveChangesAsync();

            var cycles = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.AfterHours.IGameCycleService>();
            var cycle = await cycles.CreateCycleAsync(fiscalYear.Id, RTUB.Core.Enums.AfterHours.GameCycleKind.Pilot,
                DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30));
            await cycles.ActivateAsync(cycle.Id);
        }

        var after = await ReadBodyAsync(await client.GetAsync("/after-hours"));
        after.Should().Contain("Pilot cycle · 2900-2901");
        after.Should().MatchRegex(@"<dt[^>]*>Level</dt>\s*<dd[^>]*>1</dd>");
        after.Should().MatchRegex(@"<dt[^>]*>Wallet</dt>\s*<dd[^>]*>\$400</dd>");
        after.Should().MatchRegex(@"<dt[^>]*>Energy</dt>\s*<dd[^>]*>\s*240 / 240");
        after.Should().Contain("href=\"/after-hours/crimes\"");

        var crimes = await client.GetAsync("/after-hours/crimes");
        crimes.StatusCode.Should().Be(HttpStatusCode.OK);
        var crimesHtml = await ReadBodyAsync(crimes);
        crimesHtml.Should().Contain("Lift a phone outside the bar");
        crimesHtml.Should().Contain("82%", "C01 at Standard with starting skills and no heat");
        crimesHtml.Should().MatchRegex(@"Collect a debt after rehearsal</h2>\s*<span[^>]*><i[^>]*></i> Level 2", "C02 is shown locked");
        crimesHtml.Should().Contain("+ 1 phone", "C01 shows its cargo reward");
        after.Should().Contain("href=\"/after-hours/cargo\"");

        var cargo = await client.GetAsync("/after-hours/cargo");
        cargo.StatusCode.Should().Be(HttpStatusCode.OK);
        var cargoHtml = await ReadBodyAsync(cargo);
        cargoHtml.Should().Contain("Fence pays $80 each");
        Regex.Matches(cargoHtml, "Pays <strong[^>]*>").Count.Should().Be(3, "three buyer contracts per rotation");
    }
}

/// <summary>
/// The shared test host with <c>AfterHours:Enabled</c> forced to a value. Added after the base
/// configuration, so it wins over appsettings.json and any environment variable.
/// </summary>
public abstract class AfterHoursFactory : TestWebApplicationFactory
{
    private readonly string? _enabled;

    protected AfterHoursFactory(string? enabled)
    {
        _enabled = enabled;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AfterHours:Enabled"] = _enabled
            }));
    }
}

/// <summary>A null value hides the appsettings.json default: the key is effectively unset.</summary>
public class AfterHoursMissingConfigFactory() : AfterHoursFactory(null);

public class AfterHoursDisabledFactory() : AfterHoursFactory("false");

public class AfterHoursEnabledFactory() : AfterHoursFactory("true");

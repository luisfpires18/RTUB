using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
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
    [InlineData("training", 7, "training point a day")]
    [InlineData("equipment", 8, "Brass Knuckles")]
    [InlineData("pvp", 9, "Attack setup")]
    [InlineData("pvp/report/1", 10, "Battle report")]
    [InlineData("family", 11, "Create a family")]
    [InlineData("objectives", 12, "Daily objectives pay XP")]
    [InlineData("leaderboards", 13, "Annual score")]
    [InlineData("yearbook", 16, "archived")]
    [InlineData("admin", 17, "Pilot readiness")]
    public async Task ChildRoutes_AreRefused(string route, int ip, string pageText)
    {
        var (client, _) = await CookieTestSession.SignInAsync(
            Factory, $"ah-off-route-{route.Replace('/', '-')}-{_ipPrefix.Replace('.', '-')}", $"{_ipPrefix}.{ip}", "Member");

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
        after.Should().Contain("href=\"/after-hours/training\"").And.Contain("href=\"/after-hours/equipment\"");

        var training = await ReadBodyAsync(await client.GetAsync("/after-hours/training"));
        training.Should().Contain("1 / 3 points");
        training.Should().Contain("At your current cap", "level 1 caps skills at their starting rank");

        var equipment = await ReadBodyAsync(await client.GetAsync("/after-hours/equipment"));
        equipment.Should().Contain("Brass Knuckles").And.Contain("Specialist Rig");
        equipment.Should().Contain("Level 3 · $400");
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

/// <summary>
/// The PvP pages through the real host, on their own database: the report is readable by the
/// attacker and the defender only, and the PvP page renders for a member.
/// </summary>
public class AfterHoursPvpPagesTests : AfterHoursGateTestsBase, IClassFixture<AfterHoursEnabledFactory>
{
    public AfterHoursPvpPagesTests(AfterHoursEnabledFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Report_OnlyForItsTwoPlayers_AndPvpPageRenders()
    {
        var (attackerClient, attacker) = await CookieTestSession.SignInAsync(Factory, "ah-pvp-a", "10.50.4.1", "Member");
        var (defenderClient, defender) = await CookieTestSession.SignInAsync(Factory, "ah-pvp-d", "10.50.4.2", "Member");
        var (strangerClient, _) = await CookieTestSession.SignInAsync(Factory, "ah-pvp-s", "10.50.4.3", "Member");

        int battleId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RTUB.Application.Data.ApplicationDbContext>();
            var fiscalYear = RTUB.Core.Entities.FiscalYear.Create(2700, 2701);
            db.FiscalYears.Add(fiscalYear);
            await db.SaveChangesAsync();
            var cycles = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.AfterHours.IGameCycleService>();
            var cycle = await cycles.CreateCycleAsync(fiscalYear.Id, RTUB.Core.Enums.AfterHours.GameCycleKind.Pilot,
                DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddDays(30));
            await cycles.ActivateAsync(cycle.Id);

            var states = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.AfterHours.IPlayerCycleStateService>();
            await states.GetOrCreateForActiveCycleAsync(attacker.Id);
            var defenderState = await states.GetOrCreateForActiveCycleAsync(defender.Id);
            foreach (var row in db.AfterHoursPlayerCycleStates)
                row.CreatedAt = DateTime.UtcNow.AddDays(-4); // past new-player protection
            await db.SaveChangesAsync();

            var result = await scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.AfterHours.IAfterHoursActionService>()
                .AttackAsync(attacker.Id, defenderState!.Id, RTUB.Core.Enums.AfterHours.PvpTactic.Ambush,
                    RTUB.Core.Enums.AfterHours.RiskStance.Standard, null, null, null, "page-attack");
            result.Accepted.Should().BeTrue();
            battleId = result.Receipt!.PvpBattle!.Id;
        }

        var path = $"/after-hours/pvp/report/{battleId}";
        foreach (var client in new[] { attackerClient, defenderClient })
        {
            var html = await ReadBodyAsync(await client.GetAsync(path));
            html.Should().Contain("ah-pvp-a attacked ah-pvp-d");
            html.Should().Contain("Rounds").And.Contain("Total damage:");
        }

        var strangerHtml = await ReadBodyAsync(await strangerClient.GetAsync(path));
        strangerHtml.Should().Contain("Report not found.");
        strangerHtml.Should().NotContain("ah-pvp-a attacked");

        var pvp = await ReadBodyAsync(await attackerClient.GetAsync("/after-hours/pvp"));
        pvp.Should().Contain("Effective power").And.Contain("Attack setup").And.Contain("You attacked ah-pvp-d");
        pvp.Should().NotContain("$400", "targets never show another player's cash");
    }
}

/// <summary>The family page through the real host, on its own database.</summary>
public class AfterHoursFamilyPagesTests : AfterHoursGateTestsBase, IClassFixture<AfterHoursEnabledFactory>
{
    public AfterHoursFamilyPagesTests(AfterHoursEnabledFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task FamilyPage_ShowsCreateOrLock_ThenTheFamily_WithoutMoneyOfOthers()
    {
        var (bossClient, boss) = await CookieTestSession.SignInAsync(Factory, "ah-fam-boss", "10.50.5.1", "Member");
        var (lowClient, low) = await CookieTestSession.SignInAsync(Factory, "ah-fam-low", "10.50.5.2", "Member");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RTUB.Application.Data.ApplicationDbContext>();
            var fiscalYear = RTUB.Core.Entities.FiscalYear.Create(2600, 2601);
            db.FiscalYears.Add(fiscalYear);
            await db.SaveChangesAsync();
            var cycles = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.AfterHours.IGameCycleService>();
            var cycle = await cycles.CreateCycleAsync(fiscalYear.Id, RTUB.Core.Enums.AfterHours.GameCycleKind.Pilot,
                DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30));
            await cycles.ActivateAsync(cycle.Id);

            var states = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.AfterHours.IPlayerCycleStateService>();
            await states.GetOrCreateForActiveCycleAsync(low.Id);
            var bossState = await states.GetOrCreateForActiveCycleAsync(boss.Id);
            var row = await db.AfterHoursPlayerCycleStates.SingleAsync(s => s.Id == bossState!.Id);
            (row.Level, row.WalletCash) = (5, 2_000);
            await db.SaveChangesAsync();
        }

        (await ReadBodyAsync(await bossClient.GetAsync("/after-hours/family"))).Should().Contain("Create a family").And.Contain("$1,500");
        (await ReadBodyAsync(await lowClient.GetAsync("/after-hours/family"))).Should().Contain("Unlocks at level 5");

        using (var scope = Factory.Services.CreateScope())
        {
            var created = await scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.AfterHours.IAfterHoursActionService>()
                .CreateFamilyAsync(boss.Id, "Night Shift", "Loyal after dark", "page-family");
            created.Accepted.Should().BeTrue(created.Error);
        }

        var page = await ReadBodyAsync(await bossClient.GetAsync("/after-hours/family"));
        page.Should().Contain("Night Shift").And.Contain("Loyal after dark").And.Contain("1 / 4").And.Contain("$0 this cycle");
        page.Should().Contain("ah-fam-low", "the Boss can invite current-cycle players");
        page.Should().Contain("Leave and disband");
        page.Should().NotContain("$400", "another player's wallet is never shown");
    }
}

/// <summary>Objectives and leaderboards through the real host, on their own database.</summary>
public class AfterHoursObjectivePagesTests : AfterHoursGateTestsBase, IClassFixture<AfterHoursEnabledFactory>
{
    public AfterHoursObjectivePagesTests(AfterHoursEnabledFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ObjectivesAndLeaderboards_RenderFromStoredProgress()
    {
        var (client, user) = await CookieTestSession.SignInAsync(Factory, "ah-obj-player", "10.50.6.1", "Member");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RTUB.Application.Data.ApplicationDbContext>();
            var fiscalYear = RTUB.Core.Entities.FiscalYear.Create(2500, 2501);
            db.FiscalYears.Add(fiscalYear);
            await db.SaveChangesAsync();
            var cycles = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.AfterHours.IGameCycleService>();
            var cycle = await cycles.CreateCycleAsync(fiscalYear.Id, RTUB.Core.Enums.AfterHours.GameCycleKind.Pilot,
                DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(30));
            await cycles.ActivateAsync(cycle.Id);
            var result = await scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.AfterHours.IAfterHoursActionService>()
                .CommitCrimeAsync(user.Id, "C01", RTUB.Core.Enums.AfterHours.CrimeApproach.Careful, "page-crime");
            result.Accepted.Should().BeTrue(result.Error);
        }

        var objectives = await ReadBodyAsync(await client.GetAsync("/after-hours/objectives"));
        objectives.Should().Contain("Today").And.Contain("Week 1").And.Contain("Solo crimes");
        var today = RTUB.Core.Helpers.AfterHours.ObjectiveCatalogue.DailyFor(RTUB.Core.Helpers.AfterHours.LisbonCalendar.DateOf(DateTime.UtcNow));
        foreach (var daily in today) objectives.Should().Contain(daily.Title);
        objectives.Should().NotContain("Crew Hustle", "no family section without a family");

        var boards = await ReadBodyAsync(await client.GetAsync("/after-hours/leaderboards"));
        boards.Should().Contain("ah-obj-player").And.Contain("#1").And.Contain("Individual").And.Contain("Family");
        boards.Should().NotContain("Champion");
    }
}

/// <summary>
/// AH-009: the yearbook renders stored archives only. Archives are inserted directly here (the rollover itself
/// is proven in AfterHoursRolloverTests); no cycle is active, which the yearbook does not need.
/// </summary>
public class AfterHoursYearbookPagesTests : AfterHoursGateTestsBase, IClassFixture<AfterHoursEnabledFactory>
{
    public AfterHoursYearbookPagesTests(AfterHoursEnabledFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Yearbook_ShowsOfficialCoChampions_AndPilotAsNonOfficial_NewestFirst()
    {
        var (client, user) = await CookieTestSession.SignInAsync(Factory, "ah-yearbook-player", "10.50.7.1", "Member");
        (await ReadBodyAsync(await client.GetAsync("/after-hours/yearbook"))).Should().Contain("No cycle has been archived yet");

        using (var scope = Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<RTUB.Core.Entities.ApplicationUser>>();
            var others = new List<string>();
            foreach (var name in new[] { "ah-yearbook-co", "ah-yearbook-runner" })
            {
                var other = new RTUB.Core.Entities.ApplicationUser { UserName = name, Email = $"{name}@test.com", FirstName = "A", LastName = "H", Nickname = name };
                (await users.CreateAsync(other)).Succeeded.Should().BeTrue();
                others.Add(other.Id);
            }

            var db = scope.ServiceProvider.GetRequiredService<RTUB.Application.Data.ApplicationDbContext>();
            var fiscalYear = RTUB.Core.Entities.FiscalYear.Create(2600, 2601);
            db.FiscalYears.Add(fiscalYear);
            var family = new RTUB.Core.Entities.AfterHours.Family { Name = "Night Owls", NormalizedName = "NIGHT OWLS", CreatedByUserId = user.Id };
            db.AfterHoursFamilies.Add(family);
            await db.SaveChangesAsync();
            var cycles = scope.ServiceProvider.GetRequiredService<RTUB.Application.Interfaces.AfterHours.IGameCycleService>();
            var start = RTUB.Core.Helpers.AfterHours.RolloverRules.SeptemberStartUtc(2600);
            var pilot = await cycles.CreateCycleAsync(fiscalYear.Id, RTUB.Core.Enums.AfterHours.GameCycleKind.Pilot, start, start.AddDays(30));
            var live = await cycles.CreateCycleAsync(fiscalYear.Id, RTUB.Core.Enums.AfterHours.GameCycleKind.Live, start.AddDays(30), RTUB.Core.Helpers.AfterHours.RolloverRules.SeptemberStartUtc(2601));

            var pilotArchive = Archive(pilot, official: false);
            pilotArchive.Players.Add(Entry(user.Id, "Pilot Ace", 1, 90, champion: false));
            var liveArchive = Archive(live, official: true);
            liveArchive.Players.AddRange([Entry(user.Id, "Top Dog", 1, 300, true), Entry(others[0], "Co Top", 1, 300, true), Entry(others[1], "Runner Up", 3, 120, false)]);
            liveArchive.Families.Add(new RTUB.Core.Entities.AfterHours.YearbookFamilyEntry
            {
                FamilyId = family.Id, FamilyName = "Night Owls", AnnualScore = 250, Rank = 1, ScoringWeeks = 5, IsChampion = true,
                Members = [new() { UserId = user.Id, DisplayName = "Top Dog", Role = RTUB.Core.Enums.AfterHours.FamilyRole.Boss }]
            });
            db.AfterHoursCycleArchives.AddRange(pilotArchive, liveArchive);
            await db.SaveChangesAsync();

            RTUB.Core.Entities.AfterHours.CycleArchive Archive(RTUB.Core.Entities.AfterHours.GameCycle cycle, bool official) => new()
            {
                GameCycleId = cycle.Id, Kind = cycle.Kind, FiscalYearId = fiscalYear.Id, FiscalYearLabel = "2600-2601",
                StartUtc = cycle.StartUtc, EndUtc = cycle.EndUtc, ArchivedAtUtc = cycle.EndUtc, Official = official
            };
        }

        // A deleted account does not take its history with it: the page renders from the snapshot alone.
        using (var scope = Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<RTUB.Core.Entities.ApplicationUser>>();
            (await users.DeleteAsync((await users.FindByNameAsync("ah-yearbook-co"))!)).Succeeded.Should().BeTrue();
        }

        var html = await ReadBodyAsync(await client.GetAsync("/after-hours/yearbook"));

        html.Should().Contain("2600-2601 · Live").And.Contain("2600-2601 · Pilot").And.Contain("Official");
        html.Should().Contain("Individual Co-Champions: Co Top, Top Dog").And.Contain("Family Champion: Night Owls");
        html.Should().Contain("#3</span> Runner Up").And.Contain("Pilot Ace").And.Contain("Pilot results are non-official");
        html.Should().Contain("1 Sep 2600", "dates are shown as Lisbon days");
        html.IndexOf("2600-2601 · Live", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("2600-2601 · Pilot", StringComparison.Ordinal), "newest archive first");
        var pilotSection = html[html.IndexOf("2600-2601 · Pilot", StringComparison.Ordinal)..];
        pilotSection.Should().NotContain("Champion").And.NotContain("🏆");
    }

    private static RTUB.Core.Entities.AfterHours.YearbookPlayerEntry Entry(string userId, string name, int rank, int score, bool champion) => new()
    {
        UserId = userId, DisplayName = name, Level = 7, XP = 900, Toughness = 4, Stealth = 4, Smarts = 4, Charisma = 4,
        AnnualScore = score, Rank = rank, ScoringWeeks = 4, IsChampion = champion
    };
}

/// <summary>
/// AH-010: the admin page and its navigation entry through the real host. Owner only, on top of the After Hours
/// gate; the Admin role is not enough (the narrowest role already used for dangerous operations).
/// </summary>
public class AfterHoursAdminPagesTests : AfterHoursGateTestsBase, IClassFixture<AfterHoursEnabledFactory>
{
    public AfterHoursAdminPagesTests(AfterHoursEnabledFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("Member", 1)]
    [InlineData("Admin", 2)]
    public async Task AdminPage_IsRefused_AndHidden_ForNonOwners(string role, int ip)
    {
        var (client, _) = await CookieTestSession.SignInAsync(Factory, $"ah-admin-{role.ToLowerInvariant()}", $"10.50.8.{ip}", role);

        var response = await client.GetAsync("/after-hours/admin");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Forbidden);
        (await ReadBodyAsync(response)).Should().NotContain("Pilot readiness");

        var home = await ReadBodyAsync(await client.GetAsync("/after-hours"));
        home.Should().Contain(LandingMarker).And.NotContain("href=\"/after-hours/admin\"");
    }

    [Fact]
    public async Task AdminPage_RendersForTheOwner_WithTheNavigationEntry()
    {
        var (client, _) = await CookieTestSession.SignInAsync(Factory, "ah-admin-owner", "10.50.8.3", "Owner");

        var response = await client.GetAsync("/after-hours/admin");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await ReadBodyAsync(response);
        html.Should().Contain("After Hours admin").And.Contain("Pilot readiness").And.Contain("Economy and gameplay tuning")
            .And.Contain("Suspicious PvP").And.Contain("Cosmetic awards").And.Contain("BankDepositFeePercent").And.Contain("Start a Pilot");

        (await ReadBodyAsync(await client.GetAsync("/after-hours"))).Should().Contain("href=\"/after-hours/admin\"");
    }
}

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/trintuna (docs/react-trintuna.md) through the real host: real login, antiforgery and SQLite. The access list
/// is one list for the whole page, so every test sets the list it needs (the class shares one database and its tests
/// run one after the other). This file: who gets in, the menu flag, the route and the access list.
/// </summary>
public partial class TrintunaApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public TrintunaApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] Reads = { "/api/trintuna", "/api/trintuna/access" };

    [Fact]
    public async Task Visitors_GetNothing_AndThePageAsksToSignIn()
    {
        var anonymous = Anonymous();
        await AreaHttp.WithTokenAsync(anonymous);
        foreach (var path in Reads)
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }

        foreach (var response in await WritesAsync(anonymous))
        {
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, response.RequestMessage!.RequestUri!.ToString());
        }

        var page = await anonymous.GetAsync("/trintuna");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=%2Ftrintuna");
    }

    [Fact]
    public async Task AMemberNotOnTheList_GetsNotFoundEverywhere_AndNoMenuLink()
    {
        var (owner, _) = await SignInAsync("Owner");
        await SetListAsync(owner);
        var (member, _) = await SignInAsync("Admin");
        await AreaHttp.WithTokenAsync(member);

        foreach (var path in Reads)
        {
            (await member.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound, path);
        }

        foreach (var response in await WritesAsync(member))
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, response.RequestMessage!.RequestUri!.ToString());
        }

        (await AreaHttp.Json(member, "/api/account/me")).GetProperty("menu").GetProperty("trintuna").GetBoolean().Should().BeFalse();
        (await member.GetStringAsync("/trintuna")).Should().Contain("id=\"root\"");
    }

    [Fact]
    public async Task TheOwnerListsMembers_WhoThenSeeThePage_ButNotTheOwnerTools()
    {
        var (owner, _) = await SignInAsync("Owner");
        (await AreaHttp.Json(owner, "/api/account/me")).GetProperty("menu").GetProperty("trintuna").GetBoolean().Should().BeTrue();
        var (member, me) = await SignInAsync();
        var gathering = await CreateAsync(owner);

        var list = await SetListAsync(owner, me.Id);
        list.EnumerateArray().Select(m => m.GetProperty("id").GetString()).Should().Equal(me.Id);
        list[0].GetProperty("displayName").GetString().Should().Be(me.Nickname);

        (await AreaHttp.Json(member, "/api/account/me")).GetProperty("menu").GetProperty("trintuna").GetBoolean().Should().BeTrue();
        var board = await AreaHttp.Json(member, "/api/trintuna");
        board.GetProperty("canManage").GetBoolean().Should().BeFalse();
        board.GetProperty("upcoming").EnumerateArray().Should().Contain(g => g.GetProperty("id").GetInt32() == gathering.GetProperty("id").GetInt32());

        await AreaHttp.WithTokenAsync(member);
        (await member.GetAsync("/api/trintuna/access")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PutAsJsonAsync("/api/trintuna/access", new { userIds = new[] { me.Id } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PostAsJsonAsync("/api/trintuna/gatherings", Input(NextNumber()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.DeleteAsync($"/api/trintuna/gatherings/{gathering.GetProperty("id").GetInt32()}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Taken off the list: the page is gone again.
        await SetListAsync(owner);
        (await member.GetAsync("/api/trintuna")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AreaHttp.Json(member, "/api/account/me")).GetProperty("menu").GetProperty("trintuna").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task TheAccessList_RefusesUnknownAndExpelledMembers_AndWritesNeedTheToken()
    {
        var (owner, _) = await SignInAsync("Owner");
        var expelled = await AddUserAsync(expelled: true);

        (await AreaHttp.Errors(await owner.PutAsJsonAsync("/api/trintuna/access", new { userIds = new[] { "no-such-user" } }))).Should().ContainKey("userIds");
        (await AreaHttp.Errors(await owner.PutAsJsonAsync("/api/trintuna/access", new { userIds = new[] { expelled.Id } }))).Should().ContainKey("userIds");

        var noToken = (await SignInAsync("Owner", token: false)).Client;
        (await noToken.PutAsJsonAsync("/api/trintuna/access", new { userIds = Array.Empty<string>() })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await noToken.PostAsJsonAsync("/api/trintuna/gatherings", Input(NextNumber()))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HistoricalGatherings_AreUnderPassados_AndNoUserIdLeaksToMembers()
    {
        var (owner, _) = await SignInAsync("Owner");
        var (member, me) = await SignInAsync();
        var (other, who) = await SignInAsync();
        await SetListAsync(owner, me.Id, who.Id);
        await CreateAsync(owner, organizerUserId: who.Id);
        var first = await SeedGatheringAsync(historical: true);
        var second = await SeedGatheringAsync(historical: true);
        (await member.PutAsJsonAsync($"/api/trintuna/gatherings/{second}/presence", new { willAttend = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await other.PutAsJsonAsync($"/api/trintuna/gatherings/{second}/presence", new { willAttend = false })).StatusCode.Should().Be(HttpStatusCode.OK);

        var board = await AreaHttp.Json(member, "/api/trintuna");
        var historical = board.GetProperty("past").EnumerateArray().Where(g => g.GetProperty("id").GetInt32() is var id && (id == first || id == second)).ToList();
        historical.Select(g => g.GetProperty("id").GetInt32()).Should().Equal(second, first);
        historical.Should().OnlyContain(g => g.GetProperty("date").ValueKind == JsonValueKind.Null
            && g.GetProperty("organizer").ValueKind == JsonValueKind.Null
            && g.GetProperty("poll").GetProperty("closed").GetBoolean());
        historical[0].GetProperty("going").EnumerateArray().Select(p => p.GetProperty("displayName").GetString()).Should().Equal(me.Nickname);
        historical[0].GetProperty("notGoing").EnumerateArray().Select(p => p.GetProperty("displayName").GetString()).Should().Equal(who.Nickname);

        var text = board.GetRawText();
        text.Should().NotContain(me.Id).And.NotContain(who.Id);
        (await AreaHttp.Json(owner, "/api/trintuna")).GetRawText().Should().Contain(who.Id);
        await ClearAnswersAsync();
    }

    // ---------------------------------------------------------------- helpers

    private static int _number = 1000;

    private static int NextNumber() => Interlocked.Increment(ref _number);

    private static object Input(int number, string? organizerUserId = null, params string[] pollDates) =>
        new { number, place = "Vila Real", description = "Tripas", organizerUserId, pollDates };

    private async Task<JsonElement> CreateAsync(HttpClient owner, string? organizerUserId = null, params string[] pollDates) =>
        await AreaHttp.Json(await owner.PostAsJsonAsync("/api/trintuna/gatherings", Input(NextNumber(), organizerUserId, pollDates)));

    private async Task<JsonElement> SetListAsync(HttpClient owner, params string[] userIds) =>
        await AreaHttp.Json(await owner.PutAsJsonAsync("/api/trintuna/access", new { userIds }));

    /// <summary>Every write once, on gathering 1 (whether it exists or not: these callers are refused first).</summary>
    private static async Task<HttpResponseMessage[]> WritesAsync(HttpClient client) => new[]
    {
        await client.PostAsJsonAsync("/api/trintuna/gatherings", Input(5000)),
        await client.PutAsJsonAsync("/api/trintuna/gatherings/1", Input(1)),
        await client.DeleteAsync("/api/trintuna/gatherings/1"),
        await client.PostAsJsonAsync("/api/trintuna/gatherings/1/settle", new { optionId = 1 }),
        await client.PutAsJsonAsync("/api/trintuna/gatherings/1/vote", new { optionId = 1 }),
        await client.PutAsJsonAsync("/api/trintuna/gatherings/1/presence", new { willAttend = true }),
        await client.DeleteAsync("/api/trintuna/gatherings/1/presence"),
        await client.PutAsJsonAsync("/api/trintuna/access", new { userIds = Array.Empty<string>() }),
    };

    /// <summary>A gathering written straight to the database (the API cannot create a historical one); its id.</summary>
    private async Task<int> SeedGatheringAsync(bool historical)
    {
        using var scope = _factory.Services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        var gathering = new Gathering { Number = NextNumber(), Place = "Lamego", Description = "Bola", IsHistorical = historical };
        db.Gatherings.Add(gathering);
        await db.SaveChangesAsync();
        return gathering.Id;
    }

    /// <summary>Removes every Vou / Não vou, so the Top 3 of the shared database starts empty.</summary>
    private async Task ClearAnswersAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        await db.GatheringParticipants.ExecuteDeleteAsync();
    }

    private async Task<ApplicationUser> AddUserAsync(bool expelled = false)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userName = "tt" + Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName + "@test.com",
            PhoneNumber = "919191919",
            Nickname = userName,
            FirstName = "Tri",
            LastName = "Tuna",
            IsExpelled = expelled,
        };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        return user;
    }

    private static int _ip;

    /// <summary>Signed in, with the antiforgery header set unless <paramref name="token"/> is false.</summary>
    private async Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null, bool token = true)
    {
        var n = Interlocked.Increment(ref _ip);
        var session = await CookieTestSession.SignInAsync(_factory, $"tt{Guid.NewGuid():N}"[..20], $"10.87.{n / 250}.{n % 250 + 1}", role);
        if (token)
        {
            await AreaHttp.WithTokenAsync(session.Client);
        }

        return session;
    }

    private HttpClient Anonymous() => AreaHttp.Anonymous(_factory, "10.88.0.1");
}

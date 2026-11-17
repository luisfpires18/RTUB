// OwnerOperationsApiTests: DashboardAndDatabase tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Integration.Tests.Api;

public partial class OwnerOperationsApiTests
{
    // ---------- /owner/dashboard (task 050) ----------

    [Fact]
    public async Task Dashboard_RecordsSignInsAndSeenHours_AndCountsThemPerPortugalDay()
    {
        var n = Interlocked.Increment(ref _ip);
        var (owner, user) = await CookieTestSession.SignInAsync(_factory, $"own{Guid.NewGuid():N}"[..20], $"10.72.{n / 250}.{n % 250 + 1}", "Owner");
        var signedIn = await WithDbAsync(db => db.LoginEvents.Where(e => e.UserId == user.Id).ToListAsync());
        signedIn.Should().ContainSingle("the real sign-in wrote one row").Which.IsSignIn.Should().BeTrue();

        // That row already counts as "seen this hour"; drop it and the activity throttle so the next request is seen.
        // The one 40 days ago is outside the default 30 days, inside the 30 before them.
        await WithDbAsync(db =>
        {
            db.LoginEvents.RemoveRange(signedIn);
            db.LoginEvents.Add(new LoginEvent { UserId = user.Id, At = DateTime.UtcNow.AddDays(-40), IsSignIn = true });
            return db.SaveChangesAsync();
        });
        _factory.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()
            .Remove(RTUB.Web.Extensions.ServiceCollectionExtensions.ActivityWriteCachePrefix + user.Id);
        var member = $"dash{Tag()}";
        await SeedLogAsync("Dashboard", user: member);
        await SeedLogAsync("Dashboard", user: member, setup: l => l.Action = "Played");
        await SeedLogAsync("Dashboard", user: member, setup: l => l.EntityType = "MemberStatus");
        await SeedLogAsync("Dashboard", user: member, setup: l => l.EntityType = "MemberActivityStatus");

        var body = await AreaHttp.Json(owner, "/api/owner/dashboard");
        await AreaHttp.Json(owner, "/api/owner/dashboard");

        var rows = await WithDbAsync(db => db.LoginEvents.AsNoTracking().Where(e => e.UserId == user.Id && !e.IsSignIn).ToListAsync());
        rows.Should().ContainSingle("an authenticated request marks the member seen, once per hour");

        var today = PortugalTime.Today(TimeProvider.System);
        body.GetProperty("to").GetString().Should().Be(today.ToString("yyyy-MM-dd"));
        body.GetProperty("from").GetString().Should().Be(today.AddDays(-29).ToString("yyyy-MM-dd"));
        body.GetProperty("days").GetArrayLength().Should().Be(30);

        var series = body.GetProperty("series").EnumerateArray().ToDictionary(s => s.GetProperty("key").GetString()!);
        series.Keys.Should().Equal("signIns", "activeMembers", "movements", "uploads", "songPlays", "naipePlays", "enrollments", "pageViews");
        series.Values.Should().OnlyContain(s => s.GetProperty("values").GetArrayLength() == 30);
        series["signIns"].GetProperty("previousTotal").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        series["activeMembers"].GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var top = body.GetProperty("topMembers").EnumerateArray()
            .ToDictionary(r => r.GetProperty("label").GetString()!, r => r.GetProperty("count").GetInt32());
        top.Should().Contain(member, 1, "a play is a song play and a status update is a system action, not a movement");
        var areas = body.GetProperty("areas").EnumerateArray().Select(r => r.GetProperty("label").GetString()!).ToList();
        areas.Should().NotContain(["MemberStatus", "MemberActivityStatus"]);
        areas.Should().OnlyContain(a => !a.Contains("Enrollment") && !a.Contains("LogisticsCard"), "088: areas of the app, not entity types");
        body.TryGetProperty("topSongs", out _).Should().BeFalse("088 dropped the song, naipe and action-type rankings");
        body.TryGetProperty("actions", out _).Should().BeFalse();

        var hours = body.GetProperty("hours");
        hours.GetArrayLength().Should().Be(7);
        hours.EnumerateArray().Should().OnlyContain(h => h.GetArrayLength() == 24);
        hours.EnumerateArray().SelectMany(h => h.EnumerateArray()).Sum(c => c.GetInt32()).Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("now").GetProperty("online").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Task 089: /api/account/me counts the page that asked (its Referer, same origin only, ids normalised), the flush
    /// stores it per Portugal day, and the dashboard shows the tile and "Páginas mais vistas".
    /// </summary>
    [Fact]
    public async Task Dashboard_CountsPageViews_FromTheReferer_PerRoute()
    {
        var n = Interlocked.Increment(ref _ip);
        var (owner, _) = await CookieTestSession.SignInAsync(_factory, $"pvw{Guid.NewGuid():N}"[..20], $"10.72.{n / 250}.{n % 250 + 1}", "Owner");
        var counter = _factory.Services.GetRequiredService<PageViewCounter>();
        await counter.FlushAsync(_factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()); // whatever earlier tests left

        async Task Open(string referer)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/account/me");
            request.Headers.Referrer = new Uri(referer);
            (await owner.SendAsync(request)).EnsureSuccessStatusCode();
        }

        await Open("http://localhost/events/11?x=1");
        await Open("http://localhost/events/12");
        await Open("http://localhost/music");
        await Open("https://evil.example/music");
        (await counter.FlushAsync(_factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>())).Should().Be(2);

        var body = await owner.GetFromJsonAsync<JsonElement>("/api/owner/dashboard");
        var pages = body.GetProperty("series").EnumerateArray().Single(x => x.GetProperty("key").GetString() == "pageViews");
        pages.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        var top = body.GetProperty("topPages").EnumerateArray()
            .ToDictionary(r => r.GetProperty("label").GetString()!, r => r.GetProperty("count").GetInt32());
        top["/events/{id}"].Should().BeGreaterThanOrEqualTo(2, "ids are one route");
        top["/music"].Should().BeGreaterThanOrEqualTo(1);
        top.Keys.Should().NotContain(k => k.Contains("evil") || k.Contains("?"), "other origins and query strings are never stored");
    }

    [Fact]
    public async Task Dashboard_RefusesBadRanges()
    {
        var owner = await SignInAsync("Owner");

        (await AreaHttp.Errors(await owner.GetAsync("/api/owner/dashboard?from=ontem"))).Keys.Should().Equal("from");
        (await AreaHttp.Errors(await owner.GetAsync("/api/owner/dashboard?from=2026-01-10&to=2026-01-01"))).Keys.Should().Equal("from");
        (await AreaHttp.Errors(await owner.GetAsync("/api/owner/dashboard?from=2024-01-01&to=2026-01-01"))).Keys.Should().Equal("from");
        (await AreaHttp.Json(owner, "/api/owner/dashboard?from=2025-01-01&to=2025-12-31")).GetProperty("days").GetArrayLength().Should().Be(365);
    }

    // ---------- /owner/db ----------

    [Fact]
    public async Task Database_ListsTablesAndPagesThroughOne()
    {
        var owner = await SignInAsync("Owner");
        await SeedLogAsync("db");

        var tables = (await AreaHttp.Json(owner, "/api/owner/db/tables")).EnumerateArray().Select(t => t.GetString()).ToList();
        tables.Should().Contain("AuditLogs");

        var grid = await AreaHttp.Json(owner, "/api/owner/db/tables/AuditLogs?page=1&pageSize=10");
        grid.GetProperty("columns").EnumerateArray().Select(c => c.GetString()).Should().Contain("EntityType");
        grid.GetProperty("rows").GetArrayLength().Should().BeInRange(1, 10);
        grid.GetProperty("total").GetInt32().Should().BeGreaterThan(0);

        (await owner.GetAsync("/api/owner/db/tables/NaoExiste")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync("/api/owner/db/tables/Audit%3BLogs")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Database_SelectIsReadOnly_AndAChangeNeedsTheConfirmationAndTheSqlRules()
    {
        var owner = await SignInAsync("Owner");
        var tag = Tag();
        var log = await SeedLogAsync(tag);

        var result = await AreaHttp.Json(await owner.PostAsJsonAsync("/api/owner/db/select", new { query = $"SELECT Id, EntityType, Changes FROM AuditLogs WHERE Id = {log}" }));
        result.GetProperty("columns").EnumerateArray().Select(c => c.GetString()).Should().Equal("Id", "EntityType", "Changes");
        result.GetProperty("rows")[0][0].GetString().Should().Be(log.ToString());
        result.GetProperty("rows")[0][2].GetString().Should().Be("[NULL]");

        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/db/select", new { query = $"UPDATE AuditLogs SET Action = 'x' WHERE Id = {log}" })))
            .Keys.Should().Equal("query");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/db/select", new { query = "SELECT * FROM NaoExiste" }))).Keys.Should().Equal("query");

        var update = $"UPDATE AuditLogs SET Action = 'Mudado' WHERE Id = {log}";
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/db/modify", new { query = update, confirmed = false }))).Keys.Should().Equal("confirmed");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/db/modify", new { query = "DROP TABLE AuditLogs", confirmed = true }))).Keys.Should().Equal("query");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/owner/db/modify", new { query = $"SELECT * FROM AuditLogs", confirmed = true }))).Keys.Should().Equal("query");
        (await WithDbAsync(db => db.AuditLogs.AsNoTracking().SingleAsync(a => a.Id == log))).Action.Should().Be("Created");

        (await AreaHttp.Json(await owner.PostAsJsonAsync("/api/owner/db/modify", new { query = update, confirmed = true }))).GetProperty("rowsAffected").GetInt32().Should().Be(1);
        (await WithDbAsync(db => db.AuditLogs.AsNoTracking().SingleAsync(a => a.Id == log))).Action.Should().Be("Mudado");
    }
}

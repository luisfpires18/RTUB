using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The classification on /api/leaderboard (React track 019, was the Blazor /leaderboard) through the real host: real
/// login, antiforgery, SQLite and the services the old page used (RankingService, MemberStatisticsService,
/// LeaderboardCommentService; the story above the levels is fixed in code since 029A). XP comes from appsettings' "Ranking": 15 per attended past rehearsal,
/// Festival 100, Atuacao 50, and the level thresholds 0 / 200 / 400... Push is the recording fake of
/// <see cref="EventsApiFactory"/>: nothing is ever sent. Every test scopes its own members, since the class shares one
/// database.
/// </summary>
public partial class LeaderboardApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public LeaderboardApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_GetNothing_AndThePageIsReactForMembers()
    {
        var target = await AddAsync(MemberCategory.Tuno);
        var anonymous = Anonymous();
        await AreaHttp.WithTokenAsync(anonymous);
        foreach (var path in new[] { "/api/leaderboard", $"/api/leaderboard/members/{target.Id}", $"/api/leaderboard/members/{target.Id}/comments" })
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }

        (await anonymous.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text = "olá" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/leaderboard/comments/1/like", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync("/api/leaderboard/comments/1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var page = await anonymous.GetAsync("/leaderboard");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=%2Fleaderboard");

        var (member, _) = await SignInAsync("Member");
        var html = await member.GetStringAsync("/leaderboard");
        html.Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");

        var web = typeof(RTUB.Program).Assembly;
        web.GetType("RTUB.Pages.Activities.Leaderboard").Should().BeNull("the Blazor classification page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/leaderboard", "/members/manage", "/member/events", "/member/gallery", "/member/roles", "/hierarchy" });
    }

    // ---------- helpers ----------

    private static string Tag() => "lb" + Guid.NewGuid().ToString("N")[..8];

    private static (int Level, string? Rank, int Xp, int Rehearsals, int Events) Score(JsonElement e) =>
        (e.GetProperty("level").GetInt32(), e.GetProperty("rankName").GetString(), e.GetProperty("xp").GetInt32(),
            e.GetProperty("rehearsals").GetInt32(), e.GetProperty("events").GetInt32());

    private static JsonElement Entry(JsonElement body, ApplicationUser u) =>
        body.GetProperty("entries").EnumerateArray().Single(e => e.GetProperty("id").GetString() == u.Id);

    private static int Position(JsonElement body, ApplicationUser u) => Entry(body, u).GetProperty("position").GetInt32();

    private static int Xp(JsonElement body, ApplicationUser u) => Entry(body, u).GetProperty("xp").GetInt32();

    private static string FiscalYearOf(DateTime day)
    {
        var start = day.Month >= 9 ? day.Year : day.Year - 1;
        return $"{start}-{start + 1}";
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<ApplicationUser> AddAsync(MemberCategory category, string? nickname = null, bool expelled = false)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userName = "lb" + Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName + "@test.com",
            PhoneNumber = "919191919",
            Nickname = nickname ?? userName,
            FirstName = "Mem",
            LastName = "Bro",
            Categories = new List<MemberCategory> { category },
            IsExpelled = expelled,
        };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        return user;
    }

    private async Task AttendAsync(string userId, DateTime day, bool attended)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rehearsal = Rehearsal.Create(day, "Centro Académico");
        db.Rehearsals.Add(rehearsal);
        await db.SaveChangesAsync();
        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.WillAttend = true;
        attendance.MarkAttendance(attended);
        db.RehearsalAttendances.Add(attendance);
        await db.SaveChangesAsync();
    }

    private async Task EnrollAsync(string userId, DateTime day, EventType type, bool willAttend = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var evt = Event.Create("Atuação de teste", day, "Bragança", type);
        db.Events.Add(evt);
        await db.SaveChangesAsync();
        var enrollment = Enrollment.Create(userId, evt.Id);
        enrollment.WillAttend = willAttend;
        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync();
    }

    private async Task EnsureFiscalYearAsync(int start)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.FiscalYears.AnyAsync(f => f.StartYear == start))
        {
            db.FiscalYears.Add(FiscalYear.Create(start, start + 1));
            await db.SaveChangesAsync();
        }
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"lb{Guid.NewGuid():N}"[..20], $"10.85.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous() => AreaHttp.Anonymous(_factory, "10.86.0.1");
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The classification on /api/leaderboard (React track 019, was the Blazor /leaderboard) through the real host: real
/// login, antiforgery, SQLite and the services the old page used (RankingService, MemberStatisticsService,
/// LeaderboardCommentService, LabelService). XP comes from appsettings' "Ranking": 15 per attended past rehearsal,
/// Festival 100, Atuacao 50, and the level thresholds 0 / 200 / 400... Push is the recording fake of
/// <see cref="EventsApiFactory"/>: nothing is ever sent. Every test scopes its own members, since the class shares one
/// database.
/// </summary>
public class LeaderboardApiTests : IClassFixture<EventsApiFactory>
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
        await WithTokenAsync(anonymous);
        foreach (var path in new[] { "/api/leaderboard", $"/api/leaderboard/members/{target.Id}", $"/api/leaderboard/members/{target.Id}/comments" })
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }

        (await anonymous.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text = "olá" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/leaderboard/comments/1/like", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync("/api/leaderboard/comments/1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PutAsJsonAsync("/api/leaderboard/story", new { title = "t", content = "c", isActive = true })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var page = await anonymous.GetAsync("/leaderboard");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect);
        page.Headers.Location!.ToString().Should().Be("/login?returnUrl=%2Fleaderboard");

        var (member, _) = await SignInAsync("Member");
        var html = await member.GetStringAsync("/leaderboard");
        html.Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");

        var web = typeof(RTUB.App).Assembly;
        web.GetType("RTUB.Pages.Activities.Leaderboard").Should().BeNull("the Blazor classification page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/leaderboard", "/members/manage", "/member/events", "/member/gallery", "/member/roles", "/hierarchy" });
    }

    [Fact]
    public async Task Table_ScoresAsBefore_OrdersByLevelThenXp_AndKeepsEveryAccount()
    {
        var tag = Tag();
        var day = DateTime.UtcNow.Date.AddDays(-20);
        var rehearsals = await AddAsync(MemberCategory.Tuno, $"{tag} Ensaios");
        var festival = await AddAsync(MemberCategory.Caloiro, $"{tag} Festivais");
        var idle = await AddAsync(MemberCategory.Tuno, $"{tag} Parado");
        var leitao = await AddAsync(MemberCategory.Leitao, $"{tag} Leitao");
        var expelled = await AddAsync(MemberCategory.Leitao, $"{tag} Expulso", expelled: true);
        foreach (var offset in new[] { 0, 1, 2 })
        {
            await AttendAsync(rehearsals.Id, day.AddDays(-offset), attended: true);
        }

        await AttendAsync(rehearsals.Id, day.AddDays(-3), attended: false);
        await AttendAsync(rehearsals.Id, DateTime.UtcNow.Date.AddDays(5), attended: true);
        await EnrollAsync(festival.Id, day, EventType.Festival);
        await EnrollAsync(festival.Id, day.AddDays(-1), EventType.Festival);
        await EnrollAsync(festival.Id, day.AddDays(-2), EventType.Atuacao, willAttend: false);
        await EnrollAsync(festival.Id, DateTime.UtcNow.Date.AddDays(3), EventType.Festival);
        await AttendAsync(leitao.Id, day, attended: true);
        await AttendAsync(expelled.Id, day, attended: true);
        var (member, _) = await SignInAsync("Member");

        var body = await Json(member, "/api/leaderboard");

        var entries = body.GetProperty("entries").EnumerateArray().ToList();
        entries.Should().HaveCount(body.GetProperty("total").GetInt32());
        entries.Select(e => e.GetProperty("position").GetInt32()).Should().Equal(Enumerable.Range(1, entries.Count));
        entries.Select(e => (e.GetProperty("level").GetInt32(), e.GetProperty("xp").GetInt32()))
            .Should().BeInDescendingOrder(Comparer<(int Level, int Xp)>.Create((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level) : a.Xp.CompareTo(b.Xp)),
                "level first, then XP, as before");

        var row = (ApplicationUser u) => entries.Single(e => e.GetProperty("id").GetString() == u.Id);
        Score(row(rehearsals)).Should().Be((1, "Lei Seca", 45, 3, 0), "3 attended past rehearsals x 15; not attended and future ones do not count");
        Score(row(festival)).Should().Be((2, "Vai mais um copo", 200, 0, 2), "2 past Festivals x 100 = level 2; 'não vai' and future ones do not count");
        Score(row(idle)).Should().Be((1, "Lei Seca", 0, 0, 0));
        Score(row(leitao)).Item3.Should().Be(15, "Leitões are ranked, as before");
        Score(row(expelled)).Item3.Should().Be(15, "the old page listed every account, expelled ones too");
        row(festival).GetProperty("position").GetInt32().Should().BeLessThan(row(rehearsals).GetProperty("position").GetInt32());
        row(rehearsals).GetProperty("position").GetInt32().Should().BeLessThan(row(idle).GetProperty("position").GetInt32());

        body.GetProperty("levels").EnumerateArray().Select(l => l.GetProperty("xpThreshold").GetInt32()).Take(3).Should().Equal(0, 200, 400);
        entries[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("position", "id", "displayName", "fullName", "avatarUrl", "level",
            "rankName", "xp", "rehearsals", "events");
        body.GetRawText().Should().NotContain("@test.com").And.NotContain("919191919");
    }

    [Fact]
    public async Task SearchAndFiscalYear_FilterWithoutChangingPositionsOrDroppingMembers()
    {
        var tag = Tag();
        var today = DateTime.UtcNow.Date;
        var thisYear = today.AddDays(-1);
        var lastYear = thisYear.AddYears(-1);
        var fiscalYear = FiscalYearOf(thisYear);
        await EnsureFiscalYearAsync(int.Parse(fiscalYear[..4]));
        await EnsureFiscalYearAsync(int.Parse(fiscalYear[..4]) - 1);
        var now = await AddAsync(MemberCategory.Tuno, $"{tag} Agora");
        var before = await AddAsync(MemberCategory.Tuno, $"{tag} Antes");
        await EnrollAsync(now.Id, thisYear, EventType.Atuacao);
        await EnrollAsync(before.Id, lastYear, EventType.Festival);
        var (member, _) = await SignInAsync("Member");

        var all = await Json(member, "/api/leaderboard");
        var total = all.GetProperty("total").GetInt32();
        all.GetProperty("fiscalYears").EnumerateArray().Select(y => y.GetProperty("value").GetString()).Should().Contain(fiscalYear);
        all.GetProperty("fiscalYears").EnumerateArray().Single(y => y.GetProperty("value").GetString() == FiscalYearHelper.GetCurrentFiscalYearString())
            .GetProperty("label").GetString().Should().EndWith("(ATUAL)");

        var searched = await Json(member, $"/api/leaderboard?q={Uri.EscapeDataString(tag.ToUpperInvariant() + " agora")}");
        var hit = searched.GetProperty("entries").EnumerateArray().Single();
        hit.GetProperty("id").GetString().Should().Be(now.Id);
        hit.GetProperty("position").GetInt32().Should().Be(Position(all, now), "the search keeps the place in the full table");
        searched.GetProperty("total").GetInt32().Should().Be(total);
        (await Json(member, "/api/leaderboard?q=vai%20mais%20um")).GetProperty("entries").EnumerateArray()
            .Should().OnlyContain(e => e.GetProperty("rankName").GetString() == "Vai mais um copo", "the rank name is searched too");

        var year = await Json(member, $"/api/leaderboard?fiscalYear={fiscalYear}");
        year.GetProperty("total").GetInt32().Should().Be(total, "a fiscal year changes the scores, never who is listed");
        Xp(year, now).Should().Be(50);
        Xp(year, before).Should().Be(0, "last year's Festival is outside this fiscal year");
        Xp(all, before).Should().Be(100);

        (await member.GetAsync("/api/leaderboard?fiscalYear=1999-2000")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Details_AreTheYearsProgress_WithTheXpOriginAndActivitiesOfAllTime()
    {
        var tag = Tag();
        var thisYear = DateTime.UtcNow.Date.AddDays(-1);
        var fiscalYear = FiscalYearOf(thisYear);
        await EnsureFiscalYearAsync(int.Parse(fiscalYear[..4]));
        var target = await AddAsync(MemberCategory.Tuno, $"{tag} Alvo");
        await EnrollAsync(target.Id, thisYear.AddYears(-1), EventType.Festival);
        await EnrollAsync(target.Id, thisYear.AddYears(-1).AddDays(-1), EventType.Festival);
        await AttendAsync(target.Id, thisYear, attended: true);
        var (member, _) = await SignInAsync("Member");

        var detail = await Json(member, $"/api/leaderboard/members/{target.Id}?fiscalYear={fiscalYear}");

        detail.GetProperty("progress").GetProperty("xp").GetInt32().Should().Be(15, "the level follows the year the table shows");
        detail.GetProperty("progress").GetProperty("nextRankName").GetString().Should().Be("Vai mais um copo");
        var breakdown = detail.GetProperty("breakdown");
        breakdown.GetProperty("totalXp").GetInt32().Should().Be(215, "the XP origin is of all time, as the old modal");
        (breakdown.GetProperty("rehearsalCount").GetInt32(), breakdown.GetProperty("rehearsalXpTotal").GetInt32()).Should().Be((1, 15));
        var festival = breakdown.GetProperty("eventsByType").EnumerateArray().Single();
        (festival.GetProperty("type").GetString(), festival.GetProperty("count").GetInt32(), festival.GetProperty("xpPerUnit").GetInt32(),
            festival.GetProperty("totalXp").GetInt32()).Should().Be(("Festival", 2, 100, 200));
        detail.GetProperty("activities").GetArrayLength().Should().Be(3);
        detail.GetRawText().Should().NotContain("@test.com").And.NotContain("919191919");
        (await member.GetAsync("/api/leaderboard/members/no-such-member")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Comments_AnyMemberWrites_NewestFirst_PushToTheMember_AndValidates()
    {
        _factory.Push.Invocations.Clear();
        var target = await AddAsync(MemberCategory.Tuno);
        var (member, me) = await SignInAsync("Member");
        await WithTokenAsync(member);
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);

        (await Errors(await member.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text = "   " }))).Keys.Should().Contain("text");
        (await Errors(await member.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text = new string('a', 1001) }))).Keys.Should().Contain("text");
        (await member.PostAsJsonAsync("/api/leaderboard/members/no-such-member/comments", new { text = "olá" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await member.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text = "  primeiro  " })).StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await Json(await mod.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text = "segundo" }));
        list.EnumerateArray().Select(c => c.GetProperty("text").GetString()).Should().Equal("segundo", "primeiro");
        list[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "authorName", "authorAvatarUrl", "text", "createdAt", "likes",
            "likedByMe", "canDelete", "likedBy");
        list[1].GetProperty("canDelete").GetBoolean().Should().BeFalse("the Mod did not write it");
        _factory.Push.Verify(p => p.SendToUserAsync(target.Id, It.IsAny<SendPushNotificationDto>()), Times.Exactly(2));

        await member.PostAsJsonAsync($"/api/leaderboard/members/{me.Id}/comments", new { text = "eu" });
        _factory.Push.Verify(p => p.SendToUserAsync(me.Id, It.IsAny<SendPushNotificationDto>()), Times.Never, "no push for a comment on one's own row");
        (await Json(member, $"/api/leaderboard/members/{target.Id}/comments"))[1].GetProperty("canDelete").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Likes_Toggle_AndDelete_IsTheAuthorsOrAdminsAndOwners()
    {
        _factory.Push.Invocations.Clear();
        var target = await AddAsync(MemberCategory.Tuno);
        var (author, authorUser) = await SignInAsync("Member");
        await WithTokenAsync(author);
        var (other, _) = await SignInAsync("Mod");
        await WithTokenAsync(other);
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);
        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);

        async Task<int> CommentAsync(string text) =>
            (await Json(await author.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text })))[0].GetProperty("id").GetInt32();

        var first = await CommentAsync("um");
        (await Json(await other.PostAsync($"/api/leaderboard/comments/{first}/like", null))).GetProperty("liked").GetBoolean().Should().BeTrue();
        _factory.Push.Verify(p => p.SendToUserAsync(authorUser.Id, It.IsAny<SendPushNotificationDto>()), Times.Once, "the author hears of the like");
        var liked = (await Json(other, $"/api/leaderboard/members/{target.Id}/comments")).EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == first);
        (liked.GetProperty("likes").GetInt32(), liked.GetProperty("likedByMe").GetBoolean()).Should().Be((1, true));
        (await Json(await other.PostAsync($"/api/leaderboard/comments/{first}/like", null))).GetProperty("liked").GetBoolean().Should().BeFalse();

        (await other.DeleteAsync($"/api/leaderboard/comments/{first}")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "a Mod deletes only their own");
        (await author.DeleteAsync($"/api/leaderboard/comments/{first}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await author.DeleteAsync($"/api/leaderboard/comments/{first}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostAsync($"/api/leaderboard/comments/{first}/like", null)).StatusCode.Should().Be(HttpStatusCode.NotFound, "a deleted comment cannot be liked");
        (await Json(author, $"/api/leaderboard/members/{target.Id}/comments")).EnumerateArray().Should().NotContain(c => c.GetProperty("id").GetInt32() == first);

        (await admin.DeleteAsync($"/api/leaderboard/comments/{await CommentAsync("dois")}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.DeleteAsync($"/api/leaderboard/comments/{await CommentAsync("três")}")).StatusCode.Should().Be(HttpStatusCode.NoContent, "the Owner inherits Admin");
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LeaderboardComments.IgnoreQueryFilters()
            .CountAsync(c => c.TargetUserId == target.Id && c.DeletedAt != null)).Should().Be(3, "deletes stay soft, as before");
    }

    [Fact]
    public async Task Writes_NeedTheAntiforgeryHeader()
    {
        var target = await AddAsync(MemberCategory.Tuno);
        var (member, _) = await SignInAsync("Admin");

        (await member.PostAsJsonAsync($"/api/leaderboard/members/{target.Id}/comments", new { text = "sem token" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PostAsync("/api/leaderboard/comments/1/like", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.DeleteAsync("/api/leaderboard/comments/1")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await member.PutAsJsonAsync("/api/leaderboard/story", new { title = "t", content = "c", isActive = true })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await Json(member, $"/api/leaderboard/members/{target.Id}/comments")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Story_IsEditedByAdminAndOwnerOnly_WithTheLabelRules()
    {
        await EnsureStoryAsync();
        var input = new { title = "Como sobes de nível", content = "Linha 1\nLinha 2", isActive = true };
        foreach (var role in new[] { "Member", "Mod" })
        {
            var (client, _) = await SignInAsync(role);
            await WithTokenAsync(client);
            (await client.PutAsJsonAsync("/api/leaderboard/story", input)).StatusCode.Should().Be(HttpStatusCode.Forbidden, role);
            (await Json(client, "/api/leaderboard?q=zzz-none")).GetProperty("canEditStory").GetBoolean().Should().BeFalse();
        }

        foreach (var role in new[] { "Admin", "Owner" })
        {
            var (client, _) = await SignInAsync(role);
            await WithTokenAsync(client);
            (await Json(client, "/api/leaderboard?q=zzz-none")).GetProperty("canEditStory").GetBoolean().Should().BeTrue(role);
            (await Errors(await client.PutAsJsonAsync("/api/leaderboard/story", new { title = "", content = new string('x', 5001), isActive = true })))
                .Keys.Should().BeEquivalentTo("title", "content");
            (await client.PutAsJsonAsync("/api/leaderboard/story", input with { title = $"{input.title} {role}" })).StatusCode.Should().Be(HttpStatusCode.OK);
            var story = (await Json(client, "/api/leaderboard?q=zzz-none")).GetProperty("story");
            (story.GetProperty("title").GetString(), story.GetProperty("content").GetString()).Should().Be(($"{input.title} {role}", input.content));
        }
    }

    [Fact]
    public void ReactFiles_SayNothingAboutMigration_AndTheProfileLinksTheClassification()
    {
        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        File.ReadAllText(Path.Combine(src, "Leaderboard.tsx")).Should().NotContainAny("migra", "Migra");
        File.ReadAllText(Path.Combine(src, "content.ts")).Should().Contain("leaderboard: '/leaderboard'");
        File.ReadAllText(Path.Combine(src, "Profile.tsx")).Should().Contain("href={portal.leaderboard}");
        File.ReadAllText(Path.Combine(RepoRoot(), "src", "RTUB.Web", "Shared", "MainLayout.razor")).Should().Contain("href=\"/leaderboard\"");
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

    private static async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
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

    private async Task EnsureStoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.Labels.AnyAsync(l => l.Reference == "ranking_story"))
        {
            db.Labels.Add(Label.Create("ranking_story", "Ranking", "Texto"));
            await db.SaveChangesAsync();
        }
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"lb{Guid.NewGuid():N}"[..20], $"10.85.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.86.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "RTUB.Web")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}

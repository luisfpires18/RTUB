using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// The React members area on /api/members (React track 017, was the Blazor /members and /hierarchy) through the real
/// host: real login, SQLite and the services the old page used. Every test scopes its own members with a unique tag,
/// since the class shares one database.
/// </summary>
public partial class MembersApiTests : IClassFixture<EventsApiFactory>
{
    private readonly EventsApiFactory _factory;

    public MembersApiTests(EventsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Visitors_GetNothing()
    {
        var anonymous = Anonymous();
        foreach (var path in new[] { "/api/members", "/api/members/active", "/api/members/birthdays", "/api/members/hierarchy", "/api/members/x" })
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }
    }

    [Fact]
    public async Task Directory_GroupsMembersAndLeitoes_WithTheOldFilters_AndNoContactFields()
    {
        var tag = Tag();
        var tuno = await AddAsync($"{tag} Bravo", MemberCategory.Tuno, yearTuno: DateTime.Now.Year - 1);
        var caloiro = await AddAsync($"{tag} Alfa", MemberCategory.Caloiro);
        var leitao = await AddAsync($"{tag} Leitão", MemberCategory.Leitao);
        var honorario = await AddAsync($"{tag} Honra", MemberCategory.TunoHonorario);
        await AddInstrumentAsync(tuno.Id, InstrumentType.Bandolim);
        var (member, _) = await SignInAsync("Member");

        var page = await member.GetAsync($"/api/members?q={tag}");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Headers.CacheControl!.NoStore.Should().BeTrue();
        var raw = await page.Content.ReadAsStringAsync();
        raw.Should().NotContain("@test.com").And.NotContain("919191919").And.NotContain("Bragança-Cidade")
            .And.NotContainAny(new[] { "email", "phone", "birth", "city", "passwordHash", "lastLogin\"" });
        var body = JsonDocument.Parse(raw).RootElement;
        Names(body, "members").Should().Equal(new[] { $"{tag} Alfa", $"{tag} Bravo", $"{tag} Honra" },
            "by nickname; a search also finds Tunos Honorários, as before");
        Names(body, "leitoes").Should().Equal($"{tag} Leitão");
        var bravo = body.GetProperty("members")[1];
        bravo.GetProperty("instrument").GetString().Should().Be("Bandolim");
        bravo.GetProperty("badges").EnumerateArray().Select(b => b.GetProperty("label").GetString()).Should().Equal("TUNO");
        body.GetProperty("leitoes")[0].GetProperty("inactive").GetBoolean().Should().BeTrue("no activity in the last 15 days");

        // Fix 064: the last 15 days, not the calendar month, so a Leitão is not flagged on the 1st.
        await AttendAsync(leitao.Id, DateTime.Today.AddDays(-20));
        (await AreaHttp.Json(member, $"/api/members?q={tag}"))
            .GetProperty("leitoes")[0].GetProperty("inactive").GetBoolean().Should().BeTrue("20 days ago is outside the window");
        await AttendAsync(leitao.Id, DateTime.Today.AddDays(-10));
        (await AreaHttp.Json(member, $"/api/members?q={tag}"))
            .GetProperty("leitoes")[0].GetProperty("inactive").GetBoolean().Should().BeFalse("10 days ago counts, whatever the month");
        body.GetProperty("canManage").GetBoolean().Should().BeFalse();

        Names(await AreaHttp.Json(member, $"/api/members?q={tag}&category=Caloiro"), "members").Should().Equal($"{tag} Alfa");
        Names(await AreaHttp.Json(member, $"/api/members?q={tag}&category=TunoHonorario"), "members").Should().Equal($"{tag} Honra");
        Names(await AreaHttp.Json(member, $"/api/members?q={tag}&category=Leitao"), "leitoes").Should().Equal($"{tag} Leitão");
        Names(await AreaHttp.Json(member, $"/api/members?q={tag}&instrument=Bandolim"), "members").Should().Equal($"{tag} Bravo");
        Names(await AreaHttp.Json(member, $"/api/members?q={tag}&category=Tuno&subCategory=Tuno"), "members").Should().Equal($"{tag} Bravo");
        Names(await AreaHttp.Json(member, $"/api/members?q={tuno.UserName}@test"), "members").Should().Equal(new[] { $"{tag} Bravo" },
            "the search matches the email, as before, without returning it");

        var all = await AreaHttp.Json(member, "/api/members");
        all.GetProperty("members").EnumerateArray().Should().NotContain(m => m.GetProperty("id").GetString() == honorario.Id,
            "with no filter or search, Tunos Honorários stay out of the general view");

        foreach (var bad in new[] { "category=Nope", "instrument=Kazoo", "category=Tuno&subCategory=Leitao" })
        {
            (await member.GetAsync($"/api/members?{bad}")).StatusCode.Should().Be(HttpStatusCode.BadRequest, bad);
        }

        _ = caloiro;
        _ = leitao;
    }

    [Theory]
    [InlineData("Member", false)]
    [InlineData("Mod", false)]
    [InlineData("Admin", true)]
    [InlineData("Owner", true)]
    public async Task OnlyAdminAndOwner_GetTheManagementTools(string role, bool canManage)
    {
        var (client, _) = await SignInAsync(role);

        (await AreaHttp.Json(client, "/api/members?q=zzz-none")).GetProperty("canManage").GetBoolean().Should().Be(canManage);
    }

    // ---------- helpers ----------

    private static string Tag() => "m" + Guid.NewGuid().ToString("N")[..8];

    private static IEnumerable<string?> Names(JsonElement body, string group) =>
        body.GetProperty(group).EnumerateArray().Select(m => m.GetProperty("displayName").GetString());

    private async Task<ApplicationUser> AddAsync(string nickname, MemberCategory category, int? yearTuno = null, string? mentorId = null,
        int? yearLeitao = null, int? yearCaloiro = null, bool retired = false, DateTime? birth = null)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userName = "mem" + Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName + "@test.com",
            PhoneNumber = "919191919",
            City = "Bragança-Cidade",
            DateOfBirth = birth ?? new DateTime(2000, 2, 1),
            Nickname = nickname,
            FirstName = "Mem",
            LastName = "Bro",
            Categories = new List<MemberCategory> { category },
            YearTuno = yearTuno ?? (category == MemberCategory.TunoHonorario ? 2015 : null),
            MonthTuno = yearTuno is null ? null : 1,
            YearLeitao = yearLeitao,
            YearCaloiro = yearCaloiro,
            MonthCaloiro = yearCaloiro is null ? null : 3,
            MentorId = mentorId,
            IsRetired = retired
        };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        return user;
    }

    private async Task AddInstrumentAsync(string userId, InstrumentType instrument)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.MemberInstruments.Add(MemberInstrument.Create(userId, instrument, isPrimary: true));
        await db.SaveChangesAsync();
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"mem{Guid.NewGuid():N}"[..20], $"10.81.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous() => AreaHttp.Anonymous(_factory, "10.82.0.1");

    private async Task AttendAsync(string userId, DateTime day)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rehearsal = Rehearsal.Create(day, "Centro Académico");
        db.Rehearsals.Add(rehearsal);
        await db.SaveChangesAsync();
        var attendance = RehearsalAttendance.Create(rehearsal.Id, userId);
        attendance.MarkAttendance(true);
        db.RehearsalAttendances.Add(attendance);
        await db.SaveChangesAsync();
    }
}

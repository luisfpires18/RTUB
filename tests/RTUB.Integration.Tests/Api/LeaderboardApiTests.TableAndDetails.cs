// LeaderboardApiTests: TableAndDetails tests.
using System.Net;
using System.Text.Json;
using FluentAssertions;
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

namespace RTUB.Integration.Tests.Api;

public partial class LeaderboardApiTests
{
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
        var (member, memberUser) = await SignInAsync("Member");

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
        searched.GetProperty("me").GetProperty("id").GetString().Should().Be(memberUser.Id, "'me' is the reader's own row whatever the search (#225)");
        searched.GetProperty("me").GetProperty("position").GetInt32().Should().Be(Position(all, memberUser));
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
}

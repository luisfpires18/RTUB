using System.Globalization;
using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/trintuna Hall of Fame (#368): everyone who can see the page (Owner-role users and listed members, not
/// expelled), by Vou on past gatherings, zeros included; dense places, ties share a place (by name); derived on every
/// read. Each test clears the answers first: the class shares one database, so its other Owners sit at 0 too.
/// </summary>
public partial class TrintunaApiTests
{
    private static readonly StringComparer ByName = StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), ignoreCase: true);

    private static async Task AnswerAsync(HttpClient who, int gathering, bool willAttend) =>
        (await who.PutAsJsonAsync($"/api/trintuna/gatherings/{gathering}/presence", new { willAttend })).StatusCode.Should().Be(HttpStatusCode.OK);

    private static async Task<List<(int Place, string? Name, int Count)>> HallAsync(HttpClient client) =>
        (await AreaHttp.Json(client, "/api/trintuna")).GetProperty("hallOfFame").EnumerateArray()
            .Select(r => (r.GetProperty("place").GetInt32(), r.GetProperty("displayName").GetString(), r.GetProperty("count").GetInt32()))
            .ToList();

    [Fact]
    public async Task HallOfFame_ListsEveryoneWhoCanSee_ZerosIncluded_DenseTiesByName_PastOnly()
    {
        await ClearAnswersAsync();
        var (owner, boss) = await SignInAsync("Owner");
        var (a, userA) = await SignInAsync();
        var (b, userB) = await SignInAsync();
        var (c, userC) = await SignInAsync();
        var (d, userD) = await SignInAsync();
        var (e, userE) = await SignInAsync();
        var (x, userX) = await SignInAsync();
        await SetListAsync(owner, userA.Id, userB.Id, userC.Id, userD.Id, userE.Id, userX.Id);
        var first = await SeedGatheringAsync(historical: true);
        var second = await SeedGatheringAsync(historical: true);
        var upcoming = Id(await CreateAsync(owner));

        await AnswerAsync(a, first, true);
        await AnswerAsync(a, second, true);
        await AnswerAsync(b, first, true);
        await AnswerAsync(b, second, true);
        await AnswerAsync(c, first, true);
        await AnswerAsync(c, upcoming, true);
        await AnswerAsync(d, first, false);
        await AnswerAsync(d, upcoming, true);
        await AnswerAsync(e, first, true);
        await AnswerAsync(x, first, true);

        // e is taken off the list, x is expelled while listed: neither is ranked (their answers stay stored).
        await SetListAsync(owner, userA.Id, userB.Id, userC.Id, userD.Id, userX.Id);
        await ExpelAsync(userX.Id);

        var hall = await HallAsync(owner);
        var tied = new[] { userA.Nickname, userB.Nickname }.OrderBy(n => n, ByName).ToArray();
        var mine = new[] { userA.Nickname, userB.Nickname, userC.Nickname, userD.Nickname, boss.Nickname };
        hall.Where(r => mine.Contains(r.Name)).Take(3).Should().Equal((1, tied[0], 2), (1, tied[1], 2), (2, userC.Nickname, 1));
        hall.Should().Contain((3, userD.Nickname, 0), "Não vou and a Vou on an upcoming gathering count nothing")
            .And.Contain((3, boss.Nickname, 0), "the Owner is listed with 0");
        hall.Select(r => r.Name).Should().NotContain(new[] { userE.Nickname, userX.Nickname });
        hall.Select(r => r.Place).Should().BeInAscendingOrder();
        hall.Where(r => r.Place == 3).Select(r => r.Name).Should().BeInAscendingOrder(ByName);

        var raw = await AreaHttp.Json(a, "/api/trintuna");
        raw.TryGetProperty("top", out _).Should().BeFalse("the Top 3 is now the Hall of Fame");
        raw.GetProperty("hallOfFame").GetRawText().Should().NotContain(userA.Id, "no user id in the Hall of Fame");
        await ClearAnswersAsync();
    }

    [Fact]
    public async Task HallOfFame_RowKeysStayUnique_WhenMembersShareNameAndHaveNoPhoto()
    {
        await ClearAnswersAsync();
        var (owner, _) = await SignInAsync("Owner");
        var (a, userA) = await SignInAsync();
        var (b, userB) = await SignInAsync();
        await SetListAsync(owner, userA.Id, userB.Id);
        using (var scope = _factory.Services.CreateScope())
        {
            await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
            await db.Users.Where(u => u.Id == userA.Id || u.Id == userB.Id).ExecuteUpdateAsync(u => u
                .SetProperty(x => x.Nickname, "Gémeo").SetProperty(x => x.FirstName, "Gé").SetProperty(x => x.LastName, "Meo")
                .SetProperty(x => x.ImageUrl, (string?)null));
        }

        var past = await SeedGatheringAsync(historical: true);
        await AnswerAsync(a, past, true);
        await AnswerAsync(b, past, true);

        var top = (await AreaHttp.Json(a, "/api/trintuna")).GetProperty("hallOfFame").EnumerateArray()
            .Where(r => r.GetProperty("displayName").GetString() == "Gémeo").ToList();
        top.Select(r => r.GetProperty("displayName").GetString()).Distinct().Should().HaveCount(1, "both rows show the same name");
        var keys = top.Select(r => r.GetProperty("key").GetString()).ToList();
        keys.Should().HaveCount(2).And.OnlyHaveUniqueItems().And.NotContainNulls();
        keys.Should().NotContain(new[] { userA.Id, userB.Id }, "the key is not the user id");

        var gathering = (await AreaHttp.Json(a, "/api/trintuna")).GetProperty("past").EnumerateArray().Single(g => g.GetProperty("id").GetInt32() == past);
        var going = gathering.GetProperty("going").EnumerateArray().Select(p => p.GetProperty("key").GetString()).ToList();
        going.Should().HaveCount(2).And.OnlyHaveUniqueItems().And.NotContainNulls();
        going.Should().BeEquivalentTo(keys, "an attendee keeps the same key as in the Hall of Fame");
        await ClearAnswersAsync();
    }

    [Fact]
    public async Task HallOfFame_ChangesWhenAVouIsAddedOrRemoved()
    {
        await ClearAnswersAsync();
        var (owner, _) = await SignInAsync("Owner");
        var (member, me) = await SignInAsync();
        await SetListAsync(owner, me.Id);
        var historical = await SeedGatheringAsync(historical: true);
        (await HallAsync(member)).Should().Contain((1, me.Nickname, 0)).And.OnlyContain(r => r.Count == 0);

        await AnswerAsync(member, historical, true);
        (await HallAsync(member)).First().Should().Be((1, me.Nickname, 1));

        await AnswerAsync(member, historical, false);
        (await HallAsync(member)).Should().Contain((1, me.Nickname, 0), "Não vou counts nothing");

        await AnswerAsync(member, historical, true);
        (await member.DeleteAsync($"/api/trintuna/gatherings/{historical}/presence")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await HallAsync(member)).Should().OnlyContain(r => r.Count == 0);
    }

    private async Task ExpelAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsExpelled, true));
    }

    [Fact]
    public async Task AnUnlistedMember_Gets404OnPastPresence()
    {
        var (owner, _) = await SignInAsync("Owner");
        await SetListAsync(owner);
        var (member, _) = await SignInAsync();
        var historical = await SeedGatheringAsync(historical: true);

        (await member.PutAsJsonAsync($"/api/trintuna/gatherings/{historical}/presence", new { willAttend = true })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.DeleteAsync($"/api/trintuna/gatherings/{historical}/presence")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

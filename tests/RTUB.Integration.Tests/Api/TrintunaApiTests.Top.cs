using System.Globalization;
using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/trintuna Top 3 (#366): Vou on past gatherings by member, dense places 1-3, ties share a place (by name),
/// 0 never shown, derived on every read. Each test clears the answers first: the class shares one database.
/// </summary>
public partial class TrintunaApiTests
{
    private static readonly StringComparer ByName = StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), ignoreCase: true);

    private static async Task AnswerAsync(HttpClient who, int gathering, bool willAttend) =>
        (await who.PutAsJsonAsync($"/api/trintuna/gatherings/{gathering}/presence", new { willAttend })).StatusCode.Should().Be(HttpStatusCode.OK);

    private static async Task<List<(int Place, string? Name, int Count)>> TopAsync(HttpClient client) =>
        (await AreaHttp.Json(client, "/api/trintuna")).GetProperty("top").EnumerateArray()
            .Select(r => (r.GetProperty("place").GetInt32(), r.GetProperty("displayName").GetString(), r.GetProperty("count").GetInt32()))
            .ToList();

    [Fact]
    public async Task Top_CountsVouOnPastGatherings_DenseTiesByName_ZeroExcluded()
    {
        await ClearAnswersAsync();
        var (owner, _) = await SignInAsync("Owner");
        var (a, userA) = await SignInAsync();
        var (b, userB) = await SignInAsync();
        var (c, userC) = await SignInAsync();
        var (d, userD) = await SignInAsync();
        await SetListAsync(owner, userA.Id, userB.Id, userC.Id, userD.Id);
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

        var tied = new[] { userA.Nickname, userB.Nickname }.OrderBy(n => n, ByName).ToArray();
        (await TopAsync(a)).Should().Equal((1, tied[0], 2), (1, tied[1], 2), (2, userC.Nickname, 1));
        (await AreaHttp.Json(a, "/api/trintuna")).GetProperty("top").GetRawText().Should().NotContain(userA.Id, "no user id in the Top 3");
        await ClearAnswersAsync();
    }

    [Fact]
    public async Task Top_RowKeysStayUnique_WhenMembersShareNameAndHaveNoPhoto()
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

        var top = (await AreaHttp.Json(a, "/api/trintuna")).GetProperty("top").EnumerateArray().ToList();
        top.Select(r => r.GetProperty("displayName").GetString()).Distinct().Should().HaveCount(1, "both rows show the same name");
        var keys = top.Select(r => r.GetProperty("key").GetString()).ToList();
        keys.Should().HaveCount(2).And.OnlyHaveUniqueItems().And.NotContainNulls();
        keys.Should().NotContain(new[] { userA.Id, userB.Id }, "the key is not the user id");

        var gathering = (await AreaHttp.Json(a, "/api/trintuna")).GetProperty("past").EnumerateArray().Single(g => g.GetProperty("id").GetInt32() == past);
        var going = gathering.GetProperty("going").EnumerateArray().Select(p => p.GetProperty("key").GetString()).ToList();
        going.Should().HaveCount(2).And.OnlyHaveUniqueItems().And.NotContainNulls();
        going.Should().BeEquivalentTo(keys, "an attendee keeps the same key as in the Top 3");
        await ClearAnswersAsync();
    }

    [Fact]
    public async Task Top_ChangesWhenAVouIsAddedOrRemoved()
    {
        await ClearAnswersAsync();
        var (owner, _) = await SignInAsync("Owner");
        var (member, me) = await SignInAsync();
        await SetListAsync(owner, me.Id);
        var historical = await SeedGatheringAsync(historical: true);
        (await TopAsync(member)).Should().BeEmpty();

        await AnswerAsync(member, historical, true);
        (await TopAsync(member)).Should().Equal((1, me.Nickname, 1));

        await AnswerAsync(member, historical, false);
        (await TopAsync(member)).Should().BeEmpty("Não vou counts nothing");

        await AnswerAsync(member, historical, true);
        (await member.DeleteAsync($"/api/trintuna/gatherings/{historical}/presence")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await TopAsync(member)).Should().BeEmpty();
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

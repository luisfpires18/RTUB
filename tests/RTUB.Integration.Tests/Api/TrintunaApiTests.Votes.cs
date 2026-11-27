using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/trintuna task 166: the date poll is multi-choice. A member votes for one or more dates (<c>optionIds</c>), a new
/// vote replaces their set, each date counts its members, and <c>totalVotes</c> counts the members who voted.
/// </summary>
public partial class TrintunaApiTests
{
    private static Task<HttpResponseMessage> VoteAsync(HttpClient who, JsonElement gathering, params string[] dates) =>
        who.PutAsJsonAsync($"/api/trintuna/gatherings/{Id(gathering)}/vote", new { optionIds = dates.Select(d => Option(gathering, d)).ToArray() });

    private static int[] Mine(JsonElement gathering) =>
        gathering.GetProperty("poll").GetProperty("myOptionIds").EnumerateArray().Select(o => o.GetInt32()).ToArray();

    [Fact]
    public async Task AMember_VotesForSeveralDates_AndEachDateCountsItsMembers()
    {
        var (owner, _) = await SignInAsync("Owner");
        var (first, one) = await SignInAsync();
        var (second, two) = await SignInAsync();
        await SetListAsync(owner, one.Id, two.Id);
        string a = InDays(50), b = InDays(51), c = InDays(52);
        var gathering = await CreateAsync(owner, null, a, b, c);
        gathering.GetProperty("poll").GetProperty("multiple").GetBoolean().Should().BeTrue();
        Mine(gathering).Should().BeEmpty();

        var voted = await AreaHttp.Json(await VoteAsync(first, gathering, b, a));
        Mine(voted).Should().Equal(Option(gathering, a), Option(gathering, b));
        voted.GetProperty("poll").GetProperty("myOptionId").GetInt32().Should().Be(Option(gathering, a), "the first chosen date, for older pages");
        voted.GetProperty("poll").GetProperty("totalVotes").GetInt32().Should().Be(1, "one member voted");
        (Votes(voted, a), Votes(voted, b), Votes(voted, c)).Should().Be((1, 1, 0));

        voted = await AreaHttp.Json(await VoteAsync(second, gathering, b));
        voted.GetProperty("poll").GetProperty("totalVotes").GetInt32().Should().Be(2);
        (Votes(voted, a), Votes(voted, b), Votes(voted, c)).Should().Be((1, 2, 0));
        Mine(voted).Should().Equal(Option(gathering, b));

        // A new set replaces the earlier one: a dropped, b kept, c added.
        voted = await AreaHttp.Json(await VoteAsync(first, gathering, b, c));
        Mine(voted).Should().Equal(Option(gathering, b), Option(gathering, c));
        voted.GetProperty("poll").GetProperty("totalVotes").GetInt32().Should().Be(2);
        (Votes(voted, a), Votes(voted, b), Votes(voted, c)).Should().Be((0, 2, 1));

        // A page of the previous release sends one optionId: a one-date set.
        voted = await AreaHttp.Json(await first.PutAsJsonAsync($"/api/trintuna/gatherings/{Id(gathering)}/vote", new { optionId = Option(gathering, a) }));
        Mine(voted).Should().Equal(Option(gathering, a));
        (Votes(voted, a), Votes(voted, b), Votes(voted, c)).Should().Be((1, 1, 0));
    }

    [Fact]
    public async Task AVote_WithNoDates_ARepeatedDate_OrADateOfAnotherGathering_Is400()
    {
        var (owner, _) = await SignInAsync("Owner");
        var (member, me) = await SignInAsync();
        await SetListAsync(owner, me.Id);
        string a = InDays(60), b = InDays(61);
        var gathering = await CreateAsync(owner, null, a, b);
        var other = await CreateAsync(owner, null, InDays(62));
        var vote = $"/api/trintuna/gatherings/{Id(gathering)}/vote";

        (await AreaHttp.Errors(await member.PutAsJsonAsync(vote, new { optionIds = Array.Empty<int>() }))).Should().ContainKey("optionIds");
        (await AreaHttp.Errors(await member.PutAsJsonAsync(vote, new { }))).Should().ContainKey("optionIds");
        (await AreaHttp.Errors(await member.PutAsJsonAsync(vote, new { optionIds = new[] { Option(gathering, a), Option(gathering, a) } }))).Should().ContainKey("optionIds");
        (await AreaHttp.Errors(await member.PutAsJsonAsync(vote, new { optionIds = new[] { Option(gathering, a), Option(other, InDays(62)) } }))).Should().ContainKey("optionIds");

        var unchanged = await AreaHttp.Json(member, "/api/trintuna");
        unchanged.GetProperty("upcoming").EnumerateArray().Single(g => Id(g) == Id(gathering)).GetProperty("poll").GetProperty("totalVotes").GetInt32()
            .Should().Be(0, "a refused vote writes nothing");
    }

    [Fact]
    public async Task Voting_WithoutTheAntiforgeryHeader_Is400()
    {
        var (owner, _) = await SignInAsync("Owner");
        var (bare, _) = await SignInAsync("Owner", token: false);
        var gathering = await CreateAsync(owner, null, InDays(70));

        (await VoteAsync(bare, gathering, InDays(70))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

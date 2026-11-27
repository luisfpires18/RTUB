using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace RTUB.Integration.Tests.Api;

/// <summary>/api/trintuna gatherings: create and edit, the date poll, settling the date and Vou / Não vou (past ones too).</summary>
public partial class TrintunaApiTests
{
    private static string InDays(int days) => DateTime.Today.AddDays(days).ToString("yyyy-MM-dd");

    private static int Id(JsonElement gathering) => gathering.GetProperty("id").GetInt32();

    private static int Option(JsonElement gathering, string date) =>
        gathering.GetProperty("poll").GetProperty("options").EnumerateArray().First(o => o.GetProperty("date").GetString() == date).GetProperty("id").GetInt32();

    private static int Votes(JsonElement gathering, string date) =>
        gathering.GetProperty("poll").GetProperty("options").EnumerateArray().First(o => o.GetProperty("date").GetString() == date).GetProperty("votes").GetInt32();

    [Fact]
    public async Task TheOwnerChecksTheOrganizer_AndTheOrganizerEditsOnlyTheirOwn()
    {
        var (owner, _) = await SignInAsync("Owner");
        var (organizer, who) = await SignInAsync();
        var (other, someone) = await SignInAsync();
        await SetListAsync(owner, someone.Id);

        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/trintuna/gatherings", Input(who.Id)))).Should().ContainKey("organizerUserId");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/trintuna/gatherings", new { place = " ", pollDates = new[] { "amanhã" } })))
            .Should().ContainKeys("place", "pollDates");

        await SetListAsync(owner, someone.Id, who.Id);
        var mine = await CreateAsync(owner, who.Id);
        var theirs = await CreateAsync(owner, someone.Id);
        mine.GetProperty("organizer").GetProperty("displayName").GetString().Should().Be(who.Nickname);
        mine.GetProperty("organizerUserId").GetString().Should().Be(who.Id);

        var edited = await AreaHttp.Json(await organizer.PutAsJsonAsync($"/api/trintuna/gatherings/{Id(mine)}",
            new { place = "Amarante", description = "Doces", organizerUserId = someone.Id, pollDates = new[] { InDays(20) } }));
        edited.GetProperty("place").GetString().Should().Be("Amarante");
        edited.GetProperty("number").GetInt32().Should().Be(mine.GetProperty("number").GetInt32());
        edited.GetProperty("organizer").GetProperty("displayName").GetString().Should().Be(who.Nickname);
        edited.GetProperty("organizerUserId").ValueKind.Should().Be(JsonValueKind.Null, "only the Owner gets user ids");
        edited.GetProperty("canEdit").GetBoolean().Should().BeTrue();

        (await organizer.PutAsJsonAsync($"/api/trintuna/gatherings/{Id(theirs)}", Input())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await other.GetAsync("/api/trintuna")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await owner.DeleteAsync($"/api/trintuna/gatherings/{Id(theirs)}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.DeleteAsync($"/api/trintuna/gatherings/{Id(theirs)}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Votes_ReplaceTheEarlierSet_AndKeptDatesKeepTheirVotes()
    {
        var (owner, _) = await SignInAsync("Owner");
        var (member, me) = await SignInAsync();
        await SetListAsync(owner, me.Id);
        string a = InDays(30), b = InDays(31), c = InDays(32);
        var gathering = await CreateAsync(owner, null, a, b);
        gathering.GetProperty("poll").GetProperty("canVote").GetBoolean().Should().BeTrue();

        var voted = await AreaHttp.Json(await member.PutAsJsonAsync($"/api/trintuna/gatherings/{Id(gathering)}/vote", new { optionId = Option(gathering, a) }));
        voted = await AreaHttp.Json(await member.PutAsJsonAsync($"/api/trintuna/gatherings/{Id(gathering)}/vote", new { optionId = Option(gathering, b) }));
        voted.GetProperty("poll").GetProperty("totalVotes").GetInt32().Should().Be(1);
        voted.GetProperty("poll").GetProperty("myOptionId").GetInt32().Should().Be(Option(gathering, b));
        Votes(voted, b).Should().Be(1);

        var other = await CreateAsync(owner, null, c);
        (await AreaHttp.Errors(await member.PutAsJsonAsync($"/api/trintuna/gatherings/{Id(gathering)}/vote", new { optionId = Option(other, c) }))).Should().ContainKey("optionIds");

        // b kept (its vote stays), a removed, c added.
        var edited = await AreaHttp.Json(await owner.PutAsJsonAsync($"/api/trintuna/gatherings/{Id(gathering)}", Input(null, b, c)));
        edited.GetProperty("poll").GetProperty("options").EnumerateArray().Select(o => o.GetProperty("date").GetString()).Should().Equal(b, c);
        Votes(edited, b).Should().Be(1);
        Option(edited, b).Should().Be(Option(gathering, b));
    }

    [Fact]
    public async Task SettlingTakesOneOfItsOwnOptions_ClosesThePoll_AndHappensOnce()
    {
        var (owner, _) = await SignInAsync("Owner");
        var (organizer, who) = await SignInAsync();
        var (member, me) = await SignInAsync();
        await SetListAsync(owner, who.Id, me.Id);
        string a = InDays(40), b = InDays(41);
        var gathering = await CreateAsync(owner, who.Id, a, b);
        var other = await CreateAsync(owner, null, InDays(42));
        var settle = $"/api/trintuna/gatherings/{Id(gathering)}/settle";

        (await AreaHttp.Errors(await organizer.PostAsJsonAsync(settle, new { optionId = Option(other, InDays(42)) }))).Should().ContainKey("optionId");
        (await AreaHttp.Errors(await organizer.PostAsJsonAsync(settle, new { optionId = 987654 }))).Should().ContainKey("optionId");
        (await member.PostAsJsonAsync(settle, new { optionId = Option(gathering, b) })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var settled = await AreaHttp.Json(await organizer.PostAsJsonAsync(settle, new { optionId = Option(gathering, b) }));
        settled.GetProperty("date").GetString().Should().Be(b);
        settled.GetProperty("poll").GetProperty("closed").GetBoolean().Should().BeTrue();
        settled.GetProperty("poll").GetProperty("canVote").GetBoolean().Should().BeFalse();
        settled.GetProperty("past").GetBoolean().Should().BeFalse();

        (await organizer.PostAsJsonAsync(settle, new { optionId = Option(gathering, a) })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var historical = await SeedGatheringAsync(historical: true);
        (await owner.PostAsJsonAsync($"/api/trintuna/gatherings/{historical}/settle", new { optionId = 1 })).StatusCode.Should().Be(HttpStatusCode.Conflict, "a historical gathering has no poll");
        (await member.PutAsJsonAsync($"/api/trintuna/gatherings/{historical}/vote", new { optionId = 1 })).StatusCode.Should().Be(HttpStatusCode.Conflict, "votes stay closed on a historical gathering");
        (await member.PutAsJsonAsync($"/api/trintuna/gatherings/{Id(gathering)}/vote", new { optionId = Option(gathering, a) })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AreaHttp.Errors(await organizer.PutAsJsonAsync($"/api/trintuna/gatherings/{Id(gathering)}", Input(null, a)))).Should().ContainKey("pollDates");
        (await AreaHttp.Json(await organizer.PutAsJsonAsync($"/api/trintuna/gatherings/{Id(gathering)}", new { place = "Régua", pollDates = new[] { a, b } })))
            .GetProperty("date").GetString().Should().Be(b, "the place still changes; the dates and the date do not");
    }

    [Fact]
    public async Task Presence_WorksOnUpcomingDatedPastAndHistoricalGatherings()
    {
        var (owner, _) = await SignInAsync("Owner");
        var (member, me) = await SignInAsync();
        await SetListAsync(owner, me.Id);
        var gathering = await CreateAsync(owner);
        var presence = $"/api/trintuna/gatherings/{Id(gathering)}/presence";

        var going = await AreaHttp.Json(await member.PutAsJsonAsync(presence, new { willAttend = true }));
        going.GetProperty("myStatus").GetString().Should().Be("going");
        going.GetProperty("goingCount").GetInt32().Should().Be(1);
        going.GetProperty("going").EnumerateArray().Select(p => p.GetProperty("displayName").GetString()).Should().Equal(me.Nickname);
        var notGoing = await AreaHttp.Json(await member.PutAsJsonAsync(presence, new { willAttend = false }));
        notGoing.GetProperty("myStatus").GetString().Should().Be("notGoing");
        notGoing.GetProperty("goingCount").GetInt32().Should().Be(0);
        notGoing.GetProperty("going").GetArrayLength().Should().Be(0);
        notGoing.GetProperty("notGoing").EnumerateArray().Select(p => p.GetProperty("displayName").GetString()).Should().Equal(me.Nickname);
        (await member.DeleteAsync(presence)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AreaHttp.Json(member, "/api/trintuna")).GetProperty("upcoming").EnumerateArray()
            .First(g => Id(g) == Id(gathering)).GetProperty("myStatus").ValueKind.Should().Be(JsonValueKind.Null);

        var old = await CreateAsync(owner, null, "2020-01-04");
        var dated = await AreaHttp.Json(await owner.PostAsJsonAsync($"/api/trintuna/gatherings/{Id(old)}/settle", new { optionId = Option(old, "2020-01-04") }));
        dated.GetProperty("past").GetBoolean().Should().BeTrue();
        var historical = await SeedGatheringAsync(historical: true);
        foreach (var id in new[] { Id(old), historical })
        {
            var answered = await AreaHttp.Json(await member.PutAsJsonAsync($"/api/trintuna/gatherings/{id}/presence", new { willAttend = true }));
            answered.GetProperty("past").GetBoolean().Should().BeTrue();
            answered.GetProperty("myStatus").GetString().Should().Be("going");
            answered.GetProperty("poll").GetProperty("canVote").GetBoolean().Should().BeFalse("votes stay closed on a past gathering");
            (await member.DeleteAsync($"/api/trintuna/gatherings/{id}/presence")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await member.PutAsJsonAsync("/api/trintuna/gatherings/987654/presence", new { willAttend = true })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var board = await AreaHttp.Json(member, "/api/trintuna");
        board.GetProperty("past").EnumerateArray().First().GetProperty("id").GetInt32().Should().Be(Id(old), "dated past gatherings come before the undated historical ones");
        board.GetProperty("past").EnumerateArray().Should().Contain(g => Id(g) == historical);
    }
}

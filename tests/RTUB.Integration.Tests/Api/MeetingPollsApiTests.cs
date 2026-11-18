using System.Net;
using System.Text.Json;
using FluentAssertions;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;

public sealed class MeetingPollsApiTests : IClassFixture<MeetingsApiFactory>
{
    private readonly MeetingsApiFactory _factory;

    private readonly MeetingsKit _kit;

    public MeetingPollsApiTests(MeetingsApiFactory factory)
    {
        _factory = factory;
        _kit = new MeetingsKit(factory);
    }

    [Fact]
    public async Task Visitors_CannotReadCreateVoteCloseOrDeleteAPoll()
    {
        var president = await _kit.MemberAsync(Who.Veterano, Who.Holding(Position.PresidenteConselhoVeteranos));
        var poll = new { audience = "ConselhoVeteranos", question = "Os visitantes votam?", options = new[] { "Sim", "Não" }, closesAt = (string?)null };
        var id = await (await president.Client.PostAsJsonAsync("/api/meetings/polls", poll)).Content.ReadFromJsonAsync<int>();
        var option = SinglePoll(await AreaHttp.Json(president.Client, "/api/meetings/polls"), id).GetProperty("options")[0].GetProperty("id").GetInt32();
        var anonymous = AreaHttp.Anonymous(_factory, "10.66.9.1");
        await AreaHttp.WithTokenAsync(anonymous);

        HttpResponseMessage[] answers =
        [
            await anonymous.GetAsync("/api/meetings/polls"),
            await anonymous.PostAsJsonAsync("/api/meetings/polls", poll),
            await anonymous.PutAsJsonAsync($"/api/meetings/polls/{id}/vote", new { optionId = option }),
            await anonymous.PostAsync($"/api/meetings/polls/{id}/close", null),
            await anonymous.DeleteAsync($"/api/meetings/polls/{id}"),
        ];

        answers.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Unauthorized);
        var stored = SinglePoll(await AreaHttp.Json(president.Client, "/api/meetings/polls"), id);
        stored.GetProperty("options").EnumerateArray().Sum(o => o.GetProperty("votes").GetInt32()).Should().Be(0, "a visitor's vote was not counted");
    }

    [Fact]
    public async Task CvPoll_ApiHidesItFromTunos_AndOwnerCanManageButCannotVoteOutsideAudience()
    {
        var president = await _kit.MemberAsync(Who.Veterano, Who.Holding(Position.PresidenteConselhoVeteranos));
        var veteran = await _kit.MemberAsync(Who.Veterano);
        var magister = await _kit.MemberAsync(Who.Holding(Position.Magister));
        var tuno = await _kit.MemberAsync(Who.Tuno);
        var owner = await _kit.MemberAsync("Owner");

        var response = await president.Client.PostAsJsonAsync("/api/meetings/polls", new
        {
            audience = "ConselhoVeteranos",
            question = "Que data preferem?",
            options = new[] { "6 de dezembro", "13 de dezembro" },
            closesAt = (string?)null,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var id = await response.Content.ReadFromJsonAsync<int>();

        var veteranPoll = SinglePoll(await AreaHttp.Json(veteran.Client, "/api/meetings/polls"), id);
        var options = veteranPoll.GetProperty("options").EnumerateArray().ToArray();
        var firstOption = options[0].GetProperty("id").GetInt32();
        var secondOption = options[1].GetProperty("id").GetInt32();
        veteranPoll.GetProperty("canVote").GetBoolean().Should().BeTrue();
        (await AreaHttp.Json(magister.Client, "/api/meetings/polls")).GetProperty("polls").GetArrayLength().Should().Be(1);

        var hidden = await AreaHttp.Json(tuno.Client, "/api/meetings/polls");
        hidden.GetProperty("polls").GetArrayLength().Should().Be(0);
        var refusedVote = await tuno.Client.PutAsJsonAsync($"/api/meetings/polls/{id}/vote", new { optionId = firstOption });
        refusedVote.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownerPoll = SinglePoll(await AreaHttp.Json(owner.Client, "/api/meetings/polls"), id);
        ownerPoll.GetProperty("canManage").GetBoolean().Should().BeTrue();
        ownerPoll.GetProperty("canVote").GetBoolean().Should().BeFalse();
        var ownerVote = await owner.Client.PutAsJsonAsync($"/api/meetings/polls/{id}/vote", new { optionId = firstOption });
        ownerVote.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await AreaHttp.Json(await veteran.Client.PutAsJsonAsync($"/api/meetings/polls/{id}/vote", new { optionId = firstOption })))
            .GetBoolean().Should().BeTrue();
        (await veteran.Client.PutAsJsonAsync($"/api/meetings/polls/{id}/vote", new { optionId = secondOption }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = SinglePoll(await AreaHttp.Json(veteran.Client, "/api/meetings/polls"), id);
        updated.GetProperty("totalVotes").GetInt32().Should().Be(1);
        updated.GetProperty("myOptionId").GetInt32().Should().Be(secondOption);

        // Delete: the creator (or Owner) only. A voter is refused, a member outside the audience is told nothing.
        updated.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        (await veteran.Client.DeleteAsync($"/api/meetings/polls/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await magister.Client.DeleteAsync($"/api/meetings/polls/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tuno.Client.DeleteAsync($"/api/meetings/polls/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        SinglePoll(await AreaHttp.Json(president.Client, "/api/meetings/polls"), id).GetProperty("canDelete").GetBoolean().Should().BeTrue();
        (await president.Client.DeleteAsync($"/api/meetings/polls/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AreaHttp.Json(veteran.Client, "/api/meetings/polls")).GetProperty("polls").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task PollsPage_ServesTheReactPortalToMembers()
    {
        var tuno = await _kit.MemberAsync(Who.Tuno);

        (await tuno.Client.GetStringAsync("/meetings/polls")).Should().Contain("id=\"root\"");
    }

    private static JsonElement SinglePoll(JsonElement board, int id) =>
        board.GetProperty("polls").EnumerateArray()
            .Single(poll => poll.GetProperty("id").GetInt32() == id);
}

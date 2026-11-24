using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/trintuna task 160: on a past gathering the Owner, or its organizer while listed, marks who went (Fui) and
/// removes a Fui, by the page's row key. A mark is the person's own answer row, so "Quem foi" and the Hall of Fame count
/// it; the member can still change it. Each test clears the answers first (the class shares one database).
/// </summary>
public partial class TrintunaApiTests
{
    private static string Attendee(int gathering, string key) => $"/api/trintuna/gatherings/{gathering}/attendees/{key}";

    /// <summary>The page's key for a person: as TrintunaService.RowKey (SHA-256 hex, first 16), never the user id.</summary>
    private static string KeyOf(string userId) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(userId)))[..16];

    private static Task<HttpResponseMessage> MarkAsync(HttpClient who, int gathering, string userId) =>
        who.PutAsync(Attendee(gathering, KeyOf(userId)), null);

    private static Task<HttpResponseMessage> UnmarkAsync(HttpClient who, int gathering, string userId) =>
        who.DeleteAsync(Attendee(gathering, KeyOf(userId)));

    private static async Task<string[]> AnsweredAsync(HttpClient client, int gathering, string list) =>
        (await AreaHttp.Json(client, "/api/trintuna")).GetProperty("past").EnumerateArray()
            .Single(g => g.GetProperty("id").GetInt32() == gathering).GetProperty(list).EnumerateArray()
            .Select(p => p.GetProperty("key").GetString()!).ToArray();

    private async Task<int> SeedPastAsync(string? organizerUserId)
    {
        var id = await SeedGatheringAsync(historical: true);
        using var scope = _factory.Services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        await db.Gatherings.Where(g => g.Id == id).ExecuteUpdateAsync(g => g.SetProperty(x => x.OrganizerUserId, organizerUserId));
        return id;
    }

    [Fact]
    public async Task TheOwner_MarksAListedMember_WhoIsThenInQuemFoi_AndCountedInTheHallOfFame()
    {
        await ClearAnswersAsync();
        var (owner, _) = await SignInAsync("Owner");
        var (member, me) = await SignInAsync();
        await SetListAsync(owner, me.Id);
        var past = await SeedPastAsync(null);

        var marked = await MarkAsync(owner, past, me.Id);
        marked.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await AreaHttp.Json(marked);
        dto.GetProperty("going").EnumerateArray().Select(p => p.GetProperty("key").GetString()).Should().Contain(KeyOf(me.Id));
        dto.GetProperty("goingCount").GetInt32().Should().Be(1);
        dto.GetRawText().Should().NotContain(me.Id, "no user id reaches the page");

        (await HallAsync(member)).Should().Contain((1, me.Nickname, 1));
        await ClearAnswersAsync();
    }

    [Fact]
    public async Task TheOrganizer_MarksAndUnmarks_AndTheHallOfFameGoesBack()
    {
        await ClearAnswersAsync();
        var (owner, _) = await SignInAsync("Owner");
        var (organizer, boss) = await SignInAsync();
        var (_, other) = await SignInAsync();
        await SetListAsync(owner, boss.Id, other.Id);
        var past = await SeedPastAsync(boss.Id);

        (await MarkAsync(organizer, past, other.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AnsweredAsync(organizer, past, "going")).Should().Contain(KeyOf(other.Id));
        (await HallAsync(organizer)).Should().Contain((1, other.Nickname, 1));

        (await UnmarkAsync(organizer, past, other.Id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AnsweredAsync(organizer, past, "going")).Should().BeEmpty();
        (await HallAsync(organizer)).Should().OnlyContain(r => r.Count == 0);

        // Nothing to remove is still 204 (as DELETE /presence).
        (await UnmarkAsync(organizer, past, other.Id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ClearAnswersAsync();
    }

    [Fact]
    public async Task AMark_OverwritesNaoFui_TheMemberCanChangeItBack_AndDeleteNeverTouchesNaoFui()
    {
        await ClearAnswersAsync();
        var (owner, _) = await SignInAsync("Owner");
        var (member, me) = await SignInAsync();
        await SetListAsync(owner, me.Id);
        var past = await SeedPastAsync(null);

        await AnswerAsync(member, past, false);
        (await UnmarkAsync(owner, past, me.Id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AnsweredAsync(owner, past, "notGoing")).Should().Equal(KeyOf(me.Id));

        (await MarkAsync(owner, past, me.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AnsweredAsync(owner, past, "going")).Should().Equal(KeyOf(me.Id));
        (await AnsweredAsync(owner, past, "notGoing")).Should().BeEmpty();

        await AnswerAsync(member, past, false);
        (await AnsweredAsync(member, past, "notGoing")).Should().Equal(KeyOf(me.Id));
        (await AnsweredAsync(member, past, "going")).Should().BeEmpty();
        await ClearAnswersAsync();
    }

    [Fact]
    public async Task OnlyTheOwnerOrTheListedOrganizer_MayMark()
    {
        var (owner, _) = await SignInAsync("Owner");
        var (member, me) = await SignInAsync();
        var (organizer, boss) = await SignInAsync();
        await SetListAsync(owner, me.Id, boss.Id);
        var past = await SeedPastAsync(boss.Id);

        (await MarkAsync(member, past, me.Id)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await UnmarkAsync(member, past, me.Id)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await SetListAsync(owner, me.Id);
        (await MarkAsync(organizer, past, me.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound, "an organizer off the list cannot see the page");
        (await UnmarkAsync(organizer, past, me.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var anonymous = Anonymous();
        await AreaHttp.WithTokenAsync(anonymous);
        (await MarkAsync(anonymous, past, me.Id)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await UnmarkAsync(anonymous, past, me.Id)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownGathering_404_AndAnUpcomingOne_IsClosed()
    {
        var (owner, boss) = await SignInAsync("Owner");
        var upcoming = Id(await CreateAsync(owner));

        (await MarkAsync(owner, 999_999, boss.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UnmarkAsync(owner, 999_999, boss.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var closed = await MarkAsync(owner, upcoming, boss.Id);
        closed.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await closed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString().Should().Be("trintuna:closed");
        (await UnmarkAsync(owner, upcoming, boss.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OnlyPeopleWhoCanSeeThePage_CanBeMarked()
    {
        await ClearAnswersAsync();
        var (owner, _) = await SignInAsync("Owner");
        var (_, listed) = await SignInAsync();
        var (_, unlisted) = await SignInAsync();
        var expelled = await AddUserAsync();
        await SetListAsync(owner, listed.Id, expelled.Id);
        await ExpelAsync(expelled.Id);
        var past = await SeedPastAsync(null);

        foreach (var key in new[] { KeyOf(unlisted.Id), KeyOf(expelled.Id), "0123456789ABCDEF", unlisted.Id })
        {
            var refused = await owner.PutAsync(Attendee(past, key), null);
            (await AreaHttp.Errors(refused)).Should().ContainKey("key", key);
        }

        (await AnsweredAsync(owner, past, "going")).Should().BeEmpty();
    }

    [Fact]
    public async Task Marking_WithoutTheAntiforgeryHeader_Is400()
    {
        var (owner, _) = await SignInAsync("Owner");
        var (bare, _) = await SignInAsync("Owner", token: false);
        var (_, me) = await SignInAsync();
        await SetListAsync(owner, me.Id);
        var past = await SeedPastAsync(null);

        (await MarkAsync(bare, past, me.Id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await UnmarkAsync(bare, past, me.Id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

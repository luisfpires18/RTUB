using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// /api/trintuna numbers and organizers (#368): the server numbers a new gathering (highest + 1) and nobody edits it;
/// GET /organizers (Owner only) lists the Owner(s) and the listed members, and the server accepts the same set.
/// </summary>
public partial class TrintunaApiTests
{
    private static int Number(JsonElement gathering) => gathering.GetProperty("number").GetInt32();

    private async Task<int> HighestNumberAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        return await db.Gatherings.MaxAsync(g => (int?)g.Number) ?? 0;
    }

    [Fact]
    public async Task ANewGathering_GetsTheHighestNumberPlusOne_AndASentNumberIsIgnored()
    {
        var (owner, _) = await SignInAsync("Owner");
        var (organizer, who) = await SignInAsync();
        await SetListAsync(owner, who.Id);

        var highest = await HighestNumberAsync();
        var created = await AreaHttp.Json(await owner.PostAsJsonAsync("/api/trintuna/gatherings",
            new { number = 9999, place = "Chaves", organizerUserId = who.Id, pollDates = Array.Empty<string>() }));
        Number(created).Should().Be(highest + 1);
        created.GetProperty("title").GetString().Should().Be($"{highest + 1}º Almoço");
        Number(await CreateAsync(owner)).Should().Be(highest + 2);

        var path = $"/api/trintuna/gatherings/{Id(created)}";
        Number(await AreaHttp.Json(await owner.PutAsJsonAsync(path, new { number = 1, place = "Chaves", organizerUserId = who.Id }))).Should().Be(highest + 1);
        Number(await AreaHttp.Json(await organizer.PutAsJsonAsync(path, new { number = 2, place = "Chaves" }))).Should().Be(highest + 1);
    }

    [Fact]
    public async Task ParallelCreates_GetDistinctConsecutiveNumbers()
    {
        var (owner, _) = await SignInAsync("Owner");
        var highest = await HighestNumberAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => owner.PostAsJsonAsync("/api/trintuna/gatherings", Input())));
        var numbers = new List<int>();
        foreach (var response in responses)
        {
            numbers.Add(Number(await AreaHttp.Json(response)));
        }

        numbers.Should().BeEquivalentTo(Enumerable.Range(highest + 1, 5));
    }

    [Fact]
    public async Task Organizers_AreTheOwnersAndTheListedMembers_AndOnlyTheyAreAccepted()
    {
        var (owner, boss) = await SignInAsync("Owner");
        var (member, listed) = await SignInAsync();
        var (_, outsider) = await SignInAsync();
        var expelled = await AddUserAsync();
        await SetListAsync(owner, listed.Id, expelled.Id);
        await ExpelAsync(expelled.Id);

        var organizers = (await AreaHttp.Json(owner, "/api/trintuna/organizers")).EnumerateArray()
            .Select(m => m.GetProperty("id").GetString()).ToList();
        organizers.Should().Contain(new[] { boss.Id, listed.Id }).And.NotContain(new[] { outsider.Id, expelled.Id }).And.OnlyHaveUniqueItems();
        var names = (await AreaHttp.Json(owner, "/api/trintuna/organizers")).EnumerateArray().Select(m => m.GetProperty("displayName").GetString()).ToList();
        names.Should().BeInAscendingOrder(ByName);

        // Task 161: the full name travels too, so the organizer search finds first and last names (the dialogs search in the browser).
        var listedRow = (await AreaHttp.Json(owner, "/api/trintuna/organizers")).EnumerateArray().Single(m => m.GetProperty("id").GetString() == listed.Id);
        listedRow.GetProperty("fullName").GetString().Should().Be($"{listed.FirstName} {listed.LastName}".Trim());
        (await AreaHttp.Json(member, "/api/trintuna")).GetProperty("hallOfFame").EnumerateArray()
            .Single(r => r.GetProperty("displayName").GetString() == listed.Nickname)
            .GetProperty("fullName").GetString().Should().Be($"{listed.FirstName} {listed.LastName}".Trim());

        (await AreaHttp.Json(await owner.PostAsJsonAsync("/api/trintuna/gatherings", Input(boss.Id))))
            .GetProperty("organizerUserId").GetString().Should().Be(boss.Id, "the Owner can organize");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/trintuna/gatherings", Input(outsider.Id)))).Should().ContainKey("organizerUserId");
        (await AreaHttp.Errors(await owner.PostAsJsonAsync("/api/trintuna/gatherings", Input(expelled.Id)))).Should().ContainKey("organizerUserId");

        (await member.GetAsync("/api/trintuna/organizers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var (unlisted, _) = await SignInAsync();
        (await unlisted.GetAsync("/api/trintuna/organizers")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Anonymous().GetAsync("/api/trintuna/organizers")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Task 070 (issue #250): the list and calendar views read the same board response as the Kanban and write only through
/// the card endpoints, so this pins what they rely on: one source of authorised cards (archived ones apart), and every
/// change a view can lead to - from the page or straight at the API - refused to a member who only reads the card.
/// </summary>
public partial class LogisticsApiTests
{
    [Fact]
    public async Task Views_ShareOneAuthorisedSource_AndEveryChangeKeepsTheCardRules()
    {
        var (boardId, listId, _) = await SeedBoardAsync(cards: 3);
        var cards = await CardIdsAsync(listId);
        var (worker, workerUser) = await SignInAsync();
        var (viewer, _) = await SignInAsync();
        var (outsider, _) = await SignInAsync();
        foreach (var c in new[] { worker, viewer, outsider })
        {
            await WithTokenAsync(c);
        }

        await UpdateCardAsync(cards[0], c => c.AssignToUser(workerUser.Id));
        await UpdateCardAsync(cards[2], c => c.ArchivedAt = DateTime.UtcNow);

        // One source: the active cards in the lists, the archived one apart (no view counts it).
        var board = await Json(viewer, $"/api/logistics/boards/{boardId}");
        board.GetProperty("lists").EnumerateArray().SelectMany(l => l.GetProperty("cards").EnumerateArray()).Select(c => c.GetProperty("id").GetInt32())
            .Should().BeEquivalentTo(new[] { cards[0], cards[1] });
        board.GetProperty("archivedCards").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).Should().Equal(cards[2]);

        // Every write a view leads to, sent straight to the API by a member who only reads the card: refused.
        var refused = new[]
        {
            await viewer.PostAsJsonAsync($"/api/logistics/cards/{cards[0]}/status", new { status = "Done" }),
            await viewer.PostAsJsonAsync($"/api/logistics/cards/{cards[0]}/move", new { listId, position = 1 }),
            await viewer.PostAsJsonAsync($"/api/logistics/cards/{cards[0]}/comments", new { body = "x" }),
            await viewer.PostAsJsonAsync($"/api/logistics/cards/{cards[0]}/checklist", new { task = "x" }),
            await viewer.PostAsJsonAsync($"/api/logistics/cards/{cards[0]}/reminders", Remind()),
            await worker.PostAsJsonAsync($"/api/logistics/cards/{cards[1]}/status", new { status = "Done" }),
            await worker.PostAsJsonAsync($"/api/logistics/cards/{cards[0]}/archive", new { archived = true }),
        };
        refused.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.Forbidden);

        // The worker changes their own card from any view; an archived card is read-only.
        (await worker.PostAsJsonAsync($"/api/logistics/cards/{cards[0]}/comments", new { body = "feito" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await worker.PostAsJsonAsync($"/api/logistics/cards/{cards[2]}/comments", new { body = "x" })).StatusCode.Should().NotBe(HttpStatusCode.OK);

        // A private board: nothing for a member off its list, by any path the views use.
        await UpdateBoardAsync(boardId, b => b.Visibility = BoardVisibility.Private);
        (await outsider.GetAsync($"/api/logistics/boards/{boardId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.GetAsync($"/api/logistics/cards/{cards[0]}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.PostAsJsonAsync($"/api/logistics/cards/{cards[0]}/status", new { status = "Done" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

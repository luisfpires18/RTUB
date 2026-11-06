using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Logística on /api/logistics (React track 023, were the Blazor /logistics and /logistics/{id}) through the real host:
/// real login, roles, antiforgery and SQLite. Board files go to <see cref="FakeDocumentStorage"/> (in memory, nothing
/// reaches R2). Logistics sends no email or push; reminders are only stored. Every test makes its own boards, since the
/// class shares one database.
/// </summary>
public class LogisticsApiTests : IClassFixture<DocumentationApiFactory>
{
    private readonly DocumentationApiFactory _factory;

    public LogisticsApiTests(DocumentationApiFactory factory)
    {
        _factory = factory;
        _factory.Storage.Objects.Clear();
    }

    [Fact]
    public async Task Visitors_GetNothing_AndBothPagesAreReact()
    {
        var (boardId, listId, cardId) = await SeedBoardAsync();
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        foreach (var path in new[] { "/api/logistics", $"/api/logistics/boards/{boardId}", $"/api/logistics/cards/{cardId}", "/api/logistics/members?q=a" })
        {
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }

        (await anonymous.PostAsJsonAsync($"/api/logistics/lists/{listId}/cards", new { title = "x" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync($"/api/logistics/boards/{boardId}/reminders", new { cardId })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        foreach (var page in new[] { "/logistics", $"/logistics/{boardId}" })
        {
            var response = await anonymous.GetAsync(page);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect, page);
            response.Headers.Location!.ToString().Should().Be("/login?returnUrl=" + Uri.EscapeDataString(page));
        }

        var (member, _) = await SignInAsync();
        (await member.GetStringAsync("/logistics")).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");
        (await member.GetStringAsync($"/logistics/{boardId}")).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");

        var web = typeof(RTUB.Program).Assembly;
        web.GetType("RTUB.Pages.Management.Logistics").Should().BeNull("the Blazor boards page was retired");
        web.GetType("RTUB.Pages.Management.LogisticsBoard").Should().BeNull("the Blazor board page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/logistics", "/logistics/{BoardId:int}", "/documentation", "/shop", "/inventory", "/leaderboard",
                "/members/manage", "/member/events", "/member/gallery", "/member/roles", "/hierarchy" });
    }

    /// <summary>Task 065: Leitões read like any member (boards, the board, card details, files); they manage nothing.</summary>
    [Fact]
    public async Task Leitoes_ReadBoardsAndCards_ButManageNothing()
    {
        var (boardId, listId, cardId) = await SeedBoardAsync();
        var (leitao, me) = await SignInAsync();
        await UpdateUserAsync(me.Id, u => u.Categories = new List<MemberCategory> { MemberCategory.Leitao });
        await WithTokenAsync(leitao);

        var boards = await Json(leitao, "/api/logistics");
        boards.GetProperty("canManage").GetBoolean().Should().BeFalse();
        boards.GetProperty("active").EnumerateArray().Select(b => b.GetProperty("id").GetInt32()).Should().Contain(boardId);
        (await Json(leitao, $"/api/logistics/boards/{boardId}")).GetProperty("canManage").GetBoolean().Should().BeFalse();
        (await Json(leitao, $"/api/logistics/boards/{boardId}/file-list")).ValueKind.Should().Be(JsonValueKind.Array);
        var detail = await Json(leitao, $"/api/logistics/cards/{cardId}");
        (detail.GetProperty("canManage").GetBoolean(), detail.GetProperty("canWork").GetBoolean()).Should().Be((false, false));

        (await leitao.PostAsJsonAsync("/api/logistics/boards", new { name = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await leitao.PostAsJsonAsync($"/api/logistics/lists/{listId}/cards", new { title = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await leitao.DeleteAsync($"/api/logistics/cards/{cardId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await leitao.PostAsJsonAsync($"/api/logistics/cards/{cardId}/status", new { status = "Done" })).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "not assigned: read only");

        // The other Gestão modules keep their own rules: opening Logística to Leitões widens nothing else.
        (await leitao.GetAsync("/api/documentation")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await leitao.GetAsync("/api/requests")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Task 065: a member the card is assigned to ("Atribuir a" or "Membros atribuídos") ticks checklist items and changes
    /// the status - nothing else - and loses it when the assignment goes. Forged payloads are refused.
    /// </summary>
    [Fact]
    public async Task Assignees_TickTheChecklistAndChangeTheStatus_OnlyWhileAssigned()
    {
        var (boardId, listId, cardId) = await SeedBoardAsync(cards: 2);
        var other = (await CardIdsAsync(listId)).Single(id => id != cardId);
        var (_, _, foreign) = await SeedBoardAsync();
        var checklist = "[{\"Id\":0,\"Task\":\"Lavar\",\"Done\":false},{\"Id\":0,\"Task\":\"Contar\",\"Done\":false}]";
        foreach (var id in new[] { cardId, other, foreign })
        {
            await UpdateCardAsync(id, c => c.SetChecklist(checklist));
        }

        var (worker, me) = await SignInAsync();
        await UpdateUserAsync(me.Id, u => u.Categories = new List<MemberCategory> { MemberCategory.Leitao });
        await WithTokenAsync(worker);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.LogisticsCardAssignments.Add(LogisticsCardAssignment.Create(cardId, me.Id));
            await db.SaveChangesAsync();
        }

        Card(await Json(worker, $"/api/logistics/boards/{boardId}"), cardId).GetProperty("mine").GetBoolean().Should().BeTrue();
        Card(await Json(worker, $"/api/logistics/boards/{boardId}"), other).GetProperty("mine").GetBoolean().Should().BeFalse();
        var detail = await Json(worker, $"/api/logistics/cards/{cardId}");
        (detail.GetProperty("canManage").GetBoolean(), detail.GetProperty("canWork").GetBoolean()).Should().Be((false, true));

        var ticked = await Json(await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/1", new { task = "Contar", done = true }));
        ticked.GetProperty("checklist").EnumerateArray().Select(i => i.GetProperty("done").GetBoolean()).Should().Equal(false, true);
        (await CardAsync(cardId))!.ChecklistJson.Should().Be("[{\"Id\":0,\"Task\":\"Lavar\",\"Done\":false},{\"Id\":0,\"Task\":\"Contar\",\"Done\":true}]",
            "only Done changes; text, order and the stored shape stay");
        await Json(await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/1", new { task = "Contar", done = false }));
        (await CardAsync(cardId))!.ChecklistJson.Should().Be(checklist, "unticking persists too");
        (await Errors(await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/0", new { task = "Outra", done = true }))).Keys.Should().Equal("items");
        (await Errors(await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/9", new { task = "Lavar", done = true }))).Keys.Should().Equal("items");
        (await Json(await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/status", new { status = "InProgress" })))
            .GetProperty("card").GetProperty("status").GetString().Should().Be("InProgress");
        (await CardAsync(cardId))!.Status.Should().Be(CardStatus.InProgress);

        // Assigned is not manager: a forged owner, labels, links, assignments, moves and delete stay refused.
        foreach (var (method, path, body) in new (string, string, object?)[]
        {
            ("PUT", $"/api/logistics/cards/{cardId}", new { title = "x", assignedToUserId = me.Id, version = 0 }),
            ("PUT", $"/api/logistics/cards/{cardId}/labels", new { labels = Array.Empty<object>() }),
            ("PUT", $"/api/logistics/cards/{cardId}/links", new { cardIds = Array.Empty<int>() }),
            ("POST", $"/api/logistics/cards/{cardId}/assignments", new { userId = me.Id }),
            ("DELETE", $"/api/logistics/cards/{cardId}/assignments/{me.Id}", null),
            ("POST", $"/api/logistics/cards/{cardId}/move", new { listId, position = 0 }),
            ("DELETE", $"/api/logistics/cards/{cardId}", null),
            // Not this member's cards: the other card of the board and a card on another board.
            ("POST", $"/api/logistics/cards/{other}/checklist/0", new { task = "Lavar", done = true }),
            ("POST", $"/api/logistics/cards/{other}/status", new { status = "Done" }),
            ("POST", $"/api/logistics/cards/{foreign}/checklist/0", new { task = "Lavar", done = true }),
        })
        {
            (await worker.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = body is null ? null : JsonContent.Create(body) }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {path}");
        }

        (await CardAsync(other))!.ChecklistJson.Should().Be(checklist);
        (await CardAsync(foreign))!.ChecklistJson.Should().Be(checklist);

        // "Atribuir a" counts as assigned too.
        await UpdateCardAsync(other, c => c.AssignToUser(me.Id));
        (await worker.PostAsJsonAsync($"/api/logistics/cards/{other}/status", new { status = "Done" })).StatusCode.Should().Be(HttpStatusCode.OK);

        // A manager removes the assignment: the next write is refused, the card still reads.
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        (await mod.DeleteAsync($"/api/logistics/cards/{cardId}/assignments/{me.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/0", new { task = "Lavar", done = true })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/status", new { status = "Done" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Json(worker, $"/api/logistics/cards/{cardId}")).GetProperty("canWork").GetBoolean().Should().BeFalse();
        (await CardAsync(cardId))!.Status.Should().Be(CardStatus.InProgress);

        // Managers keep the tick too, and anonymous callers get nothing.
        (await mod.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/0", new { task = "Lavar", done = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        var anonymous = Anonymous();
        await WithTokenAsync(anonymous);
        (await anonymous.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/0", new { task = "Lavar", done = false })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Members_ReadTheBoard_WithoutPrivateFields_ButOnlyModAdminOwnerManage()
    {
        var (boardId, listId, cardId) = await SeedBoardAsync();
        var (_, assignee) = await SignInAsync();
        await UpdateCardAsync(cardId, c => c.AssignToUser(assignee.Id));
        var (member, me) = await SignInAsync();
        await WithTokenAsync(member);

        var boards = await Json(member, "/api/logistics");
        boards.GetProperty("canManage").GetBoolean().Should().BeFalse();
        boards.GetProperty("active").EnumerateArray().Select(b => b.GetProperty("id").GetInt32()).Should().Contain(boardId);

        var raw = await member.GetStringAsync($"/api/logistics/boards/{boardId}");
        raw.Should().NotContain(assignee.Id).And.NotContain(assignee.Email!).And.NotContain("docs/");
        var board = JsonDocument.Parse(raw).RootElement;
        board.GetProperty("canManage").GetBoolean().Should().BeFalse();
        var card = board.GetProperty("lists").EnumerateArray().First().GetProperty("cards").EnumerateArray().Single();
        card.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "title", "description", "status", "labels", "startDate", "dueDate",
            "eventName", "assignedTo", "assignments", "checklistDone", "checklistTotal", "links", "mine", "priority", "comments", "attachments");
        card.GetProperty("mine").GetBoolean().Should().BeFalse("the card is someone else's");
        card.GetProperty("assignedTo").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("displayName", "fullName", "avatarUrl");

        // 065: every member reads the details, without member ids, and is told it may not work on the card.
        var detailRaw = await member.GetStringAsync($"/api/logistics/cards/{cardId}");
        detailRaw.Should().NotContain(assignee.Id).And.NotContain(assignee.Email!);
        var detail = JsonDocument.Parse(detailRaw).RootElement;
        (detail.GetProperty("canManage").GetBoolean(), detail.GetProperty("canWork").GetBoolean()).Should().Be((false, false));
        detail.GetProperty("assignedTo").GetProperty("userId").ValueKind.Should().Be(JsonValueKind.Null);
        (await member.GetAsync("/api/logistics/events?q=a")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var refused = new (string Method, string Path, object? Body)[]
        {
            ("POST", "/api/logistics/boards", new { name = "x" }),
            ("PUT", $"/api/logistics/boards/{boardId}", new { name = "x" }),
            ("POST", $"/api/logistics/boards/{boardId}/state", new { completed = true }),
            ("DELETE", $"/api/logistics/boards/{boardId}", null),
            ("POST", $"/api/logistics/boards/{boardId}/lists", new { name = "x" }),
            ("PUT", $"/api/logistics/lists/{listId}", new { name = "x" }),
            ("POST", $"/api/logistics/lists/{listId}/move", new { position = 0 }),
            ("DELETE", $"/api/logistics/lists/{listId}", null),
            ("POST", $"/api/logistics/lists/{listId}/cards", new { title = "x" }),
            ("PUT", $"/api/logistics/cards/{cardId}", new { title = "x" }),
            ("POST", $"/api/logistics/cards/{cardId}/status", new { status = "Done" }),
            ("POST", $"/api/logistics/cards/{cardId}/move", new { listId, position = 0 }),
            ("PUT", $"/api/logistics/cards/{cardId}/labels", new { labels = Array.Empty<object>() }),
            ("POST", $"/api/logistics/cards/{cardId}/checklist", new { task = "x" }),
            ("PUT", $"/api/logistics/cards/{cardId}/checklist/0", new { expected = "x", task = "y" }),
            ("DELETE", $"/api/logistics/cards/{cardId}/checklist/0?task=x", null),
            ("POST", $"/api/logistics/cards/{cardId}/comments", new { body = "x" }),
            ("POST", $"/api/logistics/cards/{cardId}/links", new { url = "https://exemplo.pt" }),
            ("POST", $"/api/logistics/cards/{cardId}/checklist/0", new { task = "x", done = true }),
            ("PUT", $"/api/logistics/cards/{cardId}/links", new { cardIds = Array.Empty<int>() }),
            ("POST", $"/api/logistics/cards/{cardId}/assignments", new { userId = me.Id }),
            ("DELETE", $"/api/logistics/cards/{cardId}", null),
            ("DELETE", $"/api/logistics/boards/{boardId}/files?name=x.pdf", null),
        };
        foreach (var (method, path, body) in refused)
        {
            (await member.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = body is null ? null : JsonContent.Create(body) }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {path}");
        }

        (await member.PostAsync($"/api/logistics/boards/{boardId}/files", File("x.pdf"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CardAsync(cardId))!.Status.Should().Be(CardStatus.Todo, "nothing changed");

        foreach (var role in new[] { "Mod", "Admin", "Owner" })
        {
            var (manager, _) = await SignInAsync(role);
            (await Json(manager, $"/api/logistics/boards/{boardId}")).GetProperty("canManage").GetBoolean().Should().BeTrue(role);
            (await manager.GetAsync($"/api/logistics/cards/{cardId}")).StatusCode.Should().Be(HttpStatusCode.OK, $"{role} opens card details (Owner inherits Admin)");
        }
    }

    [Fact]
    public async Task Boards_AreCreatedEditedFinishedAndDeleted_WithTheOldValidation()
    {
        var eventId = await AddEventAsync("Arraial de teste");
        var (owner, _) = await SignInAsync("Owner");
        (await owner.PostAsJsonAsync("/api/logistics/boards", new { name = "x" })).StatusCode.Should().Be(HttpStatusCode.BadRequest, "every write needs the antiforgery token");
        await WithTokenAsync(owner);

        (await Errors(await owner.PostAsJsonAsync("/api/logistics/boards", new { name = " " }))).Keys.Should().Equal("name");
        (await Errors(await owner.PostAsJsonAsync("/api/logistics/boards", new { name = new string('n', 201) }))).Keys.Should().Equal("name");
        (await Errors(await owner.PostAsJsonAsync("/api/logistics/boards", new { name = "ok", description = new string('d', 2001) }))).Keys.Should().Equal("description");
        (await Errors(await owner.PostAsJsonAsync("/api/logistics/boards", new { name = "ok", eventId = 999999 }))).Keys.Should().Equal("eventId");

        var tag = Tag();
        var created = await owner.PostAsJsonAsync("/api/logistics/boards", new { name = $" {tag} Jantar ", description = "Mesa", eventId });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var board = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = board.GetProperty("id").GetInt32();
        board.GetProperty("name").GetString().Should().Be($"{tag} Jantar");
        board.GetProperty("event").GetProperty("name").GetString().Should().Be("Arraial de teste");
        (await Json(owner, $"/api/logistics/events?q=arraial%20de")).EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).Should().Contain(eventId);

        (await Json(await owner.PutAsJsonAsync($"/api/logistics/boards/{id}", new { name = $"{tag} Jantar 2", description = "", eventId = (int?)null })))
            .GetProperty("event").ValueKind.Should().Be(JsonValueKind.Null);
        (await Json(await owner.PostAsJsonAsync($"/api/logistics/boards/{id}/state", new { completed = true }))).GetProperty("isCompleted").GetBoolean().Should().BeTrue();
        var listed = await Json(owner, $"/api/logistics?q={tag}");
        listed.GetProperty("active").GetArrayLength().Should().Be(0);
        listed.GetProperty("completed").EnumerateArray().Single().GetProperty("completedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        (await Json(await owner.PostAsJsonAsync($"/api/logistics/boards/{id}/state", new { completed = false }))).GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.Null);

        var listId = (await Json(await owner.PostAsJsonAsync($"/api/logistics/boards/{id}/lists", new { name = "A" }))).GetProperty("id").GetInt32();
        var cardId = (await Json(await owner.PostAsJsonAsync($"/api/logistics/lists/{listId}/cards", new { title = "T" }))).GetProperty("card").GetProperty("id").GetInt32();
        (await owner.DeleteAsync($"/api/logistics/boards/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CardAsync(cardId)).Should().BeNull("a board takes its lists and cards with it, as before");
        (await owner.GetAsync($"/api/logistics/boards/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListsAndCards_KeepTheOldRules_AndTheJsonColumnsTheirOldShape()
    {
        var (boardId, _, _) = await SeedBoardAsync(cards: 0);
        var (_, other) = await SignInAsync();
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);

        (await Errors(await mod.PostAsJsonAsync($"/api/logistics/boards/{boardId}/lists", new { name = new string('l', 101) }))).Keys.Should().Equal("name");
        var b = (await Json(await mod.PostAsJsonAsync($"/api/logistics/boards/{boardId}/lists", new { name = " B " }))).GetProperty("id").GetInt32();
        (await Json(await mod.PutAsJsonAsync($"/api/logistics/lists/{b}", new { name = "Compras" }))).GetProperty("name").GetString().Should().Be("Compras");
        Names(await Json(mod, $"/api/logistics/boards/{boardId}")).Should().Equal(new[] { "Material", "Compras" }, "new lists go last");
        (await mod.PostAsJsonAsync($"/api/logistics/lists/{b}/move", new { position = 0 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Names(await Json(mod, $"/api/logistics/boards/{boardId}")).Should().Equal("Compras", "Material");

        (await Errors(await mod.PostAsJsonAsync($"/api/logistics/lists/{b}/cards", new { title = "" }))).Keys.Should().Equal("title");
        (await Errors(await mod.PostAsJsonAsync($"/api/logistics/lists/{b}/cards", new { title = "x", assignedToUserId = "nobody" }))).Keys.Should().Equal("assignedToUserId");
        var created = await Json(await mod.PostAsJsonAsync($"/api/logistics/lists/{b}/cards", new { title = " Pratos ", description = "40", assignedToUserId = other.Id }));
        var id = created.GetProperty("card").GetProperty("id").GetInt32();
        created.GetProperty("card").GetProperty("status").GetString().Should().Be("Todo");
        created.GetProperty("assignedTo").GetProperty("userId").GetString().Should().Be(other.Id, "managers need the id to change it");

        (await Errors(await mod.PutAsJsonAsync($"/api/logistics/cards/{id}", new { title = "Pratos", startDate = "04/10/2026" }))).Keys.Should().Equal("startDate");
        var updated = await Json(await mod.PutAsJsonAsync($"/api/logistics/cards/{id}", new { title = "Pratos fundos", description = "50", startDate = "2026-10-04", dueDate = "2026-10-10", version = 0 }));
        updated.GetProperty("card").GetProperty("dueDate").GetString().Should().StartWith("2026-10-10");
        updated.GetProperty("assignedTo").ValueKind.Should().Be(JsonValueKind.Null, "an empty assignee clears it, as the old form");

        (await Errors(await mod.PostAsJsonAsync($"/api/logistics/cards/{id}/status", new { status = "7" }))).Keys.Should().Equal("status");
        (await Json(await mod.PostAsJsonAsync($"/api/logistics/cards/{id}/status", new { status = "InProgress" }))).GetProperty("card").GetProperty("status").GetString().Should().Be("InProgress");

        (await Errors(await mod.PutAsJsonAsync($"/api/logistics/cards/{id}/labels", new { labels = new[] { new { text = "x", color = "red" } } }))).Keys.Should().Equal("labels");
        await Json(await mod.PutAsJsonAsync($"/api/logistics/cards/{id}/labels", new { labels = new[] { new { text = " Urgente ", color = "#FF0000" } } }));
        (await Errors(await mod.PostAsJsonAsync($"/api/logistics/cards/{id}/checklist", new { task = " " }))).Keys.Should().Equal("task");
        await Json(await mod.PostAsJsonAsync($"/api/logistics/cards/{id}/checklist", new { task = " Lavar " }));
        await Json(await mod.PostAsJsonAsync($"/api/logistics/cards/{id}/checklist", new { task = "Contar" }));
        await Json(await mod.PostAsJsonAsync($"/api/logistics/cards/{id}/checklist/0", new { task = "Lavar", done = true }));
        var stored = await CardAsync(id);
        stored!.Labels.Should().Be("[{\"Id\":0,\"Text\":\"Urgente\",\"Color\":\"#ff0000\"}]", "the old page's JSON shape");
        stored.ChecklistJson.Should().Be("[{\"Id\":0,\"Task\":\"Lavar\",\"Done\":true},{\"Id\":0,\"Task\":\"Contar\",\"Done\":false}]");

        var face = Card(await Json(mod, $"/api/logistics/boards/{boardId}"), id);
        (face.GetProperty("checklistDone").GetInt32(), face.GetProperty("checklistTotal").GetInt32()).Should().Be((1, 2));
        (await Json(mod, $"/api/logistics/boards/{boardId}")).GetProperty("labels").EnumerateArray().Select(l => l.GetString()).Should().Equal("Urgente");

        var sibling = (await Json(await mod.PostAsJsonAsync($"/api/logistics/lists/{b}/cards", new { title = "Copos" }))).GetProperty("card").GetProperty("id").GetInt32();
        var (_, _, foreign) = await SeedBoardAsync();
        (await Errors(await mod.PutAsJsonAsync($"/api/logistics/cards/{id}/links", new { cardIds = new[] { foreign } }))).Keys.Should().Equal("cardIds");
        (await Errors(await mod.PutAsJsonAsync($"/api/logistics/cards/{id}/links", new { cardIds = new[] { id } }))).Keys.Should().Equal("cardIds");
        (await Json(await mod.PutAsJsonAsync($"/api/logistics/cards/{id}/links", new { cardIds = new[] { sibling } }))).GetProperty("links").EnumerateArray().Single()
            .GetProperty("name").GetString().Should().Be("Copos");

        (await Json(await mod.PostAsJsonAsync($"/api/logistics/cards/{id}/assignments", new { userId = other.Id }))).GetProperty("assignments").GetArrayLength().Should().Be(1);
        (await Errors(await mod.PostAsJsonAsync($"/api/logistics/cards/{id}/assignments", new { userId = other.Id })))["userId"].Single()
            .Should().Be("Utilizador já está atribuído a este cartão");
        (await Json(await mod.DeleteAsync($"/api/logistics/cards/{id}/assignments/{other.Id}"))).GetProperty("assignments").GetArrayLength().Should().Be(0);

        (await mod.DeleteAsync($"/api/logistics/cards/{sibling}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await mod.DeleteAsync($"/api/logistics/lists/{b}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CardAsync(id)).Should().BeNull("a list takes its cards with it, as before");
    }

    [Fact]
    public async Task MovingACard_RenumbersBothLists_AndNeverLeavesTheBoard()
    {
        var (boardId, from, first) = await SeedBoardAsync(cards: 3);
        var to = await AddListAsync(boardId, "Feito", 1);
        var cards = await CardIdsAsync(from);
        // The old move wrote only the moved card's position, so real boards hold duplicates.
        await UpdateCardAsync(cards[2], c => c.UpdatePosition(0));
        var (_, _, foreign) = await SeedBoardAsync();
        var foreignList = (await CardAsync(foreign))!.ListId;
        var (admin, _) = await SignInAsync("Admin");
        await WithTokenAsync(admin);

        (await admin.PostAsJsonAsync($"/api/logistics/cards/{first}/move", new { listId = to, position = 5 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PositionsAsync(to)).Should().Equal((first, 0));
        (await PositionsAsync(from)).Select(p => p.Position).Should().Equal(0, 1);

        (await admin.PostAsJsonAsync($"/api/logistics/cards/{cards[2]}/move", new { listId = to, position = 0 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PositionsAsync(to)).Should().Equal((cards[2], 0), (first, 1));
        Card(await Json(admin, $"/api/logistics/boards/{boardId}"), cards[2]).GetProperty("id").GetInt32().Should().Be(cards[2]);

        (await Errors(await admin.PostAsJsonAsync($"/api/logistics/cards/{first}/move", new { listId = foreignList, position = 0 }))).Keys.Should().Equal("listId");
        (await CardAsync(first))!.ListId.Should().Be(to);
    }

    [Fact]
    public async Task Reminders_AreOpenToEveryMember_AndOnlyStored()
    {
        var (boardId, _, cardId) = await SeedBoardAsync();
        var (_, _, foreign) = await SeedBoardAsync();
        var (member, me) = await SignInAsync();
        await WithTokenAsync(member);

        var errors = await Errors(await member.PostAsJsonAsync($"/api/logistics/boards/{boardId}/reminders",
            new { cardId = foreign, frequency = "Hourly", nextReminderDate = (DateTime?)null, userIds = Array.Empty<string>() }));
        errors.Keys.Should().BeEquivalentTo("cardId", "frequency", "nextReminderDate", "userIds");
        (await Errors(await member.PostAsJsonAsync($"/api/logistics/boards/{boardId}/reminders", Reminder(cardId, "nobody")))).Keys.Should().Equal("userIds");

        var created = await Json(await member.PostAsJsonAsync($"/api/logistics/boards/{boardId}/reminders", Reminder(cardId, me.Id, "Weekly")));
        created.GetProperty("frequency").GetString().Should().Be("Weekly");
        using var scope = _factory.Services.CreateScope();
        var reminder = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCardReminders.AsNoTracking()
            .SingleAsync(r => r.Id == created.GetProperty("id").GetInt32());
        (reminder.CardId, reminder.TargetUserIds, reminder.IsActive, reminder.LastSentAt).Should().Be((cardId, me.Id, true, (DateTime?)null),
            "stored only: nothing in the app sends logistics reminders");
    }

    [Fact]
    public async Task BoardFiles_LiveInTheBoardFolder_MembersDownload_ManagersUploadAndDelete()
    {
        var (boardId, _, _) = await SeedBoardAsync(name: "Arraial: 2026/../x");
        var folder = $"docs/Test/{FiscalYearHelper.GetCurrentFiscalYearString()}/Logistics/Arraial 2026x/";
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);

        (await Errors(await mod.PostAsync($"/api/logistics/boards/{boardId}/files", File("x.exe", "application/x-msdownload")))).Keys.Should().Equal("file");
        (await Json(await mod.PostAsync($"/api/logistics/boards/{boardId}/files", File(@"..\..\orcamento.pdf")))).GetProperty("name").GetString().Should().Be("orcamento.pdf");
        _factory.Storage.Objects.Keys.Should().Contain(folder + "orcamento.pdf", "the old folder, the board name sanitised as before");

        var (member, _) = await SignInAsync();
        var board = await Json(member, $"/api/logistics/boards/{boardId}");
        board.GetProperty("files").EnumerateArray().Single().GetProperty("name").GetString().Should().Be("orcamento.pdf");

        // The board page does not wait for the storage: ?files=false asks the storage nothing, file-list is the one listing.
        var listings = _factory.Storage.ListCalls;
        var lean = await Json(member, $"/api/logistics/boards/{boardId}?files=false");
        lean.GetProperty("files").GetArrayLength().Should().Be(0);
        lean.GetProperty("lists").GetArrayLength().Should().Be(board.GetProperty("lists").GetArrayLength());
        _factory.Storage.ListCalls.Should().Be(listings, "the lean board makes no storage listing");
        (await Json(member, $"/api/logistics/boards/{boardId}/file-list")).EnumerateArray().Single().GetProperty("name").GetString().Should().Be("orcamento.pdf");
        _factory.Storage.ListCalls.Should().Be(listings + 1, "the file list is one listing");
        (await member.GetAsync("/api/logistics/boards/999999/file-list")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Json(member, $"/api/logistics/boards/{boardId}/files?name=orcamento.pdf")).GetProperty("url").GetString()
            .Should().Be($"https://r2.test/{folder}orcamento.pdf?download=True");
        (await member.GetAsync($"/api/logistics/boards/{boardId}/files?name=..%2Forcamento.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await mod.DeleteAsync($"/api/logistics/boards/{boardId}/files?name=nope.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await mod.DeleteAsync($"/api/logistics/boards/{boardId}/files?name=orcamento.pdf")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Storage.Objects.Keys.Should().NotContain(folder + "orcamento.pdf");
    }

    [Fact]
    public async Task OldRows_ReadAsBefore_AndTheHiddenFileBlockSurvivesAnEdit()
    {
        var (boardId, _, cardId) = await SeedBoardAsync();
        await UpdateCardAsync(cardId, c =>
        {
            c.SetLabels("Compras, Urgente");
            c.UpdateContent(c.Title, "Visível\n\n<!-- FILES: [] -->");
            c.SetChecklist("not json");
        });
        var (owner, _) = await SignInAsync("Owner");
        await WithTokenAsync(owner);

        var face = Card(await Json(owner, $"/api/logistics/boards/{boardId}"), cardId);
        face.GetProperty("labels").EnumerateArray().Select(l => l.GetProperty("text").GetString()).Should().Equal("Compras", "Urgente");
        face.GetProperty("description").GetString().Should().Be("Visível");
        face.GetProperty("checklistTotal").GetInt32().Should().Be(0);

        await Json(await owner.PutAsJsonAsync($"/api/logistics/cards/{cardId}", new { title = "Novo", description = "Outro", version = 0 }));
        (await CardAsync(cardId))!.Description.Should().Be("Outro\n\n<!-- FILES: [] -->");
    }

    /// <summary>Mod parity is Logistics-only (unit 030): /emails is refused to Mod (and, since task 035, to Admin: Owner only).</summary>
    [Fact]
    public async Task Mod_IsStillRefusedTheEmailsPage()
    {
        var (admin, _) = await SignInAsync("Admin");
        var (mod, _) = await SignInAsync("Mod");
        (await admin.GetAsync("/emails")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.GetAsync("/emails")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.GetAsync("/api/owner/emails")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- task 066: the card workspace ----------

    /// <summary>
    /// 066: legacy cards keep everything. "Atribuir a" reads as the owner, assignment rows as collaborators (the owner's
    /// own row counted once, as owner); priority Normal, version 0; no data rewritten.
    /// </summary>
    [Fact]
    public async Task LegacyCards_KeepTheirPeopleAndData_OwnerFromAtribuirA_CollaboratorsFromTheRows()
    {
        var (boardId, _, cardId) = await SeedBoardAsync();
        var (_, owner) = await SignInAsync();
        var (_, helper) = await SignInAsync();
        await UpdateCardAsync(cardId, c =>
        {
            c.AssignToUser(owner.Id);
            c.UpdateContent(c.Title, "Antigo");
            c.SetChecklist("[{\"Id\":0,\"Task\":\"Lavar\",\"Done\":true}]");
            c.SetDates(new DateTime(2026, 10, 1), new DateTime(2026, 10, 9), null);
        });
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.LogisticsCardAssignments.AddRange(LogisticsCardAssignment.Create(cardId, owner.Id), LogisticsCardAssignment.Create(cardId, helper.Id));
            await db.SaveChangesAsync();
        }

        var (mod, _) = await SignInAsync("Mod");
        var detail = await Json(mod, $"/api/logistics/cards/{cardId}");
        detail.GetProperty("assignedTo").GetProperty("userId").GetString().Should().Be(owner.Id);
        detail.GetProperty("assignments").EnumerateArray().Select(a => a.GetProperty("userId").GetString()).Should().Equal(helper.Id);
        (detail.GetProperty("version").GetInt32(), detail.GetProperty("card").GetProperty("priority").GetString()).Should().Be((0, "Normal"));
        detail.GetProperty("card").GetProperty("description").GetString().Should().Be("Antigo");
        detail.GetProperty("checklist").GetArrayLength().Should().Be(1);
        detail.GetProperty("card").GetProperty("dueDate").GetString().Should().StartWith("2026-10-09");
        Card(await Json(mod, $"/api/logistics/boards/{boardId}"), cardId).GetProperty("assignments").GetInt32().Should().Be(1);
        (await AssignmentCountAsync(cardId)).Should().Be(2, "reading never rewrites the rows");
    }

    /// <summary>066 acceptance: an assigned Leitão edits the task, the checklist, comments with a mention, attaches a file and a link; a reload shows it all.</summary>
    [Fact]
    public async Task AssignedLeitao_CompletesTheWholeWorkflow_AndReloadShowsIt()
    {
        var (boardId, _, cardId) = await SeedBoardAsync();
        var (leitao, me) = await SignInAsync();
        var (_, mate) = await SignInAsync();
        var (_, gone) = await SignInAsync();
        await UpdateUserAsync(me.Id, u => u.Categories = new List<MemberCategory> { MemberCategory.Leitao });
        await UpdateUserAsync(gone.Id, u => u.IsExpelled = true);
        await UpdateCardAsync(cardId, c => c.AssignToUser(me.Id));
        await WithTokenAsync(leitao);

        var edit = new { title = "Credenciais", description = "Imprimir\nPlastificar", priority = "Urgent", startDate = "2026-11-10", dueDate = "2026-11-20", assignedToUserId = me.Id, version = 0 };
        var saved = await Json(await leitao.PutAsJsonAsync($"/api/logistics/cards/{cardId}", edit));
        (saved.GetProperty("version").GetInt32(), saved.GetProperty("card").GetProperty("priority").GetString()).Should().Be((1, "Urgent"));
        (await leitao.PutAsJsonAsync($"/api/logistics/cards/{cardId}", edit with { title = "Outra" })).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "the same version again is stale: another save happened");
        (await CardAsync(cardId))!.Title.Should().Be("Credenciais", "a stale save never lands");
        (await Errors(await leitao.PutAsJsonAsync($"/api/logistics/cards/{cardId}", edit with { dueDate = "2026-11-01", version = 1 }))).Keys.Should().Equal("dueDate");
        (await Errors(await leitao.PutAsJsonAsync($"/api/logistics/cards/{cardId}", edit with { priority = "9", version = 1 }))).Keys.Should().Equal("priority");
        var kept = await Json(await leitao.PutAsJsonAsync($"/api/logistics/cards/{cardId}", new { edit.title, edit.description, edit.priority, edit.startDate, edit.dueDate, version = 1 }));
        kept.GetProperty("assignedTo").GetProperty("displayName").GetString().Should().NotBeNullOrEmpty("a non-manager sends no owner: it is kept, not cleared");
        (await CardAsync(cardId))!.AssignedToUserId.Should().Be(me.Id);

        await Json(await leitao.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist", new { task = "Imprimir" }));
        await Json(await leitao.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist", new { task = "Plastificar" }));
        await Json(await leitao.PutAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/1", new { expected = "Plastificar", task = "Plastificar e furar" }));
        await Json(await leitao.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/0", new { task = "Imprimir", done = true }));
        (await Errors(await leitao.PutAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/1", new { expected = "Plastificar", task = "x" }))).Keys
            .Should().Equal(new[] { "items" }, "the item changed meanwhile: refused, not applied to whatever is there now");

        (await Errors(await leitao.PostAsJsonAsync($"/api/logistics/cards/{cardId}/comments", new { body = "Feito", mentionUserIds = new[] { gone.Id } })))
            .Keys.Should().Equal("mentionUserIds");
        (await Errors(await leitao.PostAsJsonAsync($"/api/logistics/cards/{cardId}/comments", new { body = new string('x', 2001) }))).Keys.Should().Equal("body");
        await Json(await leitao.PostAsJsonAsync($"/api/logistics/cards/{cardId}/comments", new { body = "<b>Feito</b> @colega", mentionUserIds = new[] { mate.Id } }));

        (await Errors(await leitao.PostAsync($"/api/logistics/cards/{cardId}/attachments", File("x.exe", "application/x-msdownload")))).Keys.Should().Equal("file");
        await Json(await leitao.PostAsync($"/api/logistics/cards/{cardId}/attachments", File(@"..\..\orcamento.pdf")));
        foreach (var bad in new[] { "javascript:alert(1)", "ftp://exemplo.pt/x", "https://exem plo.pt", "/relativo", "" })
        {
            (await Errors(await leitao.PostAsJsonAsync($"/api/logistics/cards/{cardId}/links", new { url = bad }))).Keys.Should().Equal(new[] { "url" }, bad);
        }

        await Json(await leitao.PostAsJsonAsync($"/api/logistics/cards/{cardId}/links", new { url = "https://docs.exemplo.pt/folha?id=1", name = "" }));

        var reloaded = await Json(leitao, $"/api/logistics/cards/{cardId}");
        reloaded.GetProperty("card").GetProperty("title").GetString().Should().Be("Credenciais");
        reloaded.GetProperty("card").GetProperty("description").GetString().Should().Be("Imprimir\nPlastificar");
        reloaded.GetProperty("checklist").EnumerateArray().Select(i => (i.GetProperty("task").GetString(), i.GetProperty("done").GetBoolean()))
            .Should().Equal(("Imprimir", true), ("Plastificar e furar", false));
        var comment = reloaded.GetProperty("comments").EnumerateArray().Single();
        comment.GetProperty("body").GetString().Should().Be("<b>Feito</b> @colega", "stored and returned as plain text; the page never renders it as HTML");
        comment.GetProperty("mentions").GetArrayLength().Should().Be(1);
        (comment.GetProperty("canEdit").GetBoolean(), comment.GetProperty("canDelete").GetBoolean()).Should().Be((true, true));
        var attachments = reloaded.GetProperty("attachments").EnumerateArray().ToList();
        attachments.Select(a => (a.GetProperty("kind").GetString(), a.GetProperty("name").GetString())).Should().Equal(("File", "orcamento.pdf"), ("Link", "docs.exemplo.pt"));
        var fileId = attachments[0].GetProperty("id").GetInt32();
        var key = _factory.Storage.Objects.Keys.Single(k => k.EndsWith("/orcamento.pdf") && k.StartsWith($"logistics/Test/cards/{cardId}/"));
        key.Should().NotContain("..").And.NotStartWith("docs/", "card files are not in the fiscal-year Documentação tree");
        (await Json(leitao, $"/api/logistics/cards/{cardId}/attachments/{fileId}")).GetProperty("url").GetString().Should().Contain(key);
        (await Json(leitao, $"/api/logistics/cards/{cardId}/attachments/{attachments[1].GetProperty("id").GetInt32()}")).GetProperty("url").GetString()
            .Should().Be("https://docs.exemplo.pt/folha?id=1");

        reloaded.GetProperty("activity").EnumerateArray().Select(a => a.GetProperty("detail").GetString()).Should().Contain(new[]
        {
            "Título: «Cartão 0» → «Credenciais»", "Prioridade: Normal → Urgente", "Prazo: 20/11/2026", "Tarefa concluída: Imprimir",
            "Tarefa editada: «Plastificar» → «Plastificar e furar»", "Ficheiro anexado: orcamento.pdf", "Ligação adicionada: docs.exemplo.pt",
        });
        reloaded.GetProperty("activity").EnumerateArray().First().GetProperty("actor").GetProperty("displayName").GetString().Should().NotBeNullOrEmpty();
        var face = Card(await Json(leitao, $"/api/logistics/boards/{boardId}"), cardId);
        (face.GetProperty("comments").GetInt32(), face.GetProperty("attachments").GetInt32()).Should().Be((1, 2));
    }

    /// <summary>066: unassigned viewers read only; comments and attachments follow their ownership rules; ids cannot be swapped across cards.</summary>
    [Fact]
    public async Task CommentsAndAttachments_FollowOwnership_AndIdsCannotCrossCards()
    {
        var (_, listId, cardId) = await SeedBoardAsync(cards: 2);
        var other = (await CardIdsAsync(listId)).Single(id => id != cardId);
        var (owner, ownerUser) = await SignInAsync();
        var (helper, helperUser) = await SignInAsync();
        var (viewer, _) = await SignInAsync();
        var (mod, _) = await SignInAsync("Mod");
        await UpdateCardAsync(cardId, c => c.AssignToUser(ownerUser.Id));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.LogisticsCardAssignments.Add(LogisticsCardAssignment.Create(cardId, helperUser.Id));
            await db.SaveChangesAsync();
        }

        foreach (var c in new[] { owner, helper, viewer, mod })
        {
            await WithTokenAsync(c);
        }

        var ownerComment = (await Json(await owner.PostAsJsonAsync($"/api/logistics/cards/{cardId}/comments", new { body = "Do responsável" })))
            .GetProperty("comments").EnumerateArray().Last().GetProperty("id").GetInt32();
        var helperComment = (await Json(await helper.PostAsJsonAsync($"/api/logistics/cards/{cardId}/comments", new { body = "Do colaborador" })))
            .GetProperty("comments").EnumerateArray().Last().GetProperty("id").GetInt32();
        var file = (await Json(await owner.PostAsync($"/api/logistics/cards/{cardId}/attachments", File("plano.pdf"))))
            .GetProperty("attachments").EnumerateArray().Last().GetProperty("id").GetInt32();

        // An unassigned viewer reads the discussion and downloads, but writes nothing.
        var read = await Json(viewer, $"/api/logistics/cards/{cardId}");
        read.GetProperty("comments").GetArrayLength().Should().Be(2);
        read.GetProperty("comments").EnumerateArray().Should().OnlyContain(c => !c.GetProperty("canEdit").GetBoolean() && !c.GetProperty("canDelete").GetBoolean());
        (await viewer.GetAsync($"/api/logistics/cards/{cardId}/attachments/{file}")).StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var (method, path, body) in new (string, string, object?)[]
        {
            ("PUT", $"/api/logistics/cards/{cardId}", new { title = "x", assignedToUserId = ownerUser.Id, version = 0 }),
            ("POST", $"/api/logistics/cards/{cardId}/comments", new { body = "x" }),
            ("PUT", $"/api/logistics/comments/{ownerComment}", new { body = "x" }),
            ("DELETE", $"/api/logistics/comments/{ownerComment}", null),
            ("POST", $"/api/logistics/cards/{cardId}/links", new { url = "https://exemplo.pt" }),
            ("DELETE", $"/api/logistics/cards/{cardId}/attachments/{file}", null),
            // a collaborator may not touch the owner's comment or file
        })
        {
            (await viewer.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = body is null ? null : JsonContent.Create(body) }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {path}");
        }

        (await viewer.PostAsync($"/api/logistics/cards/{cardId}/attachments", File("x.pdf"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _factory.Storage.Objects.Keys.Count(k => k.StartsWith($"logistics/Test/cards/{cardId}/") && k.EndsWith(".pdf")).Should().Be(1, "a refused upload stores nothing");

        (await helper.PutAsJsonAsync($"/api/logistics/comments/{ownerComment}", new { body = "mudado" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await helper.DeleteAsync($"/api/logistics/comments/{ownerComment}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await helper.DeleteAsync($"/api/logistics/cards/{cardId}/attachments/{file}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.PutAsJsonAsync($"/api/logistics/comments/{helperComment}", new { body = "mudado" })).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "moderation deletes; nobody rewrites another author's words");

        // PR #266 review: an uploader who is no longer owner or collaborator only reads; the attachment stays.
        var helperLink = (await Json(await helper.PostAsJsonAsync($"/api/logistics/cards/{cardId}/links", new { url = "https://exemplo.pt/plano" })))
            .GetProperty("attachments").EnumerateArray().Last().GetProperty("id").GetInt32();
        (await mod.DeleteAsync($"/api/logistics/cards/{cardId}/assignments/{helperUser.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await helper.DeleteAsync($"/api/logistics/cards/{cardId}/attachments/{helperLink}")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a former collaborator cannot delete what they uploaded");
        (await Json(helper, $"/api/logistics/cards/{cardId}")).GetProperty("attachments").EnumerateArray()
            .Single(a => a.GetProperty("id").GetInt32() == helperLink).GetProperty("canDelete").GetBoolean().Should().BeFalse();
        (await mod.PostAsJsonAsync($"/api/logistics/cards/{cardId}/assignments", new { userId = helperUser.Id })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Identifiers from another card are refused.
        (await viewer.GetAsync($"/api/logistics/cards/{other}/attachments/{file}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await mod.DeleteAsync($"/api/logistics/cards/{other}/attachments/{file}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Author edits own; manager moderates (one history line); uploader deletes own file (and the stored object).
        (await Json(await helper.PutAsJsonAsync($"/api/logistics/comments/{helperComment}", new { body = "Corrigido" })))
            .GetProperty("comments").EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == helperComment).GetProperty("editedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        var moderated = await Json(await mod.DeleteAsync($"/api/logistics/comments/{ownerComment}"));
        moderated.GetProperty("comments").GetArrayLength().Should().Be(1);
        moderated.GetProperty("activity").EnumerateArray().First().GetProperty("action").GetString().Should().Be("moderation");
        (await helper.DeleteAsync($"/api/logistics/comments/{helperComment}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.DeleteAsync($"/api/logistics/cards/{cardId}/attachments/{file}")).StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Storage.Objects.Keys.Should().NotContain(k => k.StartsWith($"logistics/Test/cards/{cardId}/") && k.EndsWith(".pdf"));

        // Deleting the card takes its stored files too.
        await Json(await owner.PostAsync($"/api/logistics/cards/{cardId}/attachments", File("final.pdf")));
        (await mod.DeleteAsync($"/api/logistics/cards/{cardId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Storage.Objects.Keys.Should().NotContain(k => k.StartsWith($"logistics/Test/cards/{cardId}/") && k.EndsWith(".pdf"));
    }

    private async Task<int> AssignmentCountAsync(int cardId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCardAssignments.CountAsync(a => a.CardId == cardId);
    }

    // ---------- helpers ----------

    private static string Tag() => "lgx" + Guid.NewGuid().ToString("N")[..8];

    private static object Reminder(int cardId, string userId, string frequency = "OneTime") =>
        new { cardId, frequency, nextReminderDate = DateTime.Today.AddDays(1).AddHours(10), userIds = new[] { userId } };

    private static IEnumerable<string?> Names(JsonElement board) =>
        board.GetProperty("lists").EnumerateArray().Select(l => l.GetProperty("name").GetString());

    private static JsonElement Card(JsonElement board, int id) =>
        board.GetProperty("lists").EnumerateArray().SelectMany(l => l.GetProperty("cards").EnumerateArray()).Single(c => c.GetProperty("id").GetInt32() == id);

    private static MultipartFormDataContent File(string name, string type = "application/pdf")
    {
        var content = new ByteArrayContent(new byte[] { 1, 2, 3 });
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return new MultipartFormDataContent { { content, "file", name } };
    }

    private async Task<(int Board, int List, int Card)> SeedBoardAsync(int cards = 1, string? name = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var board = LogisticsBoard.Create(name ?? $"{Tag()} Quadro");
        db.LogisticsBoards.Add(board);
        await db.SaveChangesAsync();
        var list = LogisticsList.Create("Material", board.Id, 0);
        db.LogisticsLists.Add(list);
        await db.SaveChangesAsync();
        var first = 0;
        for (var i = 0; i < cards; i++)
        {
            var card = LogisticsCard.Create($"Cartão {i}", list.Id, i);
            db.LogisticsCards.Add(card);
            await db.SaveChangesAsync();
            if (i == 0)
            {
                first = card.Id;
            }
        }

        return (board.Id, list.Id, first);
    }

    private async Task<int> AddListAsync(int boardId, string name, int position)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var list = LogisticsList.Create(name, boardId, position);
        db.LogisticsLists.Add(list);
        await db.SaveChangesAsync();
        return list.Id;
    }

    private async Task<int> AddEventAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var e = Event.Create(name, DateTime.Today.AddDays(10), "Bragança", EventType.Atuacao, "");
        db.Events.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private async Task<List<int>> CardIdsAsync(int listId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCards.Where(c => c.ListId == listId)
            .OrderBy(c => c.Position).ThenBy(c => c.Id).Select(c => c.Id).ToListAsync();
    }

    private async Task<List<(int Id, int Position)>> PositionsAsync(int listId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCards.AsNoTracking().Where(c => c.ListId == listId)
            .OrderBy(c => c.Position).ThenBy(c => c.Id).Select(c => new { c.Id, c.Position }).ToListAsync()).Select(c => (c.Id, c.Position)).ToList();
    }

    private async Task<LogisticsCard?> CardAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCards.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id);
    }

    private async Task UpdateCardAsync(int id, Action<LogisticsCard> change)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        change(await db.LogisticsCards.SingleAsync(c => c.Id == id));
        await db.SaveChangesAsync();
    }

    private async Task UpdateUserAsync(string id, Action<ApplicationUser> change)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        change(await db.Users.SingleAsync(u => u.Id == id));
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
    }

    private static int _ip;

    private Task<(HttpClient Client, ApplicationUser User)> SignInAsync(string? role = null)
    {
        var n = Interlocked.Increment(ref _ip);
        return CookieTestSession.SignInAsync(_factory, $"lgx{Guid.NewGuid():N}"[..20], $"10.93.{n / 250}.{n % 250 + 1}", role);
    }

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add(RemoteIpTestStartupFilter.HeaderName, "10.94.0.1");
        return client;
    }

    private static async Task WithTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/public/antiforgery-token");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
}

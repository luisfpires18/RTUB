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
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Logística on /api/logistics (React track 023, were the Blazor /logistics and /logistics/{id}) through the real host:
/// real login, roles, antiforgery and SQLite. Board files go to <see cref="FakeDocumentStorage"/> (in memory, nothing
/// reaches R2). Pushes (069) go through the real push service: without VAPID keys only the inbox copy is kept. Reminder
/// delivery is driven by the tests (LogisticsApiTests.Reminders.cs). Every test makes its own boards, since the
/// class shares one database.
/// </summary>
public partial class LogisticsApiTests : IClassFixture<DocumentationApiFactory>
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
        (await anonymous.PostAsJsonAsync($"/api/logistics/cards/{cardId}/reminders", new { frequency = "OneTime" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/logistics/my-tasks")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 069: the query comes back after signing in (a card link), and /logistics/tasks is a member page too.
        foreach (var page in new[] { "/logistics", $"/logistics/{boardId}", $"/logistics/{boardId}?card={cardId}", "/logistics/tasks" })
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
        await AddListAsync(boardId, "Em curso", 1, CardStatus.InProgress);
        await AddListAsync(boardId, "Feito", 2, CardStatus.Done);
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
            ("DELETE", $"/api/logistics/cards/{cardId}", null),
            ("POST", $"/api/logistics/lists/{listId}/move", new { position = 0 }),
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
        (await Errors(await mod.PostAsJsonAsync($"/api/logistics/cards/{id}/status", new { status = "InProgress" }))).Keys.Should().Equal(new[] { "status" },
            "067: no WIP column on this board, so the status cannot disagree with a column");
        var wip = (await Json(await mod.PostAsJsonAsync($"/api/logistics/boards/{boardId}/lists", new { name = "Em curso", state = "InProgress" }))).GetProperty("id").GetInt32();
        (await Json(await mod.PostAsJsonAsync($"/api/logistics/cards/{id}/status", new { status = "InProgress" }))).GetProperty("card").GetProperty("status").GetString().Should().Be("InProgress");
        (await CardAsync(id))!.ListId.Should().Be(wip, "a status change is a move to the column with that state");

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
        (await Errors(await mod.DeleteAsync($"/api/logistics/lists/{wip}"))).Keys.Should().Equal(new[] { "moveTo" }, "067: a column never takes its cards with it");
        (await mod.DeleteAsync($"/api/logistics/lists/{wip}?moveTo={b}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var moved = (await CardAsync(id))!;
        (moved.ListId, moved.Status).Should().Be((b, CardStatus.Todo), "the cards go to the chosen column and take its state");
        (await mod.DeleteAsync($"/api/logistics/cards/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await mod.DeleteAsync($"/api/logistics/lists/{b}")).StatusCode.Should().Be(HttpStatusCode.NoContent, "an empty column goes at once");
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

    /// <summary>
    /// 067: new board files go to the board's own folder (by id, not in Documentação), so a rename or the 1 September
    /// rollover changes nothing; the pre-067 folder stays listed for every year. Files are addressed by id; ?name= still
    /// works. Members download, managers upload and delete.
    /// </summary>
    [Fact]
    public async Task BoardFiles_LiveInTheBoardsOwnFolder_AndOldYearsStayListed()
    {
        var (boardId, _, _) = await SeedBoardAsync(name: "Arraial: 2026/../x");
        var legacy = "Arraial 2026x";
        await UpdateBoardAsync(boardId, b => b.LegacyFolder = legacy);
        var current = FiscalYearHelper.GetCurrentFiscalYearString();
        var previous = $"{int.Parse(current[..4]) - 1}-{int.Parse(current[..4])}";
        _factory.Storage.Put($"docs/Test/{previous}/Logistics/{legacy}/antigo.pdf", 10);
        _factory.Storage.Put($"docs/Test/{current}/Logistics/{legacy}/recente.pdf", 20);
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);

        (await Errors(await mod.PostAsync($"/api/logistics/boards/{boardId}/files", File("x.exe", "application/x-msdownload")))).Keys.Should().Equal("file");
        var uploaded = await Json(await mod.PostAsync($"/api/logistics/boards/{boardId}/files", File(@"..\..\orcamento.pdf")));
        uploaded.GetProperty("name").GetString().Should().Be("orcamento.pdf");
        var key = $"logistics/Test/boards/{boardId}/files/orcamento.pdf";
        _factory.Storage.Objects.Keys.Should().Contain(key, "the board's own folder, by id, outside Documentação");

        // A rename (and any later fiscal year) keeps every file on the board.
        await UpdateBoardAsync(boardId, b => b.Name = "Outro nome");
        var (member, _) = await SignInAsync();
        var files = (await Json(member, $"/api/logistics/boards/{boardId}/file-list")).EnumerateArray()
            .Select(f => (f.GetProperty("id").GetString(), f.GetProperty("name").GetString(), f.GetProperty("year").GetString())).ToList();
        files.Should().Equal(("f:orcamento.pdf", "orcamento.pdf", null), ($"l:{current}/recente.pdf", "recente.pdf", current),
            ($"l:{previous}/antigo.pdf", "antigo.pdf", previous));
        (await member.GetStringAsync($"/api/logistics/boards/{boardId}?files=false")).Should().NotContain("docs/").And.NotContain("logistics/Test");

        (await Json(member, $"/api/logistics/boards/{boardId}/files?file={Uri.EscapeDataString($"l:{previous}/antigo.pdf")}")).GetProperty("url").GetString()
            .Should().Be($"https://r2.test/docs/Test/{previous}/Logistics/{legacy}/antigo.pdf?download=True");
        (await Json(member, $"/api/logistics/boards/{boardId}/files?name=orcamento.pdf")).GetProperty("url").GetString().Should().Contain(key, "?name= still works");
        (await member.GetAsync($"/api/logistics/boards/{boardId}/files?file=l:{previous}/..%2F..%2Fsegredo.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.GetAsync($"/api/logistics/boards/{boardId}/files?file=f:nada.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await WithTokenAsync(member);
        (await member.DeleteAsync($"/api/logistics/boards/{boardId}/files?file=f:orcamento.pdf")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await mod.DeleteAsync($"/api/logistics/boards/{boardId}/files?file=f:orcamento.pdf")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Storage.Objects.Keys.Should().NotContain(key);
        _factory.Storage.Objects.Keys.Should().Contain($"docs/Test/{previous}/Logistics/{legacy}/antigo.pdf", "nothing is moved or deleted incidentally");
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

    // ---------- task 067: board access, workflow columns, archive, copies and templates ----------

    /// <summary>
    /// 067: a private board and everything under it is invisible (404) to members not on its access list - by id, link,
    /// list, counts, member picker, files, card, comments and attachments - while an invited Leitão and the managers see
    /// it. Assignment never grants access; access removal applies to the next request; the responsible member gets no
    /// manager rights.
    /// </summary>
    [Fact]
    public async Task PrivateBoards_AreInvisibleToTheUninvited_AndAssignmentNeverGrantsAccess()
    {
        var (boardId, listId, cardId) = await SeedBoardAsync();
        var (outsider, outsiderUser) = await SignInAsync();
        var (leitao, leitaoUser) = await SignInAsync();
        await UpdateUserAsync(leitaoUser.Id, u => u.Categories = new List<MemberCategory> { MemberCategory.Leitao });
        var (mod, modUser) = await SignInAsync("Mod");
        foreach (var c in new[] { outsider, leitao, mod })
        {
            await WithTokenAsync(c);
        }

        await Json(await mod.PostAsync($"/api/logistics/boards/{boardId}/files", File("plano.pdf")));
        var link = (await Json(await mod.PostAsJsonAsync($"/api/logistics/cards/{cardId}/links", new { url = "https://exemplo.pt/x" })))
            .GetProperty("attachments").EnumerateArray().Single().GetProperty("id").GetInt32();
        var name = (await Json(mod, $"/api/logistics/boards/{boardId}")).GetProperty("name").GetString()!;

        // The responsible member must see the board: on a private board that means the access list.
        var form = new { name, description = "", eventId = (int?)null, ownerUserId = outsiderUser.Id, visibility = "Private", memberIds = new[] { leitaoUser.Id } };
        (await Errors(await mod.PutAsJsonAsync($"/api/logistics/boards/{boardId}", form))).Keys.Should().Equal("ownerUserId");
        var saved = await Json(await mod.PutAsJsonAsync($"/api/logistics/boards/{boardId}", form with { ownerUserId = leitaoUser.Id }));
        (saved.GetProperty("visibility").GetString(), saved.GetProperty("owner").GetProperty("userId").GetString()).Should().Be(("Private", leitaoUser.Id));

        // The outsider: nothing, by any path, and no 403 that would confirm the board exists.
        (await Json(outsider, "/api/logistics")).GetProperty("active").EnumerateArray().Select(b => b.GetProperty("id").GetInt32()).Should().NotContain(boardId);
        foreach (var path in new[]
        {
            $"/api/logistics/boards/{boardId}", $"/api/logistics/boards/{boardId}/file-list", $"/api/logistics/boards/{boardId}/files?file=f:plano.pdf",
            $"/api/logistics/cards/{cardId}", $"/api/logistics/cards/{cardId}/attachments/{link}", $"/api/logistics/members?q=a&boardId={boardId}",
        })
        {
            (await outsider.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound, path);
        }

        (await outsider.PostAsJsonAsync($"/api/logistics/cards/{cardId}/comments", new { body = "x" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.PostAsJsonAsync($"/api/logistics/cards/{cardId}/reminders", Remind())).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Assignment never grants access: an assigned outsider still sees nothing, and cannot be added as collaborator.
        (await Errors(await mod.PostAsJsonAsync($"/api/logistics/cards/{cardId}/assignments", new { userId = outsiderUser.Id }))).Keys.Should().Equal("userId");
        await UpdateCardAsync(cardId, c => c.AssignToUser(outsiderUser.Id));
        (await outsider.GetAsync($"/api/logistics/cards/{cardId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.PostAsJsonAsync($"/api/logistics/cards/{cardId}/status", new { status = "Todo" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await UpdateCardAsync(cardId, c => c.AssignToUser(null));

        // Mentions and the member picker stay inside the board.
        (await Errors(await mod.PostAsJsonAsync($"/api/logistics/cards/{cardId}/comments", new { body = "olá", mentionUserIds = new[] { outsiderUser.Id } })))
            .Keys.Should().Equal("mentionUserIds");
        var picked = (await Json(mod, $"/api/logistics/members?q={Uri.EscapeDataString(outsiderUser.Nickname!)}&boardId={boardId}")).EnumerateArray()
            .Select(m => m.GetProperty("id").GetString());
        picked.Should().NotContain(outsiderUser.Id);

        // The invited Leitão (also the responsible member) reads everything and manages nothing.
        (await Json(leitao, "/api/logistics")).GetProperty("active").EnumerateArray().Select(b => b.GetProperty("id").GetInt32()).Should().Contain(boardId);
        (await leitao.GetAsync($"/api/logistics/cards/{cardId}/attachments/{link}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await leitao.GetAsync($"/api/logistics/boards/{boardId}/files?file=f:plano.pdf")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await leitao.PostAsJsonAsync($"/api/logistics/boards/{boardId}/lists", new { name = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "being responsible grants no staff rights");
        (await leitao.PutAsJsonAsync($"/api/logistics/boards/{boardId}", form with { ownerUserId = leitaoUser.Id })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.GetAsync($"/api/logistics/cards/{cardId}")).StatusCode.Should().Be(HttpStatusCode.OK, "Mod, Admin and Owner see every board");

        // Access removed: the next request sees nothing (the stale card included), even with the assignment kept.
        await UpdateCardAsync(cardId, c => c.AssignToUser(leitaoUser.Id));
        await Json(await mod.PutAsJsonAsync($"/api/logistics/boards/{boardId}", form with { ownerUserId = (string)null!, memberIds = Array.Empty<string>() }));
        (await leitao.GetAsync($"/api/logistics/cards/{cardId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await leitao.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist", new { task = "x" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        _ = listId;
        _ = modUser;
    }

    /// <summary>067: the column decides the status - moves, status changes, remaps and deletions keep them in step; workers move only their own cards.</summary>
    [Fact]
    public async Task Columns_DecideTheStatus_AndWorkersMoveOnlyTheirOwnCards()
    {
        var (boardId, todo, mine) = await SeedBoardAsync(cards: 2);
        var other = (await CardIdsAsync(todo)).Single(id => id != mine);
        var wip = await AddListAsync(boardId, "Em curso", 1, CardStatus.InProgress);
        var blocked = await AddListAsync(boardId, "Bloqueado", 2, CardStatus.Blocked);
        var (worker, me) = await SignInAsync();
        await UpdateCardAsync(mine, c => c.AssignToUser(me.Id));
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(worker);
        await WithTokenAsync(mod);

        (await worker.PostAsJsonAsync($"/api/logistics/cards/{mine}/move", new { listId = wip, position = 0 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        ((await CardAsync(mine))!.ListId, (await CardAsync(mine))!.Status).Should().Be((wip, CardStatus.InProgress));
        (await worker.PostAsJsonAsync($"/api/logistics/cards/{other}/move", new { listId = wip, position = 0 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await worker.PostAsJsonAsync($"/api/logistics/lists/{wip}/move", new { position = 0 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Json(await worker.PostAsJsonAsync($"/api/logistics/cards/{mine}/status", new { status = "Blocked" }))).GetProperty("card").GetProperty("status").GetString()
            .Should().Be("Blocked");
        (await CardAsync(mine))!.ListId.Should().Be(blocked);
        (await Errors(await worker.PostAsJsonAsync($"/api/logistics/cards/{mine}/status", new { status = "Done" }))).Keys.Should().Equal("status");

        // Remapping a column changes its cards' status in the same save; the board shows each column's state.
        (await Json(await mod.PutAsJsonAsync($"/api/logistics/lists/{blocked}", new { name = "Feito", state = "Done" }))).GetProperty("state").GetString().Should().Be("Done");
        (await CardAsync(mine))!.Status.Should().Be(CardStatus.Done);
        (await Json(mod, $"/api/logistics/boards/{boardId}")).GetProperty("lists").EnumerateArray().Select(l => l.GetProperty("state").GetString())
            .Should().Equal("Todo", "InProgress", "Done");
        (await Errors(await mod.PostAsJsonAsync($"/api/logistics/boards/{boardId}/lists", new { name = "x", state = "7" }))).Keys.Should().Equal("state");
    }

    /// <summary>067: archived boards and cards are read-only and out of the active views; managers restore them with their content.</summary>
    [Fact]
    public async Task Archive_IsReadOnly_AndRestoreKeepsTheContent()
    {
        var (boardId, listId, cardId) = await SeedBoardAsync(cards: 2);
        var other = (await CardIdsAsync(listId)).Single(id => id != cardId);
        var (worker, me) = await SignInAsync();
        await UpdateCardAsync(cardId, c => c.AssignToUser(me.Id));
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(worker);
        await WithTokenAsync(mod);

        (await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/archive", new { archived = true })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var archived = await Json(await mod.PostAsJsonAsync($"/api/logistics/cards/{cardId}/archive", new { archived = true }));
        (archived.GetProperty("readOnly").GetBoolean(), archived.GetProperty("canWork").GetBoolean()).Should().Be((true, false));
        var board = await Json(worker, $"/api/logistics/boards/{boardId}");
        board.GetProperty("lists").EnumerateArray().Single().GetProperty("cards").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).Should().Equal(other);
        board.GetProperty("archivedCards").EnumerateArray().Single().GetProperty("id").GetInt32().Should().Be(cardId);
        (await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/comments", new { body = "x" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await Json(await mod.PostAsJsonAsync($"/api/logistics/cards/{cardId}/archive", new { archived = false }));
        (await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/comments", new { body = "voltou" })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Json(await mod.PostAsJsonAsync($"/api/logistics/boards/{boardId}/archive", new { archived = true }))).GetProperty("archivedAt").ValueKind
            .Should().NotBe(JsonValueKind.Null);
        var lists = await Json(mod, "/api/logistics");
        lists.GetProperty("active").EnumerateArray().Select(b => b.GetProperty("id").GetInt32()).Should().NotContain(boardId);
        lists.GetProperty("archived").EnumerateArray().Select(b => b.GetProperty("id").GetInt32()).Should().Contain(boardId);
        foreach (var (method, path, body) in new (string, string, object?)[]
        {
            ("POST", $"/api/logistics/cards/{cardId}/comments", new { body = "x" }),
            ("POST", $"/api/logistics/boards/{boardId}/lists", new { name = "x" }),
            ("POST", $"/api/logistics/lists/{listId}/cards", new { title = "x" }),
            ("POST", $"/api/logistics/boards/{boardId}/state", new { completed = true }),
        })
        {
            var client = path.Contains("comments") ? worker : mod;
            (await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(body) })).StatusCode
                .Should().Be(HttpStatusCode.Conflict, $"{method} {path}: archived is read-only");
        }

        await Json(await mod.PostAsJsonAsync($"/api/logistics/boards/{boardId}/archive", new { archived = false }));
        (await Json(mod, $"/api/logistics/cards/{cardId}")).GetProperty("comments").GetArrayLength().Should().Be(1, "restored with its content");
    }

    /// <summary>
    /// 067: copies start clean - new ids, unticked checklists, no owner, collaborators, comments, history, attachments or
    /// reminders - and a board copy is never wider than its source. Templates are previewed and created only as sent.
    /// </summary>
    [Fact]
    public async Task CopiesAndTemplates_StartClean_AndNeverWidenAccess()
    {
        var (boardId, listId, cardId) = await SeedBoardAsync();
        var (_, helper) = await SignInAsync();
        var (member, _) = await SignInAsync();
        var (mod, _) = await SignInAsync("Mod");
        await WithTokenAsync(mod);
        await WithTokenAsync(member);
        await UpdateCardAsync(cardId, c =>
        {
            c.AssignToUser(helper.Id);
            c.SetChecklist("[{\"Id\":0,\"Task\":\"Lavar\",\"Done\":true}]");
            c.Priority = CardPriority.High;
        });
        await Json(await mod.PostAsJsonAsync($"/api/logistics/cards/{cardId}/comments", new { body = "privado" }));
        await Json(await mod.PostAsJsonAsync($"/api/logistics/cards/{cardId}/links", new { url = "https://exemplo.pt" }));
        var name = (await Json(mod, $"/api/logistics/boards/{boardId}")).GetProperty("name").GetString()!;
        await Json(await mod.PutAsJsonAsync($"/api/logistics/boards/{boardId}",
            new { name, description = "", eventId = (int?)null, ownerUserId = (string?)null, visibility = "Private", memberIds = new[] { helper.Id } }));

        (await member.PostAsync($"/api/logistics/cards/{cardId}/duplicate", null)).StatusCode.Should().Be(HttpStatusCode.NotFound, "a private board stays hidden");
        var copy = await Json(await mod.PostAsync($"/api/logistics/cards/{cardId}/duplicate", null));
        copy.GetProperty("card").GetProperty("id").GetInt32().Should().NotBe(cardId);
        copy.GetProperty("card").GetProperty("title").GetString().Should().EndWith("(cópia)");
        copy.GetProperty("card").GetProperty("priority").GetString().Should().Be("High");
        copy.GetProperty("checklist").EnumerateArray().Single().GetProperty("done").GetBoolean().Should().BeFalse();
        (copy.GetProperty("assignedTo").ValueKind, copy.GetProperty("comments").GetArrayLength(), copy.GetProperty("attachments").GetArrayLength())
            .Should().Be((JsonValueKind.Null, 0, 0));
        (await CardIdsAsync(listId)).Should().HaveCount(2);

        var duplicated = await mod.PostAsJsonAsync($"/api/logistics/boards/{boardId}/duplicate", new { name = "Cópia do quadro" });
        duplicated.StatusCode.Should().Be(HttpStatusCode.Created);
        var board = await duplicated.Content.ReadFromJsonAsync<JsonElement>();
        board.GetProperty("visibility").GetString().Should().Be("Private", "a copy is never wider than its source");
        var copied = await Json(mod, $"/api/logistics/boards/{board.GetProperty("id").GetInt32()}");
        copied.GetProperty("members").EnumerateArray().Select(m => m.GetProperty("userId").GetString()).Should().Equal(helper.Id);
        copied.GetProperty("lists").EnumerateArray().Single().GetProperty("cards").GetArrayLength().Should().Be(2);
        copied.GetProperty("files").GetArrayLength().Should().Be(0);
        (await member.GetAsync($"/api/logistics/boards/{board.GetProperty("id").GetInt32()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Templates: managers only; the board is created as previewed and edited.
        (await member.GetAsync("/api/logistics/templates")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var templates = await Json(mod, "/api/logistics/templates");
        templates.EnumerateArray().Select(t => t.GetProperty("name").GetString()).Should().Equal("Organizar FITAB", "Preparar deslocação/atuação");
        var compose = new
        {
            board = new { name = $"{Tag()} FITAB 2027", description = "", eventId = (int?)null, ownerUserId = (string?)null, visibility = "Members", memberIds = Array.Empty<string>() },
            lists = new object[]
            {
                new { name = "Por fazer", state = "Todo", cards = new[] { new { title = "Credenciais", description = "", priority = "Urgent", dueDate = "2027-10-01", checklist = new[] { "Imprimir", "Plastificar" } } } },
                new { name = "Feito", state = "Done", cards = Array.Empty<object>() },
            },
        };
        (await member.PostAsJsonAsync("/api/logistics/boards/compose", compose)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var composed = await mod.PostAsJsonAsync("/api/logistics/boards/compose", compose);
        composed.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await Json(mod, $"/api/logistics/boards/{(await composed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32()}");
        var card = created.GetProperty("lists").EnumerateArray().First().GetProperty("cards").EnumerateArray().Single();
        (card.GetProperty("title").GetString(), card.GetProperty("priority").GetString(), card.GetProperty("checklistTotal").GetInt32(), card.GetProperty("checklistDone").GetInt32())
            .Should().Be(("Credenciais", "Urgent", 2, 0));
        card.GetProperty("dueDate").GetString().Should().StartWith("2027-10-01");
        (await Errors(await mod.PostAsJsonAsync("/api/logistics/boards/compose", compose with { lists = Array.Empty<object>() }))).Keys.Should().Equal("lists");
    }

    /// <summary>
    /// 067: a private board's pre-067 folder is hidden in Documentação from members off its access list - fail closed
    /// (security review): also for a board whose folder is unresolved (null) or shared with another board (""), by its
    /// sanitised name, whatever the case. Managers still see every folder.
    /// </summary>
    [Fact]
    public async Task Documentation_HidesAPrivateBoardsOldFolder()
    {
        var year = FiscalYearHelper.GetCurrentFiscalYearString();
        var (boardId, _, _) = await SeedBoardAsync();
        var folder = $"{Tag()}legacy";
        await UpdateBoardAsync(boardId, b => b.LegacyFolder = folder);
        _factory.Storage.Put($"docs/Test/{year}/Logistics/{folder}/plano.pdf", 10);
        var shared = new List<string>();
        foreach (var unresolved in new string?[] { null, "" })
        {
            var (other, _, _) = await SeedBoardAsync();
            var name = $"{Tag()}Partilhado";
            shared.Add(name);
            _factory.Storage.Put($"docs/Test/{year}/Logistics/{name}/plano.pdf", 10);
            await UpdateBoardAsync(other, b =>
            {
                b.Name = name.ToUpperInvariant();
                b.LegacyFolder = unresolved;
                b.Visibility = BoardVisibility.Private;
            });
        }

        var (member, _) = await SignInAsync();
        var (mod, _) = await SignInAsync("Mod");
        var managed = await mod.GetStringAsync("/api/documentation");
        managed.Should().Contain(folder).And.Contain(shared[0]).And.Contain(shared[1], "the folders are listed: managers see every one");
        var listed = await member.GetStringAsync("/api/documentation");
        listed.Should().Contain(folder, "members-visible board: as before");
        foreach (var name in shared)
        {
            listed.Should().NotContain(name, "a private board hides its folder even when LegacyFolder is null or shared");
            (await member.GetAsync($"/api/documentation/file?folder=Logistics/{name}&name=plano.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        await UpdateBoardAsync(boardId, b => b.Visibility = BoardVisibility.Private);
        (await member.GetStringAsync("/api/documentation")).Should().NotContain(folder);
        (await member.GetAsync($"/api/documentation/file?folder=Logistics/{folder}&name=plano.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// 067 migration rule on real SQLite: a pre-067 column takes its majority status and the others split into
    /// "Name (STATE)" columns right after it, in order; no card changes status; an empty "Feito" column is DONE. Board file
    /// folders: the sanitised name, or none when two boards share it.
    /// </summary>
    [Fact]
    public async Task LegacyColumns_AreMappedWithoutChangingAnyCardsStatus()
    {
        int boardId, mixed, done, twinA, twinB, single;
        var twin = $"{Tag()} Gémeo";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = LogisticsBoard.Create($"{Tag()} Antigo");
            var a = LogisticsBoard.Create(twin);
            var b = LogisticsBoard.Create(twin + "?");
            db.LogisticsBoards.AddRange(board, a, b);
            await db.SaveChangesAsync();
            (boardId, twinA, twinB, single) = (board.Id, a.Id, b.Id, board.Id);
            var m = LogisticsList.Create("Material", board.Id, 0);
            var f = LogisticsList.Create("Feito", board.Id, 1);
            db.LogisticsLists.AddRange(m, f);
            await db.SaveChangesAsync();
            (mixed, done) = (m.Id, f.Id);
            var statuses = new[] { CardStatus.Todo, CardStatus.Done, CardStatus.Todo, CardStatus.InProgress, CardStatus.Done, CardStatus.Todo };
            for (var i = 0; i < statuses.Length; i++)
            {
                var card = LogisticsCard.Create($"c{i}", m.Id, i);
                card.SetStatus(statuses[i]);
                db.LogisticsCards.Add(card);
            }

            await db.SaveChangesAsync();
            await LogisticsWorkflow.MapLegacyAsync(db);
            await LogisticsWorkflow.MapLegacyAsync(db); // idempotent
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lists = await db.LogisticsLists.AsNoTracking().Where(l => l.BoardId == boardId).OrderBy(l => l.Position).ToListAsync();
            lists.Select(l => (l.Name, l.State)).Should().Equal(("Material", CardStatus.Todo), ("Material (WIP)", CardStatus.InProgress),
                ("Material (DONE)", CardStatus.Done), ("Feito", CardStatus.Done));
            var cards = await db.LogisticsCards.AsNoTracking().Where(c => lists.Select(l => l.Id).Contains(c.ListId)).ToListAsync();
            cards.Should().HaveCount(6).And.OnlyContain(c => lists.Single(l => l.Id == c.ListId).State == c.Status, "no card changes status");
            cards.Where(c => c.ListId == mixed).OrderBy(c => c.Position).Select(c => c.Title).Should().Equal("c0", "c2", "c5");
            cards.Where(c => c.Status == CardStatus.Done).OrderBy(c => c.Position).Select(c => c.Title).Should().Equal("c1", "c4");
            (await db.LogisticsBoards.AsNoTracking().SingleAsync(x => x.Id == single)).LegacyFolder.Should().NotBeNullOrEmpty();
            (await db.LogisticsBoards.AsNoTracking().Where(x => x.Id == twinA || x.Id == twinB).Select(x => x.LegacyFolder).ToListAsync())
                .Should().Equal(new[] { "", "" }, "two boards with one folder name cannot be told apart");
            _ = done;
        }
    }

    private async Task UpdateBoardAsync(int id, Action<LogisticsBoard> change)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        change(await db.LogisticsBoards.SingleAsync(b => b.Id == id));
        await db.SaveChangesAsync();
    }

    // ---------- helpers ----------

    private static string Tag() => "lgx" + Guid.NewGuid().ToString("N")[..8];

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
        board.LegacyFolder = "";
        var list = LogisticsList.Create("Material", board.Id, 0);
        list.State = CardStatus.Todo;
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

    private async Task<int> AddListAsync(int boardId, string name, int position, CardStatus state = CardStatus.Todo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var list = LogisticsList.Create(name, boardId, position);
        list.State = state;
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

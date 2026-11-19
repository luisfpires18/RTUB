// LogisticsApiTests tests: who may read and manage boards and cards.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
public partial class LogisticsApiTests
{
    [Fact]
    public async Task Visitors_GetNothing_AndBothPagesAreReact()
    {
        var (boardId, listId, cardId) = await SeedBoardAsync();
        var anonymous = Anonymous();
        await AreaHttp.WithTokenAsync(anonymous);
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
        await AreaHttp.WithTokenAsync(leitao);

        var boards = await AreaHttp.Json(leitao, "/api/logistics");
        boards.GetProperty("canManage").GetBoolean().Should().BeFalse();
        boards.GetProperty("active").EnumerateArray().Select(b => b.GetProperty("id").GetInt32()).Should().Contain(boardId);
        (await AreaHttp.Json(leitao, $"/api/logistics/boards/{boardId}")).GetProperty("canManage").GetBoolean().Should().BeFalse();
        (await AreaHttp.Json(leitao, $"/api/logistics/boards/{boardId}/file-list")).ValueKind.Should().Be(JsonValueKind.Array);
        var detail = await AreaHttp.Json(leitao, $"/api/logistics/cards/{cardId}");
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
        await AreaHttp.WithTokenAsync(worker);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.LogisticsCardAssignments.Add(LogisticsCardAssignment.Create(cardId, me.Id));
            await db.SaveChangesAsync();
        }

        Card(await AreaHttp.Json(worker, $"/api/logistics/boards/{boardId}"), cardId).GetProperty("mine").GetBoolean().Should().BeTrue();
        Card(await AreaHttp.Json(worker, $"/api/logistics/boards/{boardId}"), other).GetProperty("mine").GetBoolean().Should().BeFalse();
        var detail = await AreaHttp.Json(worker, $"/api/logistics/cards/{cardId}");
        (detail.GetProperty("canManage").GetBoolean(), detail.GetProperty("canWork").GetBoolean()).Should().Be((false, true));

        var ticked = await Json(await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/1", new { task = "Contar", done = true }));
        ticked.GetProperty("checklist").EnumerateArray().Select(i => i.GetProperty("done").GetBoolean()).Should().Equal(false, true);
        (await CardAsync(cardId))!.ChecklistJson.Should().Be("[{\"Id\":0,\"Task\":\"Lavar\",\"Done\":false},{\"Id\":0,\"Task\":\"Contar\",\"Done\":true}]",
            "only Done changes; text, order and the stored shape stay");
        await Json(await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/1", new { task = "Contar", done = false }));
        (await CardAsync(cardId))!.ChecklistJson.Should().Be(checklist, "unticking persists too");
        (await AreaHttp.Errors(await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/0", new { task = "Outra", done = true }))).Keys.Should().Equal("items");
        (await AreaHttp.Errors(await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/9", new { task = "Lavar", done = true }))).Keys.Should().Equal("items");
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
        await AreaHttp.WithTokenAsync(mod);
        (await mod.DeleteAsync($"/api/logistics/cards/{cardId}/assignments/{me.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/0", new { task = "Lavar", done = true })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await worker.PostAsJsonAsync($"/api/logistics/cards/{cardId}/status", new { status = "Done" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AreaHttp.Json(worker, $"/api/logistics/cards/{cardId}")).GetProperty("canWork").GetBoolean().Should().BeFalse();
        (await CardAsync(cardId))!.Status.Should().Be(CardStatus.InProgress);

        // Managers keep the tick too, and anonymous callers get nothing.
        (await mod.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/0", new { task = "Lavar", done = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        var anonymous = Anonymous();
        await AreaHttp.WithTokenAsync(anonymous);
        (await anonymous.PostAsJsonAsync($"/api/logistics/cards/{cardId}/checklist/0", new { task = "Lavar", done = false })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Members_ReadTheBoard_WithoutPrivateFields_ButOnlyModAdminOwnerManage()
    {
        var (boardId, listId, cardId) = await SeedBoardAsync();
        var (_, assignee) = await SignInAsync();
        await UpdateCardAsync(cardId, c => c.AssignToUser(assignee.Id));
        var (member, me) = await SignInAsync();
        await AreaHttp.WithTokenAsync(member);

        var boards = await AreaHttp.Json(member, "/api/logistics");
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
            (await AreaHttp.Json(manager, $"/api/logistics/boards/{boardId}")).GetProperty("canManage").GetBoolean().Should().BeTrue(role);
            (await manager.GetAsync($"/api/logistics/cards/{cardId}")).StatusCode.Should().Be(HttpStatusCode.OK, $"{role} opens card details (Owner inherits Admin)");
        }
    }

    private HttpClient Anonymous() => AreaHttp.Anonymous(_factory, "10.94.0.1");
}

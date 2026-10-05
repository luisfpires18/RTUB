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

        var web = typeof(RTUB.App).Assembly;
        web.GetType("RTUB.Pages.Management.Logistics").Should().BeNull("the Blazor boards page was retired");
        web.GetType("RTUB.Pages.Management.LogisticsBoard").Should().BeNull("the Blazor board page was retired");
        web.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
            .Select(r => r.Template)
            .Should().NotContain(new[] { "/logistics", "/logistics/{BoardId:int}", "/documentation", "/shop", "/inventory", "/leaderboard",
                "/members/manage", "/member/events", "/member/gallery", "/member/roles", "/hierarchy" });
    }

    [Fact]
    public async Task Leitoes_AreRefused_UnlessTheyManage()
    {
        var (boardId, _, cardId) = await SeedBoardAsync();
        var (leitao, me) = await SignInAsync();
        await UpdateUserAsync(me.Id, u => u.Categories = new List<MemberCategory> { MemberCategory.Leitao });
        await WithTokenAsync(leitao);
        (await leitao.GetAsync("/api/logistics")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await leitao.GetAsync($"/api/logistics/boards/{boardId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the old board page forgot to send Leitões away; the API does not");
        (await leitao.PostAsJsonAsync($"/api/logistics/boards/{boardId}/reminders", Reminder(cardId, me.Id))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (mod, modUser) = await SignInAsync("Mod");
        await UpdateUserAsync(modUser.Id, u => u.Categories = new List<MemberCategory> { MemberCategory.Leitao });
        (await mod.GetAsync($"/api/logistics/boards/{boardId}")).StatusCode.Should().Be(HttpStatusCode.OK);
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
            "eventName", "assignedTo", "assignments", "checklistDone", "checklistTotal", "links");
        card.GetProperty("assignedTo").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("displayName", "fullName", "avatarUrl");

        (await member.GetAsync($"/api/logistics/cards/{cardId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "members never opened card details");
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
            ("PUT", $"/api/logistics/cards/{cardId}/checklist", new { items = Array.Empty<object>() }),
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
        var updated = await Json(await mod.PutAsJsonAsync($"/api/logistics/cards/{id}", new { title = "Pratos fundos", description = "50", startDate = "2026-10-04", dueDate = "2026-10-10" }));
        updated.GetProperty("card").GetProperty("dueDate").GetString().Should().StartWith("2026-10-10");
        updated.GetProperty("assignedTo").ValueKind.Should().Be(JsonValueKind.Null, "an empty assignee clears it, as the old form");

        (await Errors(await mod.PostAsJsonAsync($"/api/logistics/cards/{id}/status", new { status = "7" }))).Keys.Should().Equal("status");
        (await Json(await mod.PostAsJsonAsync($"/api/logistics/cards/{id}/status", new { status = "InProgress" }))).GetProperty("card").GetProperty("status").GetString().Should().Be("InProgress");

        (await Errors(await mod.PutAsJsonAsync($"/api/logistics/cards/{id}/labels", new { labels = new[] { new { text = "x", color = "red" } } }))).Keys.Should().Equal("labels");
        await Json(await mod.PutAsJsonAsync($"/api/logistics/cards/{id}/labels", new { labels = new[] { new { text = " Urgente ", color = "#FF0000" } } }));
        (await Errors(await mod.PutAsJsonAsync($"/api/logistics/cards/{id}/checklist", new { items = new[] { new { task = " ", done = false } } }))).Keys.Should().Equal("items");
        await Json(await mod.PutAsJsonAsync($"/api/logistics/cards/{id}/checklist", new { items = new[] { new { task = "Lavar", done = true }, new { task = "Contar", done = false } } }));
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

        await Json(await owner.PutAsJsonAsync($"/api/logistics/cards/{cardId}", new { title = "Novo", description = "Outro" }));
        (await CardAsync(cardId))!.Description.Should().Be("Outro\n\n<!-- FILES: [] -->");
    }

    /// <summary>Mod parity is Logistics-only (unit 030): /emails stays Admin-only. Kept from the retired page tests.</summary>
    [Fact]
    public async Task Mod_IsStillRefusedAnAdminOnlyPage()
    {
        var (admin, _) = await SignInAsync("Admin");
        var (mod, _) = await SignInAsync("Mod");
        (await admin.GetAsync("/emails")).StatusCode.Should().Be(HttpStatusCode.OK);
        var refused = await mod.GetAsync("/emails");
        refused.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Forbidden);
        if (refused.StatusCode == HttpStatusCode.Redirect)
        {
            refused.Headers.Location!.ToString().Should().Contain("/login");
        }
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

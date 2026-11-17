// LogisticsApiTests tests: boards, lists, cards, moves and legacy rows.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
public partial class LogisticsApiTests
{
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
    /// works. Members download, managers upload and delete. 104: the files are Documentação documents.
    /// </summary>
    [Fact]
    public async Task BoardFiles_LiveInTheBoardsDocumentationFolder_EveryYearListed()
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
        var key = $"docs/Test/{current}/Logistics/{legacy}/orcamento.pdf";
        _factory.Storage.Objects.Keys.Should().Contain(key, "the board's Documentação folder, this fiscal year");
        (await Json(await mod.PostAsync($"/api/logistics/boards/{boardId}/files", File("recente.pdf")))).GetProperty("name").GetString()
            .Should().Be("recente (2).pdf", "a name already in the folder is never overwritten");
        _factory.Storage.Objects[$"docs/Test/{current}/Logistics/{legacy}/recente.pdf"].Size.Should().Be(20);

        var (member, _) = await SignInAsync();
        var files = (await Json(member, $"/api/logistics/boards/{boardId}/file-list")).EnumerateArray()
            .Select(f => (f.GetProperty("id").GetString(), f.GetProperty("name").GetString(), f.GetProperty("year").GetString())).ToList();
        files.Should().Equal(("f:orcamento.pdf", "orcamento.pdf", null), ("f:recente (2).pdf", "recente (2).pdf", null),
            ("f:recente.pdf", "recente.pdf", null), ($"l:{previous}/antigo.pdf", "antigo.pdf", previous));
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

    private static IEnumerable<string?> Names(JsonElement board) =>
        board.GetProperty("lists").EnumerateArray().Select(l => l.GetProperty("name").GetString());

    private async Task<int> AddEventAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var e = Event.Create(name, DateTime.Today.AddDays(10), "Bragança", EventType.Atuacao, "");
        db.Events.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private async Task<List<(int Id, int Position)>> PositionsAsync(int listId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCards.AsNoTracking().Where(c => c.ListId == listId)
            .OrderBy(c => c.Position).ThenBy(c => c.Id).Select(c => new { c.Id, c.Position }).ToListAsync()).Select(c => (c.Id, c.Position)).ToList();
    }
}

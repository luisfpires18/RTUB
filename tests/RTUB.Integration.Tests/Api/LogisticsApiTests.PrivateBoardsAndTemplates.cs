// LogisticsApiTests tests: private boards, columns, archive, copies and templates.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Integration.Tests.Api;
public partial class LogisticsApiTests
{
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
}

// LogisticsApiTests tests: assignee workflow, comments and attachments.
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
        var key = _factory.Storage.Objects.Keys.Single(k => k.EndsWith("/orcamento.pdf") && k.Contains("/Logistics/"));
        key.Should().NotContain("..").And.Be($"docs/Test/{FiscalYearHelper.GetCurrentFiscalYearString()}/Logistics/{await BoardFolderAsync(boardId)}/orcamento.pdf",
            "card files are documents in the board's Documentação folder");
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
        var (boardId, listId, cardId) = await SeedBoardAsync(cards: 2);
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
        var folder = $"docs/Test/{FiscalYearHelper.GetCurrentFiscalYearString()}/Logistics/{await BoardFolderAsync(boardId)}/";

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
        _factory.Storage.Objects.Keys.Count(k => k.StartsWith(folder) && k.EndsWith(".pdf")).Should().Be(1, "a refused upload stores nothing");

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
        _factory.Storage.Objects.Keys.Should().NotContain(k => k.StartsWith(folder) && k.EndsWith(".pdf"));

        // Deleting the card takes its stored files too.
        await Json(await owner.PostAsync($"/api/logistics/cards/{cardId}/attachments", File("final.pdf")));
        (await mod.DeleteAsync($"/api/logistics/cards/{cardId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Storage.Objects.Keys.Should().NotContain(k => k.StartsWith(folder) && k.EndsWith(".pdf"));
    }

    private async Task<int> AssignmentCountAsync(int cardId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LogisticsCardAssignments.CountAsync(a => a.CardId == cardId);
    }
}

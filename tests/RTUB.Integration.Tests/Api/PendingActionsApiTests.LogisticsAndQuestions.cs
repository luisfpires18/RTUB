// PendingActionsApiTests tests: logistics cards and questions panels.
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Integration.Tests.Api;
public partial class PendingActionsApiTests
{
    // ---------- Logística and Questões (073) ----------

    [Fact]
    public async Task Logistics_ShowsOnlyTheMembersOwnOpenCardsOnBoardsTheySee_AndConcluirIsTheBoardsOwnMove()
    {
        var (member, me) = await SignInAsync([MemberCategory.Leitao], retired: false); // own work: Leitões too
        await WithTokenAsync(member);
        var (_, other) = await SignInAsync([MemberCategory.Tuno], retired: false);
        var board = await SeedBoardAsync(withDone: true);
        var owned = await AddCardAsync(board.Todo, "Comprar cordas", owner: me.Id);
        var helping = await AddCardAsync(board.Todo, "Montar palco", owner: other.Id, collaborator: me.Id);
        var someoneElses = await AddCardAsync(board.Todo, "Carrinha", owner: other.Id);
        var unowned = await AddCardAsync(board.Todo, "Sem dono");
        var done = await AddCardAsync(board.Done, "Já feito", owner: me.Id);
        var privateBoard = await SeedBoardAsync(withDone: true, isPrivate: true);
        var hidden = await AddCardAsync(privateBoard.Todo, "Segredo", owner: me.Id);

        var page = await member.GetFromJsonAsync<JsonElement>("/api/pending-actions");
        page.GetProperty("panels").EnumerateArray().Select(p => p.GetProperty("key").GetString()).Should().Contain("logistics")
            .And.NotContain("questions", "Questões is the Direção's panel (087)");
        var keys = await AllKeysAsync(member, "logistics", null);
        keys.Should().Contain([owned.ToString(), helping.ToString()])
            .And.NotContain([someoneElses.ToString(), unowned.ToString(), done.ToString(), hidden.ToString()],
                "not mine, done, or on a private board I am not on (assignment alone does not open it)");

        (await Answer(member, "logistics.card", someoneElses, "done")).StatusCode.Should().Be(HttpStatusCode.NotFound, "another member's card");
        (await Answer(member, "logistics.card", hidden, "done")).StatusCode.Should().Be(HttpStatusCode.NotFound, "a board I cannot see");
        (await Answer(member, "logistics.card", owned, "yes")).StatusCode.Should().Be(HttpStatusCode.BadRequest, "not a logistics command");
        (await Answer(member, "logistics.card", owned, "done")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Answer(member, "logistics.card", owned, "done")).StatusCode.Should().Be(HttpStatusCode.NotFound, "a retry finds it done");

        var card = await member.GetFromJsonAsync<JsonElement>($"/api/logistics/cards/{owned}");
        card.GetProperty("card").GetProperty("status").GetString().Should().Be("Done", "the board's own status change");
        card.GetProperty("listId").GetInt32().Should().Be(board.Done, "moved to the done column");
        (await member.GetFromJsonAsync<JsonElement>("/api/logistics/my-tasks")).GetProperty("tasks").EnumerateArray()
            .Single(t => t.GetProperty("cardId").GetInt32() == owned).GetProperty("status").GetString().Should().Be("Done", "As minhas tarefas agrees");
        (await AllKeysAsync(member, "logistics", null)).Should().NotContain(owned.ToString());

        // Unassigned meanwhile: the card leaves, and Concluir is refused.
        await UnassignAsync(helping, me.Id);
        (await AllKeysAsync(member, "logistics", null)).Should().NotContain(helping.ToString());
        (await Answer(member, "logistics.card", helping, "done")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Logistics_ABoardWithoutADoneColumn_RefusesHonestly_AndTheCardStays()
    {
        var (member, me) = await SignInAsync([MemberCategory.Tuno], retired: false);
        await WithTokenAsync(member);
        var board = await SeedBoardAsync(withDone: false);
        var card = await AddCardAsync(board.Todo, "Sem coluna feita", owner: me.Id);

        (await Answer(member, "logistics.card", card, "done")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AllKeysAsync(member, "logistics", null)).Should().Contain(card.ToString());
    }

    [Fact]
    public async Task Questions_ListsTheAuthorsOpenQuestions_AndOnlyTheAuthorCloses()
    {
        var (author, me) = await SignInAsync([MemberCategory.Tuno], retired: false, positions: [Position.Secretario]);
        var (recipient, them) = await SignInAsync([MemberCategory.Tuno], retired: false, positions: [Position.Magister]);
        var (plain, _) = await SignInAsync([MemberCategory.Tuno], retired: false);
        await WithTokenAsync(author);
        await WithTokenAsync(recipient);
        (await plain.GetAsync("/api/pending-actions/panels/questions")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "Questões is the Direção's panel (087)");
        var mine = await AddQuestionAsync(me.Id, them.Id, "Quando é o jantar?");
        var closed = await AddQuestionAsync(me.Id, them.Id, "Antiga", closed: true);
        var theirs = await AddQuestionAsync(them.Id, me.Id, "Pergunta deles");

        (await AllKeysAsync(author, "questions", null)).Should().Contain(mine.ToString()).And.NotContain([closed.ToString(), theirs.ToString()]);
        (await AllKeysAsync(recipient, "questions", null)).Should().Contain(theirs.ToString()).And.NotContain(mine.ToString(),
            "being the recipient does not let you close it");

        (await Answer(recipient, "question.close", mine, "close")).StatusCode.Should().Be(HttpStatusCode.NotFound, "a forged close by the recipient");
        (await Answer(author, "question.close", mine, "close")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Answer(author, "question.close", mine, "close")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var detail = await author.GetFromJsonAsync<JsonElement>($"/api/questions/{mine}");
        detail.GetProperty("question").GetProperty("status").GetString().Should().Be("closed", "the module's own close");
        (await AllKeysAsync(author, "questions", null)).Should().NotContain(mine.ToString());
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Questions.AsNoTracking().SingleAsync(q => q.Id == mine))
            .Content.Should().Be("Conteúdo da pergunta", "the thread is kept");
    }

    private async Task UnassignAsync(int cardId, string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.LogisticsCardAssignments.RemoveRange(db.LogisticsCardAssignments.Where(a => a.CardId == cardId && a.UserId == userId));
        await db.SaveChangesAsync();
    }

    private async Task<int> AddQuestionAsync(string authorId, string recipientId, string title, bool closed = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var q = Question.Create(title, "Conteúdo da pergunta", authorId, Position.Magister, recipientId);
        if (closed)
        {
            q.Close();
        }

        db.Questions.Add(q);
        await db.SaveChangesAsync();
        return q.Id;
    }
}

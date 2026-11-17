// Logistics: duplicate a board, templates and composing a board from cards.
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class LogisticsKanbanService
{
    /// <summary>
    /// A copy of a board (managers): its columns and active cards (copied as <see cref="DuplicateCardAsync"/>), its
    /// visibility, access list and responsible member - never wider than the source. No files, no archived cards.
    /// </summary>
    public async Task<EventResult<LogisticsBoardSummaryDto>> DuplicateBoardAsync(int id, LogisticsDuplicateInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (refused, source) = await SeeBoardAsync<LogisticsBoardSummaryDto>(db, id, user, manage: true);
        if (refused is not null)
        {
            return refused;
        }

        var name = input.Name?.Trim() is { Length: > 0 and <= 200 } n ? n : null;
        if (name is null)
        {
            return EventResult<LogisticsBoardSummaryDto>.Invalid("name", "O nome do quadro é obrigatório (máx. 200 caracteres).");
        }

        var board = LogisticsBoard.Create(name, source!.Description);
        board.AssociateWithEvent(source.EventId);
        board.Visibility = source.Visibility;
        board.OwnerUserId = source.OwnerUserId;
        board.LegacyFolder = await FolderForAsync(db, 0, board.Name, null);
        foreach (var member in await db.LogisticsBoardMembers.Where(m => m.BoardId == id).ToListAsync())
        {
            board.Members.Add(new LogisticsBoardMember { UserId = member.UserId });
        }

        db.LogisticsBoards.Add(board);
        await db.SaveChangesAsync();
        foreach (var list in await db.LogisticsLists.Where(l => l.BoardId == id).OrderBy(l => l.Position).ThenBy(l => l.Id).ToListAsync())
        {
            var copy = LogisticsList.Create(list.Name, board.Id, list.Position);
            copy.State = list.State ?? CardStatus.Todo;
            db.LogisticsLists.Add(copy);
            await db.SaveChangesAsync();
            var i = 0;
            foreach (var card in await db.LogisticsCards.Where(c => c.ListId == list.Id && c.ArchivedAt == null).OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync())
            {
                var c = Copy(card, copy.Id, copy.State.Value, card.Title);
                c.UpdatePosition(i++);
                db.LogisticsCards.Add(c);
            }
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return EventResult<LogisticsBoardSummaryDto>.Ok(await SummaryAsync(db, board.Id, user));
    }

    public async Task<EventResult<IReadOnlyList<LogisticsTemplateDto>>> GetTemplatesAsync(ClaimsPrincipal user) =>
        await RefusalAsync<IReadOnlyList<LogisticsTemplateDto>>(user, manage: true) is { } refused
            ? refused
            : EventResult<IReadOnlyList<LogisticsTemplateDto>>.Ok(LogisticsTemplates.All);

    /// <summary>
    /// A new board from a reviewed structure (a template's preview, edited on the page; managers): its columns and cards
    /// with checklists, dates and priority, plus the usual board form (visibility, access list, responsible member).
    /// Nothing is assigned or notified.
    /// </summary>
    public async Task<EventResult<LogisticsBoardSummaryDto>> ComposeBoardAsync(LogisticsComposeInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsBoardSummaryDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        var lists = input.Lists ?? Array.Empty<LogisticsComposeListInput>();
        var errors = new Dictionary<string, string[]>();
        var allCards = lists.SelectMany(l => l.Cards ?? Array.Empty<LogisticsComposeCardInput>()).ToList();
        if (lists.Count is 0 or > 12 || lists.Any(l => l.Name?.Trim() is not { Length: > 0 and <= 100 } || State(l.State, CardStatus.Todo) is null))
        {
            errors["lists"] = new[] { "Entre 1 e 12 colunas, cada uma com nome (máx. 100) e estado." };
        }

        var priority = CardPriority.Normal;
        if (allCards.Count > MaxComposedCards
            || allCards.Any(c => CardErrors(c.Title, c.Description).Count > 0
                || (c.Checklist?.Count ?? 0) > MaxChecklistItems || (c.Checklist ?? Array.Empty<string>()).Any(t => TaskText(t) is null)
                || (c.Priority is not null && (!Enum.TryParse(c.Priority, out priority) || !Enum.IsDefined(priority) || int.TryParse(c.Priority, out _)))
                || (!string.IsNullOrEmpty(c.DueDate) && !DateTime.TryParseExact(c.DueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))))
        {
            errors["cards"] = new[] { "Cada tarefa precisa de título (máx. 200), datas válidas e no máximo 100 itens de checklist (máx. 200 caracteres)." };
        }

        if (errors.Count > 0)
        {
            return new EventResult<LogisticsBoardSummaryDto>(EventResultStatus.Invalid, Errors: errors);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var board = LogisticsBoard.Create(input.Board.Name?.Trim() is { Length: > 0 } n ? n : "x");
        board.LegacyFolder = await FolderForAsync(db, 0, board.Name, null);
        if (await ApplyBoardInputAsync(db, board, input.Board, creating: true) is { } invalid)
        {
            return invalid;
        }

        db.LogisticsBoards.Add(board);
        await db.SaveChangesAsync();
        var me = LogisticsAuthorization.UserId(user);
        for (var li = 0; li < lists.Count; li++)
        {
            var l = lists[li];
            var list = LogisticsList.Create(l.Name!.Trim(), board.Id, li);
            list.State = State(l.State, CardStatus.Todo)!.Value;
            db.LogisticsLists.Add(list);
            await db.SaveChangesAsync();
            var ci = 0;
            foreach (var c in l.Cards ?? Array.Empty<LogisticsComposeCardInput>())
            {
                var card = LogisticsCard.Create(c.Title!.Trim(), list.Id, ci++, c.Description?.Trim() ?? "");
                card.SetStatus(list.State.Value);
                card.Priority = c.Priority is null ? CardPriority.Normal : Enum.Parse<CardPriority>(c.Priority);
                card.SetDates(null, string.IsNullOrEmpty(c.DueDate) ? null : DateTime.ParseExact(c.DueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture), null);
                card.SetChecklist(JsonSerializer.Serialize((c.Checklist ?? Array.Empty<string>()).Select(t => new StoredTask(0, TaskText(t)!, false))));
                card.AssociateWithEvent(input.Board.EventId);
                db.LogisticsCards.Add(card);
                await db.SaveChangesAsync();
                db.LogisticsCardActivities.Add(new LogisticsCardActivity { CardId = card.Id, ActorId = me, Action = "created", Detail = "Cartão criado (modelo)" });
            }
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return EventResult<LogisticsBoardSummaryDto>.Ok(await SummaryAsync(db, board.Id, user));
    }

    private static void AddDefaultColumns(ApplicationDbContext db, int boardId)
    {
        for (var i = 0; i < DefaultColumns.Length; i++)
        {
            var list = LogisticsList.Create(DefaultColumns[i].Name, boardId, i);
            list.State = DefaultColumns[i].State;
            db.LogisticsLists.Add(list);
        }
    }

    // ---------------------------------------------------------------- members, reminders
}
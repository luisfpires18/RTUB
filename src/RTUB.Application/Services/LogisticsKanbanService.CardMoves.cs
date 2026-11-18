// Logistics: create, move, restatus, archive and duplicate cards.
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class LogisticsKanbanService
{
    public async Task<EventResult<LogisticsCardDetailDto>> CreateCardAsync(int listId, LogisticsCardCreateInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeListBoardAsync<LogisticsCardDetailDto>(db, listId, user, manage: true, writable: true);
        if (refused is not null)
        {
            return refused;
        }

        var errors = CardErrors(input.Title, input.Description);
        if (!string.IsNullOrEmpty(input.AssignedToUserId) && !(await BoardViewersAsync(db, board!, new[] { input.AssignedToUserId })).Contains(input.AssignedToUserId))
        {
            errors["assignedToUserId"] = new[] { NoAccess };
        }

        if (errors.Count > 0)
        {
            return new EventResult<LogisticsCardDetailDto>(EventResultStatus.Invalid, Errors: errors);
        }

        var list = (await db.LogisticsLists.FindAsync(listId))!;
        // Last in its column, with the column's state.
        var last = await db.LogisticsCards.Where(c => c.ListId == listId).Select(c => (int?)c.Position).MaxAsync() ?? -1;
        var card = LogisticsCard.Create(input.Title!.Trim(), listId, last + 1, input.Description?.Trim() ?? "");
        card.AssignToUser(string.IsNullOrEmpty(input.AssignedToUserId) ? null : input.AssignedToUserId);
        card.SetStatus(list.State ?? CardStatus.Todo);
        db.LogisticsCards.Add(card);
        await db.SaveChangesAsync();
        db.LogisticsCardActivities.Add(new LogisticsCardActivity { CardId = card.Id, ActorId = LogisticsAuthorization.UserId(user), Action = "created", Detail = "Cartão criado" });
        await db.SaveChangesAsync();
        if (_notifier is not null && card.AssignedToUserId is { } owner)
        {
            var me = LogisticsAuthorization.UserId(user);
            await _notifier.SendAsync(LogisticsNotice.Owner, new LogisticsNotifier.Card(board!.Id, card.Id, card.Title, board.Name), new[] { owner }, me);
        }

        return EventResult<LogisticsCardDetailDto>.Ok(await DetailAsync(db, card.Id, user));
    }

    /// <summary>
    /// Move a card to a position in a column of the same board: managers, or the card's owner and collaborators (their own
    /// cards; columns themselves stay managers'). The card takes the column's state.
    /// </summary>
    public async Task<EventResult<bool>> MoveCardAsync(int id, LogisticsMoveInput input, ClaimsPrincipal user)
    {
        var moved = await ChangeCardAsync(id, user, assignedMayChange: true, async (db, card, access) =>
        {
            if (await db.LogisticsLists.FindAsync(input.ListId) is not { } target || target.BoardId != access.BoardId)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("listId", "Escolha uma lista deste quadro.");
            }

            await PlaceAsync(db, card, target, input.Position, access);
            return null;
        });
        return moved.Status == EventResultStatus.Ok ? EventResult<bool>.Ok(true) : new EventResult<bool>(moved.Status, Errors: moved.Errors);
    }

    /// <summary>
    /// A status change is a move: to the end of the first column (by position) with that state. A board without such a
    /// column refuses it, so status and column never disagree.
    /// </summary>
    public Task<EventResult<LogisticsCardDetailDto>> SetStatusAsync(int id, LogisticsStatusInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: true, async (db, card, access) =>
        {
            if (!Enum.TryParse<CardStatus>(input.Status, out var status) || !Enum.IsDefined(status) || int.TryParse(input.Status, out _))
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("status", "Estado inválido.");
            }

            var current = await db.LogisticsLists.FindAsync(card.ListId);
            if (current?.State == status)
            {
                return null;
            }

            if (await db.LogisticsLists.Where(l => l.BoardId == access.BoardId && l.State == status).OrderBy(l => l.Position).ThenBy(l => l.Id)
                    .FirstOrDefaultAsync() is not { } target)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("status", $"Este quadro não tem uma coluna {StatusName(status)}.");
            }

            await PlaceAsync(db, card, target, int.MaxValue, access);
            return null;
        });

    /// <summary>The one move rule: position in the target column, renumber both columns, status = the column's state, history.</summary>
    private static async Task PlaceAsync(ApplicationDbContext db, LogisticsCard card, LogisticsList target, int position, CardAccess access)
    {
        var from = card.ListId;
        var entering = await db.LogisticsCards.Where(c => c.ListId == target.Id && c.Id != card.Id && c.ArchivedAt == null)
            .OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync();
        entering.Insert(Math.Clamp(position, 0, entering.Count), card);
        card.MoveToList(target.Id, 0);
        Renumber(entering);
        if (from != target.Id)
        {
            Renumber(await db.LogisticsCards.Where(c => c.ListId == from && c.Id != card.Id && c.ArchivedAt == null)
                .OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync());
            Log(db, card.Id, access, "list", $"Movido para «{Cut(target.Name)}»");
        }

        var state = target.State ?? card.Status;
        if (state != card.Status)
        {
            Log(db, card.Id, access, "status", $"Estado: {StatusName(card.Status)} → {StatusName(state)}");
            card.SetStatus(state);
        }
    }

    /// <summary>Archive (out of the columns, read-only) or restore a card; managers, on a board that is not archived.</summary>
    public async Task<EventResult<LogisticsCardDetailDto>> ArchiveCardAsync(int id, LogisticsArchiveInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var boardId = await db.LogisticsCards.Where(c => c.Id == id).Select(c => (int?)c.List.BoardId).FirstOrDefaultAsync();
        var (refused, _) = boardId is { } b
            ? await SeeBoardAsync<LogisticsCardDetailDto>(db, b, user, manage: true, writable: true)
            : (await RefusalAsync<LogisticsCardDetailDto>(user, manage: false) ?? EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound), null);
        if (refused is not null)
        {
            return refused;
        }

        var card = (await db.LogisticsCards.FindAsync(id))!;
        var access = await AccessAsync(db, card, user);
        if (input.Archived && card.ArchivedAt is null)
        {
            card.ArchivedAt = DateTime.UtcNow;
            Log(db, id, access, "archive", "Cartão arquivado");
            Renumber(await db.LogisticsCards.Where(c => c.ListId == card.ListId && c.Id != id && c.ArchivedAt == null)
                .OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync());
        }
        else if (!input.Archived && card.ArchivedAt is not null)
        {
            card.ArchivedAt = null;
            card.UpdatePosition((await db.LogisticsCards.Where(c => c.ListId == card.ListId && c.ArchivedAt == null).Select(c => (int?)c.Position).MaxAsync() ?? -1) + 1);
            Log(db, id, access, "archive", "Cartão restaurado");
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return EventResult<LogisticsCardDetailDto>.Ok(await DetailAsync(db, id, user));
    }

    /// <summary>
    /// A copy of a card, right after it in the same column (managers): title, details, labels, priority, dates and the
    /// checklist (all unticked). Never the owner, collaborators, comments, history, attachments, reminders, card links or
    /// status beyond the column's.
    /// </summary>
    public async Task<EventResult<LogisticsCardDetailDto>> DuplicateCardAsync(int id, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var boardId = await db.LogisticsCards.Where(c => c.Id == id).Select(c => (int?)c.List.BoardId).FirstOrDefaultAsync();
        var (refused, _) = boardId is { } b
            ? await SeeBoardAsync<LogisticsCardDetailDto>(db, b, user, manage: true, writable: true)
            : (await RefusalAsync<LogisticsCardDetailDto>(user, manage: false) ?? EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound), null);
        if (refused is not null)
        {
            return refused;
        }

        var source = (await db.LogisticsCards.Include(c => c.List).SingleAsync(c => c.Id == id));
        var siblings = await db.LogisticsCards.Where(c => c.ListId == source.ListId && c.ArchivedAt == null).OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync();
        var copy = Copy(source, source.ListId, source.List.State ?? CardStatus.Todo, $"{source.Title} (cópia)");
        db.LogisticsCards.Add(copy);
        var at = siblings.FindIndex(c => c.Id == id);
        siblings.Insert(at < 0 ? siblings.Count : at + 1, copy);
        Renumber(siblings);
        await db.SaveChangesAsync();
        db.LogisticsCardActivities.Add(new LogisticsCardActivity
        {
            CardId = copy.Id, ActorId = LogisticsAuthorization.UserId(user), Action = "created", Detail = $"Cartão criado (cópia de «{Cut(source.Title)}»)",
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return EventResult<LogisticsCardDetailDto>.Ok(await DetailAsync(db, copy.Id, user));
    }

    private static LogisticsCard Copy(LogisticsCard source, int listId, CardStatus state, string title)
    {
        var description = source.Description ?? "";
        var marker = description.IndexOf(LegacyFilesMarker, StringComparison.Ordinal);
        var copy = LogisticsCard.Create(Cut(title, 200), listId, source.Position, marker < 0 ? description : description[..marker].TrimEnd());
        copy.SetLabels(source.Labels);
        copy.SetDates(source.StartDate, source.DueDate, null);
        copy.SetChecklist(JsonSerializer.Serialize(Tasks(source.ChecklistJson).Select(t => t with { Done = false })));
        copy.Priority = source.Priority;
        copy.SetStatus(state);
        copy.AssociateWithEvent(source.EventId);
        return copy;
    }

    // ---------------------------------------------------------------- duplicate boards and templates
}
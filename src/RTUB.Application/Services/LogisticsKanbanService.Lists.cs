// Logistics: board lists (columns): create, rename, move, delete.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class LogisticsKanbanService
{
    public async Task<EventResult<LogisticsListDto>> CreateListAsync(int boardId, LogisticsListInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, _) = await SeeBoardAsync<LogisticsListDto>(db, boardId, user, manage: true, writable: true);
        if (refused is not null)
        {
            return refused;
        }

        if (ListName(input) is not { } name)
        {
            return EventResult<LogisticsListDto>.Invalid("name", "O nome da lista é obrigatório (máx. 100 caracteres).");
        }

        if (State(input.State, CardStatus.Todo) is not { } state)
        {
            return EventResult<LogisticsListDto>.Invalid("state", "Estado inválido.");
        }

        // Last, as before.
        var last = await db.LogisticsLists.Where(l => l.BoardId == boardId).Select(l => (int?)l.Position).MaxAsync() ?? -1;
        var list = LogisticsList.Create(name, boardId, last + 1);
        list.State = state;
        db.LogisticsLists.Add(list);
        await db.SaveChangesAsync();
        return EventResult<LogisticsListDto>.Ok(new LogisticsListDto(list.Id, list.Name, Array.Empty<LogisticsCardDto>(), state.ToString()));
    }

    /// <summary>Rename and / or remap a column (managers). A new state is applied to every card in it, in the same save.</summary>
    public async Task<EventResult<LogisticsListDto>> RenameListAsync(int id, LogisticsListInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (refused, _) = await SeeListBoardAsync<LogisticsListDto>(db, id, user, manage: true, writable: true);
        if (refused is not null)
        {
            return refused;
        }

        var list = (await db.LogisticsLists.FindAsync(id))!;
        if (ListName(input) is not { } name)
        {
            return EventResult<LogisticsListDto>.Invalid("name", "O nome da lista é obrigatório (máx. 100 caracteres).");
        }

        if (State(input.State, list.State ?? CardStatus.Todo) is not { } state)
        {
            return EventResult<LogisticsListDto>.Invalid("state", "Estado inválido.");
        }

        list.UpdateName(name);
        if (state != list.State)
        {
            list.State = state;
            foreach (var card in await db.LogisticsCards.Where(c => c.ListId == id && c.Status != state).ToListAsync())
            {
                db.LogisticsCardActivities.Add(new LogisticsCardActivity
                {
                    CardId = card.Id, ActorId = LogisticsAuthorization.UserId(user), Action = "status",
                    Detail = $"Estado: {StatusName(card.Status)} → {StatusName(state)} (coluna «{Cut(name)}»)",
                });
                card.SetStatus(state);
            }
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return EventResult<LogisticsListDto>.Ok(new LogisticsListDto(list.Id, list.Name, Array.Empty<LogisticsCardDto>(), state.ToString()));
    }

    public async Task<EventResult<bool>> MoveListAsync(int id, LogisticsPositionInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, _) = await SeeListBoardAsync<bool>(db, id, user, manage: true, writable: true);
        if (refused is not null)
        {
            return refused;
        }

        var list = (await db.LogisticsLists.FindAsync(id))!;
        var others = await db.LogisticsLists.Where(l => l.BoardId == list.BoardId && l.Id != id).OrderBy(l => l.Position).ThenBy(l => l.Id).ToListAsync();
        others.Insert(Math.Clamp(input.Position, 0, others.Count), list);
        for (var i = 0; i < others.Count; i++)
        {
            others[i].UpdatePosition(i);
        }

        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    /// <summary>
    /// Delete a column (managers). Its cards - archived ones too - never disappear with it: an empty column goes at once;
    /// otherwise <paramref name="moveTo"/> (another column of the board) receives them, at its end, with its state.
    /// </summary>
    public async Task<EventResult<bool>> DeleteListAsync(int id, int? moveTo, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (refused, _) = await SeeListBoardAsync<bool>(db, id, user, manage: true, writable: true);
        if (refused is not null)
        {
            return refused;
        }

        var list = (await db.LogisticsLists.FindAsync(id))!;
        var cards = await db.LogisticsCards.Where(c => c.ListId == id).OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync();
        if (cards.Count > 0)
        {
            if (moveTo is not { } targetId || targetId == id || await db.LogisticsLists.FindAsync(targetId) is not { } target || target.BoardId != list.BoardId)
            {
                return EventResult<bool>.Invalid("moveTo", "Esta coluna tem cartões: escolha outra coluna deste quadro para onde passam.");
            }

            var state = target.State ?? CardStatus.Todo;
            var position = (await db.LogisticsCards.Where(c => c.ListId == targetId).Select(c => (int?)c.Position).MaxAsync() ?? -1) + 1;
            foreach (var card in cards)
            {
                db.LogisticsCardActivities.Add(new LogisticsCardActivity
                {
                    CardId = card.Id, ActorId = LogisticsAuthorization.UserId(user), Action = "list",
                    Detail = card.Status == state ? $"Movido para «{Cut(target.Name)}» (coluna eliminada)"
                        : $"Movido para «{Cut(target.Name)}» (coluna eliminada); estado: {StatusName(card.Status)} → {StatusName(state)}",
                });
                card.MoveToList(targetId, position++);
                card.SetStatus(state);
            }
        }

        db.LogisticsLists.Remove(list);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return EventResult<bool>.Ok(true);
    }

    // ---------------------------------------------------------------- cards: create, move, status, archive, duplicate
}
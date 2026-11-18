// Logistics: list, create, update, state, archive and delete boards.
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
    public async Task<EventResult<LogisticsBoardsDto>> GetBoardsAsync(string? search, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsBoardsDto>(user, manage: false) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var q = search?.Trim() ?? "";
        var boards = await VisibleBoards(db, user).AsNoTracking().Include(b => b.Event)
            .Where(b => q == "" || b.Name.Contains(q) || b.Description.Contains(q))
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
        var summaries = await SummariesAsync(db, boards, user);
        return EventResult<LogisticsBoardsDto>.Ok(new LogisticsBoardsDto(
            summaries.Where(b => b.ArchivedAt is null && !b.IsCompleted).ToList(),
            summaries.Where(b => b.ArchivedAt is null && b.IsCompleted).ToList(),
            LogisticsAuthorization.CanManage(user),
            summaries.Where(b => b.ArchivedAt is not null).ToList()));
    }

    public async Task<EventResult<LogisticsBoardSummaryDto>> CreateBoardAsync(LogisticsBoardInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<LogisticsBoardSummaryDto>(user, manage: true) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var board = LogisticsBoard.Create(input.Name?.Trim() is { Length: > 0 } n ? n : "x", input.Description?.Trim() ?? "");
        if (await ApplyBoardInputAsync(db, board, input, creating: true) is { } invalid)
        {
            return invalid;
        }

        board.LegacyFolder = await FolderForAsync(db, 0, board.Name, null);
        db.LogisticsBoards.Add(board);
        await db.SaveChangesAsync();
        AddDefaultColumns(db, board.Id);
        await db.SaveChangesAsync();
        return EventResult<LogisticsBoardSummaryDto>.Ok(await SummaryAsync(db, board.Id, user));
    }

    /// <summary>Name, description, event, responsible member, visibility and access list (managers; archived: read-only).</summary>
    public async Task<EventResult<LogisticsBoardSummaryDto>> UpdateBoardAsync(int id, LogisticsBoardInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (refused, board) = await SeeBoardAsync<LogisticsBoardSummaryDto>(db, id, user, manage: true, writable: true);
        if (refused is not null)
        {
            return refused;
        }

        var oldName = board!.Name;
        if (await ApplyBoardInputAsync(db, board, input, creating: false) is { } invalid)
        {
            return invalid;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        if (board.Name != oldName)
        {
            _audit.SetUser(user.Identity?.Name, user.UserId());
            await FollowRenameAsync(db, board);
        }

        return EventResult<LogisticsBoardSummaryDto>.Ok(await SummaryAsync(db, id, user));
    }

    /// <summary>
    /// Validates and applies a board form. Private boards: the responsible member must be on the access list (or staff);
    /// the list is replaced as sent. Visibility null keeps it (an older page), Members on a new board.
    /// </summary>
    private static async Task<EventResult<LogisticsBoardSummaryDto>?> ApplyBoardInputAsync(ApplicationDbContext db, LogisticsBoard board,
        LogisticsBoardInput input, bool creating)
    {
        var errors = new Dictionary<string, string[]>();
        var name = input.Name?.Trim() ?? "";
        if (name == "" || name.Length > 200)
        {
            errors["name"] = new[] { "O nome do quadro é obrigatório (máx. 200 caracteres)." };
        }

        if ((input.Description?.Trim().Length ?? 0) > 2000)
        {
            errors["description"] = new[] { "A descrição não pode exceder 2000 caracteres" };
        }

        if (input.EventId is { } eventId && !await db.Events.AnyAsync(e => e.Id == eventId))
        {
            errors["eventId"] = new[] { "Evento não encontrado." };
        }

        var visibility = board.Visibility;
        if (input.Visibility is not null && (!Enum.TryParse(input.Visibility, out visibility) || !Enum.IsDefined(visibility) || int.TryParse(input.Visibility, out _)))
        {
            errors["visibility"] = new[] { "Visibilidade inválida." };
        }

        var members = (input.MemberIds ?? Array.Empty<string>()).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
        if (members.Count > MaxBoardMembers || await db.Users.CountAsync(u => members.Contains(u.Id) && !u.IsExpelled) != members.Count)
        {
            errors["memberIds"] = new[] { "Escolha membros da tuna (no máximo 200)." };
        }

        var owner = string.IsNullOrEmpty(input.OwnerUserId) ? null : input.OwnerUserId;
        if (owner is not null && errors.Count == 0)
        {
            var probe = new LogisticsBoard { Visibility = visibility };
            if (!(await BoardViewersAsync(db, probe, new[] { owner }, members)).Contains(owner))
            {
                errors["ownerUserId"] = new[] { visibility == BoardVisibility.Private
                    ? "O responsável tem de estar na lista de acesso deste quadro privado."
                    : "Membro não encontrado." };
            }
        }

        if (errors.Count > 0)
        {
            return new EventResult<LogisticsBoardSummaryDto>(EventResultStatus.Invalid, Errors: errors);
        }

        board.UpdateDetails(name, input.Description?.Trim() ?? "");
        board.AssociateWithEvent(input.EventId);
        board.OwnerUserId = owner;
        board.Visibility = visibility;
        if (input.MemberIds is not null || creating)
        {
            var current = creating ? new List<LogisticsBoardMember>() : await db.LogisticsBoardMembers.Where(m => m.BoardId == board.Id).ToListAsync();
            db.LogisticsBoardMembers.RemoveRange(current.Where(m => !members.Contains(m.UserId)));
            foreach (var id in members.Where(m => current.All(c => c.UserId != m)))
            {
                board.Members.Add(new LogisticsBoardMember { BoardId = board.Id, UserId = id });
            }
        }

        return null;
    }

    public async Task<EventResult<LogisticsBoardSummaryDto>> SetBoardStateAsync(int id, LogisticsBoardStateInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeBoardAsync<LogisticsBoardSummaryDto>(db, id, user, manage: true, writable: true);
        if (refused is not null)
        {
            return refused;
        }

        if (input.Completed)
        {
            board!.MarkAsCompleted();
        }
        else
        {
            board!.MarkAsNotCompleted();
        }

        await db.SaveChangesAsync();
        return EventResult<LogisticsBoardSummaryDto>.Ok(await SummaryAsync(db, id, user));
    }

    /// <summary>Archive (read-only, out of the active lists) or restore; managers. Content is untouched.</summary>
    public async Task<EventResult<LogisticsBoardSummaryDto>> ArchiveBoardAsync(int id, LogisticsArchiveInput input, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeBoardAsync<LogisticsBoardSummaryDto>(db, id, user, manage: true);
        if (refused is not null)
        {
            return refused;
        }

        board!.ArchivedAt = input.Archived ? board.ArchivedAt ?? DateTime.UtcNow : null;
        await db.SaveChangesAsync();
        return EventResult<LogisticsBoardSummaryDto>.Ok(await SummaryAsync(db, id, user));
    }

    public async Task<EventResult<bool>> DeleteBoardAsync(int id, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeBoardAsync<bool>(db, id, user, manage: true);
        if (refused is not null)
        {
            return refused;
        }

        // Its lists and cards go with it (assignments, reminders, comments, history and attachment rows by cascade). Its
        // Documentação folder stays: board and card files alike remain there as documents.
        db.LogisticsBoards.Remove(board!);
        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    // ---------------------------------------------------------------- one board
}
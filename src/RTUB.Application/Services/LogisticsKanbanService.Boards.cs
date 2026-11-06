using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Task 067 (Logística 3/5): boards with a responsible member and an access list, workflow columns, stable board files,
/// archive, duplicates and templates.
///
/// - Access: <see cref="CanSeeBoardAsync"/> is the one board rule (members-visible, or private to its access list; Mod,
///   Admin and Owner always). Everything under a board - lists, cards, details, comments, mentions, member pickers,
///   counts, files, reminders - goes through it; a board the caller may not see is 404 (not disclosed). Assignment never
///   grants access; the responsible member gets no rights from being responsible.
/// - Workflow: every column has a state (TODO / WIP / BLOQUEADO / DONE) and is authoritative: a card's status is its
///   column's state. Moving a card sets its status; a status change moves the card to the first column with that state.
/// - Files: new board files live at logistics/{env}/boards/{id}/files/ (stable across renames and the 1 September
///   rollover, not in Documentação); the pre-067 folder docs/{env}/{year}/Logistics/{LegacyFolder}/ stays listed for
///   every year.
/// - Archive: boards and cards are read-only while archived (409 "closed"); managers archive and restore.
/// </summary>
public sealed partial class LogisticsKanbanService
{
    public const int MaxBoardMembers = 200;
    public const int MaxComposedCards = 200;

    private static readonly CardStatus[] Workflow = [CardStatus.Todo, CardStatus.InProgress, CardStatus.Blocked, CardStatus.Done];

    /// <summary>The default columns of a new board, one per state.</summary>
    private static readonly (string Name, CardStatus State)[] DefaultColumns =
        [("Por fazer", CardStatus.Todo), ("Em curso", CardStatus.InProgress), ("Bloqueado", CardStatus.Blocked), ("Feito", CardStatus.Done)];

    // ---------------------------------------------------------------- access

    /// <summary>The board rule: managers, every member on a members board, the access list on a private one.</summary>
    private static async Task<bool> CanSeeBoardAsync(ApplicationDbContext db, LogisticsBoard board, ClaimsPrincipal user)
    {
        if (LogisticsAuthorization.CanManage(user) || board.Visibility == BoardVisibility.Members)
        {
            return true;
        }

        var me = LogisticsAuthorization.UserId(user);
        return me is not null && await db.LogisticsBoardMembers.AnyAsync(m => m.BoardId == board.Id && m.UserId == me);
    }

    /// <summary>The boards the caller sees (for lists, counts and search).</summary>
    private static IQueryable<LogisticsBoard> VisibleBoards(ApplicationDbContext db, ClaimsPrincipal user)
    {
        if (LogisticsAuthorization.CanManage(user))
        {
            return db.LogisticsBoards;
        }

        var me = LogisticsAuthorization.UserId(user);
        return db.LogisticsBoards.Where(b => b.Visibility == BoardVisibility.Members || b.Members.Any(m => m.UserId == me));
    }

    /// <summary>
    /// Which of <paramref name="userIds"/> may see <paramref name="board"/> (assignees, mentions, reminder targets, the
    /// responsible member): not expelled, and on a private board either on its access list or Mod / Admin / Owner.
    /// </summary>
    private static async Task<HashSet<string>> BoardViewersAsync(ApplicationDbContext db, LogisticsBoard board, IReadOnlyCollection<string> userIds,
        IReadOnlyCollection<string>? accessList = null)
    {
        var ids = userIds.Distinct().ToList();
        var active = await db.Users.Where(u => ids.Contains(u.Id) && !u.IsExpelled).Select(u => u.Id).ToListAsync();
        if (board.Visibility == BoardVisibility.Members)
        {
            return active.ToHashSet();
        }

        var listed = accessList?.ToHashSet()
            ?? (await db.LogisticsBoardMembers.Where(m => m.BoardId == board.Id && active.Contains(m.UserId)).Select(m => m.UserId).ToListAsync()).ToHashSet();
        var staff = await StaffAsync(db, active);
        return active.Where(u => listed.Contains(u) || staff.Contains(u)).ToHashSet();
    }

    private static async Task<HashSet<string>> StaffAsync(ApplicationDbContext db, List<string> userIds) =>
        (await db.UserRoles.Where(ur => userIds.Contains(ur.UserId))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .Where(x => x.Name == "Admin" || x.Name == "Mod" || x.Name == "Owner")
            .Select(x => x.UserId).ToListAsync()).ToHashSet();

    /// <summary>A board the caller sees (tracked), or 401 / 404 (a private board is never disclosed).</summary>
    private async Task<(EventResult<T>? Refused, LogisticsBoard? Board)> SeeBoardAsync<T>(ApplicationDbContext db, int boardId, ClaimsPrincipal user,
        bool manage = false, bool writable = false)
    {
        if (await RefusalAsync<T>(user, manage: false) is { } refused)
        {
            return (refused, null);
        }

        if (await db.LogisticsBoards.FindAsync(boardId) is not { } board || !await CanSeeBoardAsync(db, board, user))
        {
            return (EventResult<T>.Fail(EventResultStatus.NotFound), null);
        }

        if (manage && !LogisticsAuthorization.CanManage(user))
        {
            return (EventResult<T>.Fail(EventResultStatus.Forbidden), null);
        }

        if (writable && board.ArchivedAt is not null)
        {
            return (EventResult<T>.Fail(EventResultStatus.Closed), null);
        }

        return (null, board);
    }

    private async Task<(EventResult<T>? Refused, LogisticsBoard? Board)> SeeListBoardAsync<T>(ApplicationDbContext db, int listId, ClaimsPrincipal user,
        bool manage = false, bool writable = false)
    {
        var boardId = await db.LogisticsLists.Where(l => l.Id == listId).Select(l => (int?)l.BoardId).FirstOrDefaultAsync();
        if (boardId is null)
        {
            return (await RefusalAsync<T>(user, manage: false) ?? EventResult<T>.Fail(EventResultStatus.NotFound), null);
        }

        return await SeeBoardAsync<T>(db, boardId.Value, user, manage, writable);
    }

    // ---------------------------------------------------------------- boards

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
        board.LegacyFolder = "";
        if (await ApplyBoardInputAsync(db, board, input, creating: true) is { } invalid)
        {
            return invalid;
        }

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

        if (await ApplyBoardInputAsync(db, board!, input, creating: false) is { } invalid)
        {
            return invalid;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
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

        // Its lists and cards go with it, as before (assignments, reminders, comments, history and attachments by
        // cascade); the cards' own files are deleted after the save. Board files stay in storage, as before.
        var files = await AttachmentPathsAsync(db, c => c.List.BoardId == id);
        db.LogisticsBoards.Remove(board!);
        await db.SaveChangesAsync();
        await DeleteStoredAsync(files);
        return EventResult<bool>.Ok(true);
    }

    // ---------------------------------------------------------------- one board

    public async Task<EventResult<LogisticsBoardDto>> GetBoardAsync(int id, ClaimsPrincipal user, bool includeFiles = true)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, tracked) = await SeeBoardAsync<LogisticsBoardDto>(db, id, user);
        if (refused is not null)
        {
            return refused;
        }

        var board = tracked!;
        await db.Entry(board).Reference(b => b.Event).LoadAsync();
        var manage = LogisticsAuthorization.CanManage(user);
        var lists = await db.LogisticsLists.AsNoTracking().Where(l => l.BoardId == id).OrderBy(l => l.Position).ThenBy(l => l.Id).ToListAsync();
        var listIds = lists.Select(l => l.Id).ToList();
        var cards = await db.LogisticsCards.AsNoTracking().Include(c => c.Event).Include(c => c.AssignedToUser)
            .Where(c => listIds.Contains(c.ListId)).ToListAsync();
        var faces = await FacesAsync(db, cards, user);
        var members = await db.LogisticsBoardMembers.AsNoTracking().Include(m => m.User).Where(m => m.BoardId == id).OrderBy(m => m.Id)
            .Select(m => m.User).ToListAsync();
        var owner = board.OwnerUserId is null ? null : await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == board.OwnerUserId);

        return EventResult<LogisticsBoardDto>.Ok(new LogisticsBoardDto(
            board.Id, board.Name, board.Description, Event(board.Event), board.IsCompleted,
            lists.Select(l => new LogisticsListDto(l.Id, l.Name, cards.Where(c => c.ListId == l.Id && c.ArchivedAt is null)
                .OrderBy(c => c.Position).ThenBy(c => c.Id).Select(c => faces[c.Id]).ToList(), (l.State ?? CardStatus.Todo).ToString())).ToList(),
            faces.Values.SelectMany(c => c.Labels).Select(l => l.Text).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList(),
            includeFiles ? await BoardFilesAsync(board) : new List<LogisticsFileDto>(),
            manage,
            owner is null ? null : Assignee(owner, manage),
            board.Visibility.ToString(),
            members.Select(u => Assignee(u, manage)).ToList(),
            board.ArchivedAt is null ? null : Utc(board.ArchivedAt.Value),
            cards.Where(c => c.ArchivedAt is not null).OrderByDescending(c => c.ArchivedAt).Select(c => faces[c.Id]).ToList()));
    }

    /// <summary>Card faces with the caller's "mine", collaborator, comment and attachment counts.</summary>
    private static async Task<Dictionary<int, LogisticsCardDto>> FacesAsync(ApplicationDbContext db, List<LogisticsCard> cards, ClaimsPrincipal user)
    {
        var cardIds = cards.Select(c => c.Id).ToList();
        var assignments = await AssignmentCountsAsync(db, cardIds);
        var me = LogisticsAuthorization.UserId(user);
        var mine = (await db.LogisticsCardAssignments.AsNoTracking().Where(a => a.UserId == me && cardIds.Contains(a.CardId))
            .Select(a => a.CardId).ToListAsync()).ToHashSet();
        var comments = await db.LogisticsCardComments.AsNoTracking().Where(c => cardIds.Contains(c.CardId))
            .GroupBy(c => c.CardId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
        var attachments = await db.LogisticsCardAttachments.AsNoTracking().Where(a => cardIds.Contains(a.CardId))
            .GroupBy(a => a.CardId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
        return cards.ToDictionary(c => c.Id, c => Face(c, assignments.GetValueOrDefault(c.Id), c.AssignedToUserId == me || mine.Contains(c.Id),
            comments.GetValueOrDefault(c.Id), attachments.GetValueOrDefault(c.Id)));
    }

    /// <summary>One summary per board: responsible, visibility, and its active cards' progress, overdue, unowned and mine.</summary>
    private async Task<List<LogisticsBoardSummaryDto>> SummariesAsync(ApplicationDbContext db, List<LogisticsBoard> boards, ClaimsPrincipal user)
    {
        var ids = boards.Select(b => b.Id).ToList();
        var me = LogisticsAuthorization.UserId(user);
        var today = PortugalTime.Today(_time);
        var cards = await db.LogisticsCards.AsNoTracking().Where(c => ids.Contains(c.List.BoardId) && c.ArchivedAt == null)
            .Select(c => new
            {
                c.List.BoardId, c.Status, c.DueDate, c.AssignedToUserId,
                Mine = c.AssignedToUserId == me || c.Assignments.Any(a => a.UserId == me),
            })
            .ToListAsync();
        var ownerIds = boards.Select(b => b.OwnerUserId).OfType<string>().Distinct().ToList();
        var owners = await db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var manage = LogisticsAuthorization.CanManage(user);
        return boards.Select(b =>
        {
            var own = cards.Where(c => c.BoardId == b.Id).ToList();
            return new LogisticsBoardSummaryDto(b.Id, b.Name, b.Description, Event(b.Event), b.IsCompleted, b.CompletedAt,
                b.OwnerUserId is not null && owners.TryGetValue(b.OwnerUserId, out var o) ? Assignee(o, manage) : null,
                b.Visibility.ToString(), b.ArchivedAt is null ? null : Utc(b.ArchivedAt.Value),
                own.Count, own.Count(c => c.Status == CardStatus.Done),
                own.Count(c => c.Status != CardStatus.Done && c.DueDate is { } d && d.Date < today),
                own.Count(c => c.Status != CardStatus.Done && c.AssignedToUserId == null),
                own.Count(c => c.Mine && c.Status != CardStatus.Done));
        }).ToList();
    }

    private async Task<LogisticsBoardSummaryDto> SummaryAsync(ApplicationDbContext db, int id, ClaimsPrincipal user) =>
        (await SummariesAsync(db, await db.LogisticsBoards.AsNoTracking().Include(b => b.Event).Where(b => b.Id == id).ToListAsync(), user)).Single();

    // ---------------------------------------------------------------- columns (lists)

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
        board.LegacyFolder = "";
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
        board.LegacyFolder = "";
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

    /// <summary>
    /// The member picker (≤ 20, not expelled). With <paramref name="boardId"/>: only members who may see that board (the
    /// responsible member, assignees, mentions and reminders are chosen from them); 404 for a board the caller may not see.
    /// </summary>
    public async Task<EventResult<IReadOnlyList<LogisticsMemberOptionDto>>> SearchMembersAsync(string? search, ClaimsPrincipal user, int? boardId = null)
    {
        if (await RefusalAsync<IReadOnlyList<LogisticsMemberOptionDto>>(user, manage: false) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        LogisticsBoard? board = null;
        if (boardId is { } id)
        {
            var (hidden, seen) = await SeeBoardAsync<IReadOnlyList<LogisticsMemberOptionDto>>(db, id, user);
            if (hidden is not null)
            {
                return hidden;
            }

            board = seen;
        }

        var words = EventParticipantsAdminService.Fold(search).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return EventResult<IReadOnlyList<LogisticsMemberOptionDto>>.Ok(Array.Empty<LogisticsMemberOptionDto>());
        }

        // ponytail: every member in memory (~100), as the old pages loaded every user; page it if it grows.
        var members = await db.Users.AsNoTracking().Where(u => !u.IsExpelled).ToListAsync();
        var found = members
            .Select(u => (u, who: GovernanceService.ToMember(u.Nickname, u.FirstName, u.LastName, u.ImageUrl),
                text: EventParticipantsAdminService.Fold($"{u.UserName} {u.Nickname} {u.FirstName} {u.LastName}")))
            .Where(x => words.All(w => x.text.Contains(w)))
            .ToList();
        if (board is not null)
        {
            var viewers = await BoardViewersAsync(db, board, found.Select(x => x.u.Id).ToList());
            found = found.Where(x => viewers.Contains(x.u.Id)).ToList();
        }

        return EventResult<IReadOnlyList<LogisticsMemberOptionDto>>.Ok(found
            .OrderBy(x => x.who.DisplayName, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .Take(SearchLimit)
            .Select(x => new LogisticsMemberOptionDto(x.u.Id, x.who.DisplayName, x.who.FullName, x.who.AvatarUrl))
            .ToList());
    }

    public async Task<EventResult<LogisticsReminderDto>> CreateReminderAsync(int boardId, LogisticsReminderInput input, ClaimsPrincipal user)
    {
        // Every member who sees the board, as before (not only managers); the targets must see it too.
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeBoardAsync<LogisticsReminderDto>(db, boardId, user, writable: true);
        if (refused is not null)
        {
            return refused;
        }

        var errors = new Dictionary<string, string[]>();
        if (!await db.LogisticsCards.AnyAsync(c => c.Id == input.CardId && c.List.BoardId == boardId))
        {
            errors["cardId"] = new[] { "Selecione um cartão deste quadro." };
        }

        if (!Enum.TryParse<ReminderFrequency>(input.Frequency, out var frequency) || !Enum.IsDefined(frequency) || int.TryParse(input.Frequency, out _))
        {
            errors["frequency"] = new[] { "Frequência inválida." };
        }

        if (input.NextReminderDate is null)
        {
            errors["nextReminderDate"] = new[] { "Indique a primeira data do lembrete." };
        }

        var userIds = (input.UserIds ?? Array.Empty<string>()).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        if (userIds.Count == 0 || userIds.Count > 100 || (await BoardViewersAsync(db, board!, userIds)).Count != userIds.Count)
        {
            errors["userIds"] = new[] { "Pelo menos um utilizador deve ser selecionado" };
        }

        if (errors.Count > 0)
        {
            return new EventResult<LogisticsReminderDto>(EventResultStatus.Invalid, Errors: errors);
        }

        var reminder = LogisticsCardReminder.Create(input.CardId, frequency, string.Join(",", userIds), input.NextReminderDate!.Value);
        db.LogisticsCardReminders.Add(reminder);
        await db.SaveChangesAsync();
        return EventResult<LogisticsReminderDto>.Ok(new LogisticsReminderDto(reminder.Id, reminder.CardId, reminder.Frequency.ToString(),
            reminder.NextReminderDate, userIds.Count));
    }

    // ---------------------------------------------------------------- board files

    /// <summary>The board's own, stable folder: by id, outside the fiscal-year Documentação tree.</summary>
    private string BoardFolder(int boardId) => $"logistics/{_environment.EnvironmentName}/boards/{boardId}/files/";

    private string LegacyPrefix => $"docs/{_environment.EnvironmentName}/";

    /// <summary>
    /// The board's files: its stable folder, then its pre-067 folder in every fiscal year (newest first), each with an
    /// opaque id ("f:name" or "l:year/name") - the key is always rebuilt from the board, never taken from input.
    /// </summary>
    private async Task<List<(LogisticsFileDto Dto, string Path)>> BoardFileEntriesAsync(LogisticsBoard board)
    {
        var entries = new List<(LogisticsFileDto, string)>();
        foreach (var d in await ListAsync(BoardFolder(board.Id), board.Name))
        {
            entries.Add((new LogisticsFileDto(d.FileName, d.Extension, d.SizeBytes, $"f:{d.FileName}", null), d.FilePath));
        }

        if (!string.IsNullOrEmpty(board.LegacyFolder))
        {
            List<string> years;
            try
            {
                years = await _storage.ListSubfoldersAsync(LegacyPrefix);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not list the fiscal years for logistics board {Board}", board.Id);
                years = new();
            }

            foreach (var year in years.Where(y => System.Text.RegularExpressions.Regex.IsMatch(y, @"^\d{4}-\d{4}$")).OrderByDescending(y => y, StringComparer.Ordinal))
            {
                foreach (var d in await ListAsync($"{LegacyPrefix}{year}/Logistics/{board.LegacyFolder}/", board.Name))
                {
                    entries.Add((new LogisticsFileDto(d.FileName, d.Extension, d.SizeBytes, $"l:{year}/{d.FileName}", year), d.FilePath));
                }
            }
        }

        return entries;
    }

    private async Task<List<LogisticsFileDto>> BoardFilesAsync(LogisticsBoard board) => (await BoardFileEntriesAsync(board)).Select(e => e.Dto).ToList();

    private async Task<List<DocumentMetadata>> ListAsync(string folder, string boardName)
    {
        try
        {
            return await _storage.ListDocumentsInFolderAsync(folder);
        }
        catch (Exception ex)
        {
            // The old page treated the files as "not critical" too: the board still opens.
            _logger.LogWarning(ex, "Could not list the files of logistics board {Board}", boardName);
            return new();
        }
    }

    public async Task<EventResult<IReadOnlyList<LogisticsFileDto>>> GetFilesAsync(int boardId, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeBoardAsync<IReadOnlyList<LogisticsFileDto>>(db, boardId, user);
        return refused ?? EventResult<IReadOnlyList<LogisticsFileDto>>.Ok(await BoardFilesAsync(board!));
    }

    public async Task<EventResult<LogisticsFileDto>> UploadFileAsync(int boardId, LogisticsFileUpload upload, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeBoardAsync<LogisticsFileDto>(db, boardId, user, manage: true, writable: true);
        if (refused is not null)
        {
            return refused;
        }

        var name = SafeFileName(upload.FileName);
        if (upload.Length <= 0 || upload.Length > MaxFileBytes)
        {
            return EventResult<LogisticsFileDto>.Invalid("file", "O ficheiro é demasiado grande. Tamanho máximo: 10MB");
        }

        if (name is null || !FileTypes.Contains(upload.ContentType))
        {
            return EventResult<LogisticsFileDto>.Invalid("file", "Tipo de ficheiro não suportado. Formatos permitidos: PDF, Word, Excel, TXT, PNG, JPG");
        }

        _audit.SetUser(user.Identity?.Name, LogisticsAuthorization.UserId(user));
        await _storage.UploadDocumentAsync(BoardFolder(board!.Id), name, upload.Content, upload.ContentType);
        return EventResult<LogisticsFileDto>.Ok(new LogisticsFileDto(name, Path.GetExtension(name), upload.Length, $"f:{name}", null));
    }

    public async Task<EventResult<DocumentLinkDto>> GetFileAsync(int boardId, string? fileId, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeBoardAsync<DocumentLinkDto>(db, boardId, user);
        if (refused is not null)
        {
            return refused;
        }

        if (await FilePathAsync(board!, fileId) is not { } path)
        {
            return EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
        }

        return await _storage.GetDocumentUrlAsync(path, forceDownload: true) is { } url
            ? EventResult<DocumentLinkDto>.Ok(new DocumentLinkDto(url))
            : EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<bool>> DeleteFileAsync(int boardId, string? fileId, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeBoardAsync<bool>(db, boardId, user, manage: true, writable: true);
        if (refused is not null)
        {
            return refused;
        }

        if (await FilePathAsync(board!, fileId) is not { } path)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        _audit.SetUser(user.Identity?.Name, LogisticsAuthorization.UserId(user));
        await _storage.DeleteDocumentAsync(path);
        _trees.Forget(path); // a legacy folder also shows in Documentação (#226 F01)
        return EventResult<bool>.Ok(true);
    }

    /// <summary>
    /// The key of one of the board's listed files, by its id - or, for older pages, by its bare name (the stable folder
    /// first, then the newest year).
    /// </summary>
    private async Task<string?> FilePathAsync(LogisticsBoard board, string? fileId)
    {
        if (string.IsNullOrEmpty(fileId))
        {
            return null;
        }

        var entries = await BoardFileEntriesAsync(board);
        return (entries.FirstOrDefault(e => e.Dto.Id == fileId).Path)
            ?? entries.FirstOrDefault(e => e.Dto.Name == fileId).Path;
    }

    // ---------------------------------------------------------------- helpers

    private const string NoAccess = "Este membro não tem acesso a este quadro. Dê-lhe acesso primeiro (Editar quadro).";

    private static CardStatus? State(string? value, CardStatus fallback)
    {
        if (string.IsNullOrEmpty(value))
        {
            return fallback;
        }

        return Enum.TryParse<CardStatus>(value, out var state) && Enum.IsDefined(state) && !int.TryParse(value, out _) ? state : null;
    }
}

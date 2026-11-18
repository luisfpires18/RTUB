using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Boards with a responsible member and an access list, workflow columns, stable board files,
/// archive, duplicates and templates.
///
/// - Access: <see cref="CanSeeBoardAsync"/> is the one board rule (members-visible, or private to its access list; Mod,
///   Admin and Owner always). Everything under a board - lists, cards, details, comments, mentions, member pickers,
///   counts, files, reminders - goes through it; a board the caller may not see is 404 (not disclosed). Assignment never
///   grants access; the responsible member gets no rights from being responsible.
/// - Workflow: every column has a state (TODO / WIP / BLOQUEADO / DONE) and is authoritative: a card's status is its
///   column's state. Moving a card sets its status; a status change moves the card to the first column with that state.
/// - Files: in the board's Documentação folder, docs/{env}/{year}/Logistics/{LegacyFolder}/, every year listed
///   (LogisticsKanbanService.Documents.cs).
/// - Archive: boards and cards are read-only while archived (409 "closed"); managers archive and restore.
/// </summary>
public sealed partial class LogisticsKanbanService
{
    public const int MaxBoardMembers = 200;

    public const int MaxComposedCards = 200;

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
    internal static async Task<HashSet<string>> BoardViewersAsync(ApplicationDbContext db, LogisticsBoard board, IReadOnlyCollection<string> userIds,
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

    internal static Task<HashSet<string>> StaffIdsAsync(ApplicationDbContext db, List<string> userIds) => StaffAsync(db, userIds);

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

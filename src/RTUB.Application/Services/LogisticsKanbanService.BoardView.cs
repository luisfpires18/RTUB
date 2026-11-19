// Logistics: read one board with its lists and cards, and the board summaries.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public sealed partial class LogisticsKanbanService
{
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
        var me = user.UserId();
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
        var me = user.UserId();
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
}
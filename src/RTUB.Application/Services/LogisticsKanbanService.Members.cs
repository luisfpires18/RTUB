// Logistics: member picker for board access and assignment.
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class LogisticsKanbanService
{
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

        // ponytail: every member in memory (~100); page it if it grows.
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

    // ---------------------------------------------------------------- board files
}
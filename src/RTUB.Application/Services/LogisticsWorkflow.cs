using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Task 067: brings pre-067 boards onto the workflow rules, once, at startup (idempotent: only rows not mapped yet).
///
/// Columns (State null): every card keeps its status, so a column gets the status most of its cards have (ties: TODO,
/// WIP, BLOQUEADO, DONE, in that order); cards with another status move, in their order, to a new column "Name (STATE)"
/// right after it. An empty column is DONE when its name says so ("feito", "done", "concluído"), WIP when it says
/// "curso" / "wip" / "progress", otherwise TODO. Column positions are renumbered in that order; no card is lost or changes
/// status.
///
/// Board files (LegacyFolder null): the board's sanitised name, the folder its files had under
/// docs/{env}/{year}/Logistics/ before 067 - or "" when another board has the same folder name (the files cannot be told
/// apart; they stay in Documentação, as before). Nothing is moved or deleted in storage.
/// </summary>
public static class LogisticsWorkflow
{
    private static readonly CardStatus[] Order = [CardStatus.Todo, CardStatus.InProgress, CardStatus.Blocked, CardStatus.Done];

    public static async Task<(int Columns, int Split, int Boards)> MapLegacyAsync(ApplicationDbContext db)
    {
        var columns = 0;
        var split = 0;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var boardIds = await db.LogisticsLists.Where(l => l.State == null).Select(l => l.BoardId).Distinct().ToListAsync();
        foreach (var boardId in boardIds)
        {
            var lists = await db.LogisticsLists.Where(l => l.BoardId == boardId).OrderBy(l => l.Position).ThenBy(l => l.Id).ToListAsync();
            var ordered = new List<LogisticsList>();
            foreach (var list in lists)
            {
                ordered.Add(list);
                if (list.State is not null)
                {
                    continue;
                }

                columns++;
                var cards = await db.LogisticsCards.Where(c => c.ListId == list.Id).OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync();
                list.State = cards.Count == 0
                    ? ByName(list.Name)
                    : cards.GroupBy(c => c.Status).OrderByDescending(g => g.Count()).ThenBy(g => Array.IndexOf(Order, g.Key)).First().Key;
                Renumber(cards.Where(c => c.Status == list.State).ToList());
                foreach (var state in Order.Where(s => s != list.State && cards.Any(c => c.Status == s)))
                {
                    var name = $"{list.Name} ({Label(state)})";
                    var column = LogisticsList.Create(name.Length <= 100 ? name : $"{list.Name[..(99 - Label(state).Length - 3)]}… ({Label(state)})", boardId, 0);
                    column.State = state;
                    db.LogisticsLists.Add(column);
                    await db.SaveChangesAsync();
                    var moving = cards.Where(c => c.Status == state).ToList();
                    for (var i = 0; i < moving.Count; i++)
                    {
                        moving[i].MoveToList(column.Id, i);
                    }

                    ordered.Add(column);
                    split++;
                }
            }

            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].UpdatePosition(i);
            }

            await db.SaveChangesAsync();
        }

        var unresolved = await db.LogisticsBoards.Where(b => b.LegacyFolder == null).ToListAsync();
        if (unresolved.Count > 0)
        {
            var all = await db.LogisticsBoards.Select(b => new { b.Id, b.Name }).ToListAsync();
            var folders = all.ToDictionary(b => b.Id, b => LogisticsKanbanService.SanitizePathComponent(b.Name));
            foreach (var board in unresolved)
            {
                var folder = folders[board.Id];
                board.LegacyFolder = folder != "" && folders.Count(f => f.Value == folder) == 1 ? folder : "";
            }

            await db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return (columns, split, unresolved.Count);
    }

    private static CardStatus ByName(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("feito") || n.Contains("done") || n.Contains("conclu"))
        {
            return CardStatus.Done;
        }

        return n.Contains("curso") || n.Contains("wip") || n.Contains("progress") ? CardStatus.InProgress : CardStatus.Todo;
    }

    private static string Label(CardStatus state) => state switch
    {
        CardStatus.InProgress => "WIP",
        CardStatus.Blocked => "BLOQUEADO",
        CardStatus.Done => "DONE",
        _ => "TODO",
    };

    private static void Renumber(List<LogisticsCard> cards)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            cards[i].UpdatePosition(i);
        }
    }
}

// Logistics: moving board folders and card files into Documentacao.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class LogisticsKanbanService
{
    /// <summary>
    /// Moves a board's folder, in every fiscal year, to <paramref name="target"/> as one step: every file is copied first
    /// (a failed copy removes the copies made and changes nothing), then the card rows and the board's folder change in one
    /// save, then the sources are deleted. A source that cannot be deleted stays in the old folder, now owned by no board
    /// (Mod, Admin and Owner only): a duplicate, never the only copy. Returns how many files moved, or null when nothing changed.
    /// </summary>
    private async Task<int?> MoveFolderAsync(ApplicationDbContext db, LogisticsBoard board, string target, IReadOnlyList<string> years)
    {
        var source = board.LegacyFolder!;
        var copies = new List<(string From, string To)>();
        var created = new List<string>();
        try
        {
            foreach (var year in years)
            {
                var files = await ListAsync(DocsFolder(year, source), source);
                if (files.Count == 0)
                {
                    continue;
                }

                var to = DocsFolder(year, target);
                var taken = (await ListAsync(to, target)).Select(d => d.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                created.Add(to);
                await _storage.CreateFolderAsync(to);
                foreach (var file in files)
                {
                    var copy = to + await FreeNameAsync(to, file.FileName, taken);
                    await _storage.CopyDocumentAsync(file.FilePath, copy);
                    copies.Add((file.FilePath, copy));
                }
            }

            foreach (var (from, to) in copies)
            {
                foreach (var row in await db.LogisticsCardAttachments.Where(a => a.StoragePath == from).ToListAsync())
                {
                    row.StoragePath = to;
                    row.Name = Path.GetFileName(to);
                }
            }

            board.LegacyFolder = target;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not move the folder of logistics board {Board} from {From} to {To}; it keeps its old folder", board.Id, source, target);
            Revert(db);
            board.LegacyFolder = source;
            foreach (var (_, to) in copies)
            {
                await TryDeleteAsync(to);
            }

            foreach (var folder in created)
            {
                await DropIfEmptyAsync(folder); // else the next try would take the empty folder for another board's
            }

            return null;
        }

        foreach (var (from, to) in copies)
        {
            await TryDeleteAsync(from);
            _trees.Forget(to);
        }

        foreach (var year in years)
        {
            await DropIfEmptyAsync(DocsFolder(year, source));
            _trees.Forget(DocsFolder(year, source));
        }

        return copies.Count;
    }

    private async Task TryDeleteAsync(string path)
    {
        try
        {
            await _storage.DeleteDocumentAsync(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete the logistics file {Path}", path);
        }
    }

    /// <summary>Unsaved card-row changes must not ride along a later save.</summary>
    private static void Revert(ApplicationDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries<LogisticsCardAttachment>().Where(e => e.State == EntityState.Modified))
        {
            entry.CurrentValues.SetValues(entry.OriginalValues);
            entry.State = EntityState.Unchanged;
        }
    }

    /// <summary>After a rename: the folder follows the board's new name (no move when the name maps to the same folder).</summary>
    private async Task FollowRenameAsync(ApplicationDbContext db, LogisticsBoard board)
    {
        if (string.IsNullOrEmpty(board.LegacyFolder))
        {
            return;
        }

        var years = await DocsYearsAsync();
        var target = await FolderForAsync(db, board.Id, board.Name, board.LegacyFolder, await StoredFoldersAsync(years));
        if (!string.Equals(target, board.LegacyFolder, StringComparison.Ordinal))
        {
            await MoveFolderAsync(db, board, target, years); // on failure the next start tries again
        }
    }

    public async Task<int> MoveFilesIntoDocumentationAsync()
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var years = await DocsYearsAsync();
        var stored = await StoredFoldersAsync(years);
        var outside = await _storage.ListTreeAsync($"logistics/{_environment.EnvironmentName}/boards/");
        if (outside is null)
        {
            _logger.LogWarning("Could not list the logistics files kept outside Documentação; trying again at the next start");
            return 0;
        }

        var moved = 0;
        foreach (var board in await db.LogisticsBoards.OrderBy(b => b.Id).ToListAsync())
        {
            // The folder: the board's current name; files under an older name follow it.
            var target = await FolderForAsync(db, board.Id, board.Name, board.LegacyFolder, stored);
            if (string.IsNullOrEmpty(board.LegacyFolder))
            {
                board.LegacyFolder = target;
                await db.SaveChangesAsync();
            }
            else if (!string.Equals(target, board.LegacyFolder, StringComparison.Ordinal))
            {
                moved += await MoveFolderAsync(db, board, target, years) ?? 0;
            }

            stored.Add(target);

            // Files kept outside Documentação: the board's stable folder, then its cards' files.
            var boardFiles = $"logistics/{_environment.EnvironmentName}/boards/{board.Id}/files/";
            var files = outside.Where(o => o.FilePath.StartsWith(boardFiles, StringComparison.Ordinal) && !o.FilePath.EndsWith('/')
                && o.FilePath.IndexOf('/', boardFiles.Length) < 0).Select(o => o.FilePath).ToList();
            var cardFiles = (await AttachmentPathsAsync(db, c => c.List.BoardId == board.Id))
                .Where(p => p.StartsWith("logistics/", StringComparison.Ordinal)).ToList();
            if (files.Count + cardFiles.Count == 0)
            {
                continue;
            }

            // Into the folder the board has now (the old one while a rename is unfinished), so the board always lists them.
            var folder = DocsFolder(CurrentYear, board.LegacyFolder!);
            var existing = await ExistingAsync(folder, board.LegacyFolder!);
            foreach (var path in files)
            {
                moved += await MoveIntoAsync(db, path, folder, existing) ? 1 : 0;
            }

            foreach (var path in cardFiles)
            {
                if (await MoveIntoAsync(db, path, folder, existing))
                {
                    moved++;
                    await DropIfEmptyAsync(path[..(path.LastIndexOf('/') + 1)]);
                }
            }

            await DropIfEmptyAsync(boardFiles);
        }

        await SweepCardLeftoversAsync(db);
        return moved;
    }

    /// <summary>
    /// Old card files no row points at any more: a move whose copy and row update landed but whose delete failed. Nothing
    /// shows them (the card uses the copy), so they go. Only under logistics/{env}/cards/, where only card uploads wrote.
    /// </summary>
    private async Task SweepCardLeftoversAsync(ApplicationDbContext db)
    {
        var prefix = $"logistics/{_environment.EnvironmentName}/cards/";
        if (await _storage.ListTreeAsync(prefix) is not { Count: > 0 } objects)
        {
            return;
        }

        var used = (await db.LogisticsCardAttachments.Where(a => a.StoragePath != null && a.StoragePath.StartsWith(prefix))
            .Select(a => a.StoragePath!).ToListAsync()).ToHashSet(StringComparer.Ordinal);
        foreach (var path in objects.Select(o => o.FilePath).Where(p => !p.EndsWith('/') && !used.Contains(p)))
        {
            try
            {
                await _storage.DeleteDocumentAsync(path);
                await DropIfEmptyAsync(path[..(path.LastIndexOf('/') + 1)]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not remove the leftover logistics card file {Path}", path);
            }
        }
    }
}
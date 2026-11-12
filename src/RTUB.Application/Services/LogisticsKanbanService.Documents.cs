using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Logística files live in Documentação. Every board file and card attachment is one object at
/// docs/{env}/{fiscal year of the upload}/Logistics/{board folder}/{name}, shown in both places; deleting it on either
/// side removes it from both (Documentação also drops the card's attachment row).
/// - Board folder (<see cref="LogisticsBoard.LegacyFolder"/>): the board's sanitised name, unique among boards and among
///   Logistics folders no board owns (a deleted board's files, a name two old boards shared); else "Name 2", "Name 3".
/// - A rename moves the folder, in every fiscal year, to the new name; card rows follow their files.
/// - Same name in a folder: "name (2).ext", never an overwrite (card and board files share the folder).
/// - Deleting a board leaves its folder.
/// - Moves are recoverable. A folder move copies everything, then changes the rows and the folder in one save, then
///   deletes the sources; a failed copy changes nothing. A single file (startup) is copied, its rows repointed, its source
///   deleted; the next start reuses a copy already made (same name and size) and removes card-file leftovers.
/// - <see cref="MoveFilesIntoDocumentationAsync"/> (startup, idempotent) brings older files in: the boards' stable
///   folders (logistics/{env}/boards/{id}/files/), the card files (logistics/{env}/cards/...) and folders under an older
///   board name.
/// </summary>
public sealed partial class LogisticsKanbanService
{
    private static readonly Regex YearPattern = new(@"^\d{4}-\d{4}$", RegexOptions.Compiled);

    private string DocsPrefix => $"docs/{_environment.EnvironmentName}/";

    private string DocsFolder(string year, string folder) => $"{DocsPrefix}{year}/Logistics/{folder}/";

    private string CurrentYear => FiscalYearHelper.GetCurrentFiscalYearString(_time);

    /// <summary>The fiscal years of Documentação, newest first.</summary>
    private async Task<List<string>> DocsYearsAsync()
    {
        try
        {
            return (await _storage.ListSubfoldersAsync(DocsPrefix))
                .Where(y => YearPattern.IsMatch(y)).OrderByDescending(y => y, StringComparer.Ordinal).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not list the fiscal years of Documentação");
            return new();
        }
    }

    /// <summary>Every Logistics folder name in storage, in any year.</summary>
    private async Task<HashSet<string>> StoredFoldersAsync(IEnumerable<string> years)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var year in years)
        {
            try
            {
                found.UnionWith(await _storage.ListSubfoldersAsync($"{DocsPrefix}{year}/Logistics/"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not list the Logistics folders of {Year}", year);
            }
        }

        return found;
    }

    /// <summary>
    /// The folder a board should have: its sanitised name, else "Name 2", "Name 3"... Never another board's folder, nor a
    /// stored folder no board owns; <paramref name="current"/> (the board's own) is kept when the sequence reaches it.
    /// shortcut: two boards created at the same moment with the same name can get the same folder; a unique index would close it.
    /// </summary>
    private async Task<string> FolderForAsync(ApplicationDbContext db, int boardId, string name, string? current, HashSet<string>? stored = null)
    {
        var baseName = SanitizePathComponent(name);
        baseName = baseName.Length > 190 ? baseName[..190].TrimEnd() : baseName; // room for " 99" in the 200-character column
        var others = (await db.LogisticsBoards.Where(b => b.Id != boardId && b.LegacyFolder != null && b.LegacyFolder != "")
            .Select(b => b.LegacyFolder!).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        stored ??= await StoredFoldersAsync(await DocsYearsAsync());
        for (var n = 1; ; n++)
        {
            var candidate = n == 1 ? baseName : $"{baseName} {n}";
            if (string.Equals(candidate, current, StringComparison.OrdinalIgnoreCase))
            {
                return current!;
            }

            if (!others.Contains(candidate) && !stored.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>The board's folder, given one now if it has none (a board from before every board had one).</summary>
    private async Task<string> EnsureFolderAsync(LogisticsBoard board)
    {
        if (!string.IsNullOrEmpty(board.LegacyFolder))
        {
            return board.LegacyFolder;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var stored = await db.LogisticsBoards.FirstAsync(b => b.Id == board.Id);
        if (string.IsNullOrEmpty(stored.LegacyFolder))
        {
            stored.LegacyFolder = await FolderForAsync(db, stored.Id, stored.Name, null);
            await db.SaveChangesAsync();
        }

        return board.LegacyFolder = stored.LegacyFolder!;
    }

    /// <summary>
    /// <paramref name="name"/>, or "name (2).ext", "name (3).ext"... when the folder already holds it.
    /// shortcut: two uploads of the same name at the same moment can still meet; fine for a handful of managers.
    /// </summary>
    private async Task<string> FreeNameAsync(string folder, string name, ISet<string>? taken = null)
    {
        taken ??= (await ListAsync(folder, folder)).Select(d => d.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var free = name;
        for (var n = 2; taken.Contains(free); n++)
        {
            free = $"{Path.GetFileNameWithoutExtension(name)} ({n}){Path.GetExtension(name)}";
        }

        taken.Add(free);
        return free;
    }

    /// <summary>Stores an upload in the board's Documentação folder of the current fiscal year; returns its key.</summary>
    private async Task<string> StoreInDocumentationAsync(LogisticsBoard board, string name, Stream content, string contentType)
    {
        var folder = DocsFolder(CurrentYear, await EnsureFolderAsync(board));
        var key = await _storage.UploadDocumentAsync(folder, await FreeNameAsync(folder, name), content, contentType);
        _trees.Forget(key);
        return key;
    }

    /// <summary>
    /// Moves one object into <paramref name="folder"/>, recoverably: copy, point the card rows at the copy, then delete the
    /// source. Any failure leaves the source in place for the next run; a copy an interrupted run already made (same name,
    /// same size in <paramref name="existing"/>) is reused instead of copied again, so a retry never makes "name (2)".
    /// </summary>
    private async Task<bool> MoveIntoAsync(ApplicationDbContext db, string source, string folder, Dictionary<string, long> existing)
    {
        try
        {
            var name = Path.GetFileName(source);
            var size = await _storage.GetFileSizeAsync(source);
            string target;
            if (existing.TryGetValue(name, out var found) && found == size)
            {
                target = folder + name;
            }
            else
            {
                target = folder + await FreeNameAsync(folder, name, existing.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase));
                await _storage.CreateFolderAsync(folder);
                await _storage.CopyDocumentAsync(source, target);
                existing[Path.GetFileName(target)] = size;
            }

            foreach (var row in await db.LogisticsCardAttachments.Where(a => a.StoragePath == source).ToListAsync())
            {
                row.StoragePath = target;
                row.Name = Path.GetFileName(target);
            }

            await db.SaveChangesAsync();
            await _storage.DeleteDocumentAsync(source);
            _trees.Forget(target);
            _trees.Forget(source);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not move the logistics file {Path} into {Folder}; the next start tries again", source, folder);
            Revert(db);
            return false;
        }
    }

    /// <summary>Name -> size of the documents already in a folder.</summary>
    private async Task<Dictionary<string, long>> ExistingAsync(string folder, string label) =>
        (await ListAsync(folder, label)).GroupBy(d => d.FileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().SizeBytes, StringComparer.OrdinalIgnoreCase);

    /// <summary>Removes an emptied folder marker; best effort.</summary>
    private async Task DropIfEmptyAsync(string folder)
    {
        try
        {
            if (await _storage.DocumentExistsAsync(folder) && (await _storage.ListDocumentsInFolderAsync(folder)).Count == 0
                && (await _storage.ListSubfoldersAsync(folder)).Count == 0)
            {
                await _storage.DeleteFolderAsync(folder);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove the empty folder {Folder}", folder);
        }
    }

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

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

    /// <summary>Moves one object into <paramref name="folder"/> (free name) and points the card rows that use it there.</summary>
    private async Task<bool> MoveIntoAsync(ApplicationDbContext db, string source, string folder, ISet<string> taken)
    {
        try
        {
            var target = folder + await FreeNameAsync(folder, Path.GetFileName(source), taken);
            await _storage.CreateFolderAsync(folder);
            await _storage.MoveDocumentAsync(source, target);
            foreach (var row in await db.LogisticsCardAttachments.Where(a => a.StoragePath == source).ToListAsync())
            {
                row.StoragePath = target;
                row.Name = Path.GetFileName(target);
            }

            await db.SaveChangesAsync();
            _trees.Forget(target);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not move the logistics file {Path} into {Folder}", source, folder);
            return false;
        }
    }

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

    /// <summary>Moves a board's folder, in every fiscal year, from <paramref name="source"/> to <paramref name="target"/>.</summary>
    private async Task<int> MoveFolderAsync(ApplicationDbContext db, string source, string target, IReadOnlyList<string> years)
    {
        var moved = 0;
        foreach (var year in years)
        {
            var from = DocsFolder(year, source);
            var files = await ListAsync(from, source);
            if (files.Count == 0)
            {
                continue;
            }

            var to = DocsFolder(year, target);
            var taken = (await ListAsync(to, target)).Select(d => d.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                moved += await MoveIntoAsync(db, file.FilePath, to, taken) ? 1 : 0;
            }

            await DropIfEmptyAsync(from);
            _trees.Forget(from);
        }

        return moved;
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
        if (string.Equals(target, board.LegacyFolder, StringComparison.Ordinal))
        {
            return;
        }

        await MoveFolderAsync(db, board.LegacyFolder, target, years);
        board.LegacyFolder = target;
        await db.SaveChangesAsync();
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
            if (!string.IsNullOrEmpty(board.LegacyFolder) && !string.Equals(target, board.LegacyFolder, StringComparison.Ordinal))
            {
                moved += await MoveFolderAsync(db, board.LegacyFolder, target, years);
            }

            if (!string.Equals(target, board.LegacyFolder, StringComparison.Ordinal))
            {
                board.LegacyFolder = target;
                await db.SaveChangesAsync();
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

            var folder = DocsFolder(CurrentYear, target);
            var taken = (await ListAsync(folder, target)).Select(d => d.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var path in files)
            {
                moved += await MoveIntoAsync(db, path, folder, taken) ? 1 : 0;
            }

            foreach (var path in cardFiles)
            {
                if (await MoveIntoAsync(db, path, folder, taken))
                {
                    moved++;
                    await DropIfEmptyAsync(path[..(path.LastIndexOf('/') + 1)]);
                }
            }

            await DropIfEmptyAsync(boardFiles);
        }

        return moved;
    }
}

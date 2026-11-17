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
///   deleted; the next start reuses a copy already made (same name, size and ETag) and removes card-file leftovers.
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
    /// source. Any failure leaves the source in place for the next run. A copy an interrupted run already made is reused
    /// only when it is provably the same content (same name, size and ETag): a different file of that name, uploaded there
    /// meanwhile by a member, is never taken for it - the source then gets its own "name (2)" copy.
    /// </summary>
    private async Task<bool> MoveIntoAsync(ApplicationDbContext db, string source, string folder, Dictionary<string, long> existing)
    {
        try
        {
            var name = Path.GetFileName(source);
            var size = await _storage.GetFileSizeAsync(source);
            string target;
            if (existing.TryGetValue(name, out var found) && found == size
                && SameETag(await _storage.GetDocumentETagAsync(folder + name), await _storage.GetDocumentETagAsync(source)))
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

    /// <summary>Two ETags for the same content: quotes and hex case aside; a missing one never matches.</summary>
    public static bool SameETag(string? a, string? b) =>
        a?.Trim().Trim('"') is { Length: > 0 } x && b?.Trim().Trim('"') is { Length: > 0 } y && string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

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
}

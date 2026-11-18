// Documentacao: viewer rules, paths and tree filters.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class DocumentationService
{
    /// <summary>Who is asking, the year asked for (validated) and the folders of that year they may see.</summary>
    private async Task<(EventResult<bool>? Refusal, Viewer? Viewer)> ViewerAsync(string? fiscalYear, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } id)
        {
            return (EventResult<bool>.Fail(EventResultStatus.SignInRequired), null);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id) is not { } member)
        {
            return (EventResult<bool>.Fail(EventResultStatus.SignInRequired), null);
        }

        var isOwner = DocumentationAuthorization.IsOwner(user);
        if (!DocumentationAuthorization.CanOpen(member, isOwner))
        {
            return (EventResult<bool>.Fail(EventResultStatus.Forbidden), null);
        }

        var current = FiscalYearHelper.GetCurrentFiscalYearString();
        var years = (await _fiscalYears.GetAllFiscalYearsAsync())
            .Where(fy => fy.StartYear >= FiscalYearHelper.AppYearCreated)
            .Select(fy => fy.GetFiscalYearString())
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();
        var selected = string.IsNullOrEmpty(fiscalYear) ? current : fiscalYear;
        if (selected != current && !years.Contains(selected))
        {
            return (EventResult<bool>.Invalid("fiscalYear", "Ano inválido."), null);
        }

        // One listing for the whole year (shared for 30 s). A storage failure shows no folders and finds no document, as
        // the per-folder listings did, and is not kept.
        var tree = await _trees.GetAsync(_storage, YearPath(selected)) ?? [];
        var folders = new List<(string Name, string Label)>();
        foreach (var folder in Subfolders(tree, YearPath(selected)))
        {
            if (folder.Equals(Logistics, StringComparison.OrdinalIgnoreCase))
            {
                folders.AddRange(Subfolders(tree, $"{YearPath(selected)}{folder}/").Select(board => ($"{folder}/{board}", board)));
            }
            else
            {
                folders.Add((folder, folder));
            }
        }

        // A private board's per-year file folder is only for those who see the board (its access list,
        // Mod, Admin, Owner), here as on the board - listing, download and delete all go through the visible folders.
        // Fail closed: a folder is hidden if any private board the caller may not see could own it - by its resolved
        // LegacyFolder or by its current sanitised name (unresolved boards, and boards that share a folder name with
        // another board, whose LegacyFolder is ""), compared case-insensitively.
        // A Logistics folder no board owns (a deleted board's, a name two old boards shared, a folder a rename has
        // not finished moving) is for Mod, Admin and Owner only: it may hold a private board's files.
        var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string>? open = null;
        if (!LogisticsAuthorization.CanManage(user))
        {
            var boards = await db.LogisticsBoards.AsNoTracking()
                .Select(b => new { b.Name, b.LegacyFolder, Open = b.Visibility != BoardVisibility.Private || b.Members.Any(m => m.UserId == id) })
                .ToListAsync();
            open = boards.Where(b => b.Open && !string.IsNullOrEmpty(b.LegacyFolder))
                .Select(b => $"{Logistics}/{b.LegacyFolder}").ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var b in boards.Where(b => !b.Open))
            {
                foreach (var folder in new[] { b.LegacyFolder, LogisticsKanbanService.SanitizePathComponent(b.Name) }.Where(f => !string.IsNullOrEmpty(f)))
                {
                    hidden.Add($"{Logistics}/{folder}");
                }
            }
        }

        bool Visible(string folder) => !folder.StartsWith($"{Logistics}/", StringComparison.OrdinalIgnoreCase)
            || open is null || (open.Contains(folder) && !hidden.Contains(folder));

        return (null, new Viewer(isOwner, DocumentationAuthorization.CanManage(user), years, selected,
            folders.Where(f => DocumentationAuthorization.CanSeeFolder(member, isOwner, f.Name) && Visible(f.Name)).ToList(), tree));
    }

    private static string? Folder(Viewer viewer, string? folder) =>
        viewer.Folders.Select(f => f.Name).FirstOrDefault(f => f == folder);

    /// <summary>
    /// The key of a document the caller may download or delete: the folder must be one of the caller's visible folders and
    /// the name a document listed directly in it, so the key is always one storage itself listed - never built from input.
    /// </summary>
    private string? DocumentPath(Viewer viewer, string? folder, string? name) =>
        Folder(viewer, folder) is { } found
            ? Documents(viewer.Tree, FolderPath(viewer.FiscalYear, found)).FirstOrDefault(d => d.FileName == name)?.FilePath
            : null;

    /// <summary>The direct subfolders of a prefix in the listing, as the delimiter listing named them (key order).</summary>
    internal static List<string> Subfolders(IReadOnlyList<DocumentMetadata> tree, string prefix) =>
        tree.Select(o => o.FilePath)
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k => k.IndexOf('/', prefix.Length) is var end && end > prefix.Length ? k[prefix.Length..end] : null)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(f => f + "/")
            .ToList();

    /// <summary>The documents directly in a folder (no folder marker, nothing in a subfolder), by file name.</summary>
    internal static List<DocumentMetadata> Documents(IReadOnlyList<DocumentMetadata> tree, string folderPath)
    {
        var prefix = folderPath.EndsWith('/') ? folderPath : folderPath + "/";
        return tree.Where(o => o.FilePath.StartsWith(prefix, StringComparison.Ordinal)
                && o.FilePath.Length > prefix.Length && o.FilePath.IndexOf('/', prefix.Length) < 0)
            .OrderBy(o => o.FileName)
            .ToList();
    }

    private string YearPath(string fiscalYear) => $"docs/{_environment.EnvironmentName}/{fiscalYear}/";

    private string FolderPath(string fiscalYear, string folder) => $"{YearPath(fiscalYear)}{folder}";

    private void SetAudit(ClaimsPrincipal user) => _audit.SetUser(user.Identity?.Name, user.UserId());
}
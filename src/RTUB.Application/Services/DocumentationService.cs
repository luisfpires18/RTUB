using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /documentation ("Documentação") behind the React /documentation (React track 022,
/// docs/react-documentation.md). Same storage and rules, through <see cref="IDocumentStorageService"/>, now enforced
/// server-side (<see cref="DocumentationAuthorization"/>; the old page filtered and hid buttons in the circuit):
/// - folders of one fiscal year (the current one by default) under docs/{environment}/{year}/, in storage order, the
///   Logistics folder shown as one folder per board; documents by file name;
/// - download: a pre-signed attachment URL, only for a document listed in a folder the caller sees;
/// - create folder (Owner): letters, digits, spaces and hyphens, at most 50, in the current fiscal year, not existing;
/// - upload (any member who sees the folder): PDF, TXT, DOC(X), XLS(X), CSV, PPT(X), at most 50 MB; a name already in
///   the folder replaces that document, for Owner only;
/// - delete document / folder (Owner): removed from storage (the folder with everything in it), as before.
/// Every key is rebuilt from a listed year, folder and name, never from raw input. Audit log rows as before (the
/// storage service writes them). No notifications, no schema change.
/// </summary>
public sealed class DocumentationService : IDocumentationService
{
    public const long MaxFileBytes = 50 * 1024 * 1024;
    public const int MaxFolderName = 50;
    public static readonly IReadOnlyList<string> Extensions = new[] { ".pdf", ".txt", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".ppt", ".pptx" };
    private static readonly Regex FolderPattern = new(@"^[a-zA-Z0-9\s\-]+$");
    private const string Logistics = "Logistics";

    private readonly IDocumentStorageService _storage;
    private readonly IFiscalYearService _fiscalYears;
    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IHostEnvironment _environment;
    private readonly AuditContext _audit;

    public DocumentationService(IDocumentStorageService storage, IFiscalYearService fiscalYears, IDbContextFactory<ApplicationDbContext> contexts,
        IHostEnvironment environment, AuditContext audit)
    {
        _storage = storage;
        _fiscalYears = fiscalYears;
        _contexts = contexts;
        _environment = environment;
        _audit = audit;
    }

    private sealed record Viewer(bool IsOwner, IReadOnlyList<string> Years, string FiscalYear, IReadOnlyList<(string Name, string Label)> Folders);

    public async Task<EventResult<DocumentationDto>> GetAsync(string? fiscalYear, ClaimsPrincipal user)
    {
        var (refusal, viewer) = await ViewerAsync(fiscalYear, user);
        if (viewer is null)
        {
            return new EventResult<DocumentationDto>(refusal!.Status, Errors: refusal.Errors);
        }

        var current = FiscalYearHelper.GetCurrentFiscalYearString();
        var folders = await Task.WhenAll(viewer.Folders.Select(async f =>
            new DocumentFolderDto(f.Name, f.Label, (await _storage.ListDocumentsInFolderAsync(FolderPath(viewer.FiscalYear, f.Name)))
                .Select(d => new DocumentFileDto(d.FileName, d.Extension, d.SizeBytes)).ToList())));

        return EventResult<DocumentationDto>.Ok(new DocumentationDto(
            viewer.Years.Select(y => new MemberOptionDto(y, y == current ? $"{y} (ATUAL)" : y)).ToList(),
            viewer.FiscalYear,
            folders,
            Extensions,
            MaxFileBytes,
            viewer.IsOwner));
    }

    public async Task<EventResult<DocumentLinkDto>> GetDownloadAsync(string? fiscalYear, string? folder, string? name, ClaimsPrincipal user)
    {
        var (refusal, viewer) = await ViewerAsync(fiscalYear, user);
        if (viewer is null)
        {
            return new EventResult<DocumentLinkDto>(refusal!.Status, Errors: refusal.Errors);
        }

        if (await DocumentAsync(viewer, folder, name) is not { } path)
        {
            return EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
        }

        return await _storage.GetDocumentUrlAsync(path, forceDownload: true) is { } url
            ? EventResult<DocumentLinkDto>.Ok(new DocumentLinkDto(url))
            : EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<DocumentFolderDto>> CreateFolderAsync(DocumentFolderInput input, ClaimsPrincipal user)
    {
        // Folders are always created in the current fiscal year, whatever year the page shows.
        var (refusal, viewer) = await ViewerAsync(null, user);
        if (viewer is null)
        {
            return new EventResult<DocumentFolderDto>(refusal!.Status, Errors: refusal.Errors);
        }

        if (!viewer.IsOwner)
        {
            return EventResult<DocumentFolderDto>.Fail(EventResultStatus.Forbidden);
        }

        var name = input.Name?.Trim() ?? "";
        if (name == "")
        {
            return EventResult<DocumentFolderDto>.Invalid("name", "O nome da pasta não pode estar vazio.");
        }

        if (name.Length > MaxFolderName || !FolderPattern.IsMatch(name))
        {
            return EventResult<DocumentFolderDto>.Invalid("name",
                $"O nome da pasta só pode conter letras, números, espaços e hífens (máx. {MaxFolderName} caracteres).");
        }

        if ((await _storage.ListSubfoldersAsync(YearPath(viewer.FiscalYear))).Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return EventResult<DocumentFolderDto>.Invalid("name", "Uma pasta com este nome já existe.");
        }

        SetAudit(user);
        await _storage.CreateFolderAsync(FolderPath(viewer.FiscalYear, name));
        return EventResult<DocumentFolderDto>.Ok(new DocumentFolderDto(name, name, Array.Empty<DocumentFileDto>()));
    }

    public async Task<EventResult<bool>> DeleteFolderAsync(string? fiscalYear, string? folder, ClaimsPrincipal user)
    {
        var (refusal, viewer) = await ViewerAsync(fiscalYear, user);
        if (viewer is null)
        {
            return new EventResult<bool>(refusal!.Status, Errors: refusal.Errors);
        }

        if (!viewer.IsOwner)
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        if (Folder(viewer, folder) is not { } found)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        SetAudit(user);
        await _storage.DeleteFolderAsync(FolderPath(viewer.FiscalYear, found));
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<DocumentFileDto>> UploadAsync(string? fiscalYear, string? folder, DocumentUpload upload, ClaimsPrincipal user)
    {
        var (refusal, viewer) = await ViewerAsync(fiscalYear, user);
        if (viewer is null)
        {
            return new EventResult<DocumentFileDto>(refusal!.Status, Errors: refusal.Errors);
        }

        if (Folder(viewer, folder) is not { } found)
        {
            return EventResult<DocumentFileDto>.Fail(EventResultStatus.NotFound);
        }

        // The browser's name without any path: a document never lands outside its folder.
        var name = upload.FileName.Split('/', '\\')[^1].Trim();
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (name == "" || name.StartsWith('.') || !Extensions.Contains(extension))
        {
            return EventResult<DocumentFileDto>.Invalid("file", $"Tipo de ficheiro não permitido. Tipos permitidos: {string.Join(", ", Extensions)}");
        }

        if (upload.Length <= 0 || upload.Length > MaxFileBytes)
        {
            return EventResult<DocumentFileDto>.Invalid("file", "O ficheiro tem de ter até 50 MB.");
        }

        var path = FolderPath(viewer.FiscalYear, found);
        if (!viewer.IsOwner && (await _storage.ListDocumentsInFolderAsync(path)).Any(d => d.FileName == name))
        {
            return EventResult<DocumentFileDto>.Invalid("file", "Já existe um documento com este nome nesta pasta.");
        }

        SetAudit(user);
        await _storage.UploadDocumentAsync(path, name, upload.Content,
            string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType);
        return EventResult<DocumentFileDto>.Ok(new DocumentFileDto(name, Path.GetExtension(name), upload.Length));
    }

    public async Task<EventResult<bool>> DeleteDocumentAsync(string? fiscalYear, string? folder, string? name, ClaimsPrincipal user)
    {
        var (refusal, viewer) = await ViewerAsync(fiscalYear, user);
        if (viewer is null)
        {
            return new EventResult<bool>(refusal!.Status, Errors: refusal.Errors);
        }

        if (!viewer.IsOwner)
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        if (await DocumentAsync(viewer, folder, name) is not { } path)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        SetAudit(user);
        await _storage.DeleteDocumentAsync(path);
        return EventResult<bool>.Ok(true);
    }

    /// <summary>Who is asking, the year asked for (validated) and the folders of that year they may see.</summary>
    private async Task<(EventResult<bool>? Refusal, Viewer? Viewer)> ViewerAsync(string? fiscalYear, ClaimsPrincipal user)
    {
        if (DocumentationAuthorization.UserId(user) is not { } id)
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

        var folders = new List<(string Name, string Label)>();
        foreach (var folder in await _storage.ListSubfoldersAsync(YearPath(selected)))
        {
            if (folder.Equals(Logistics, StringComparison.OrdinalIgnoreCase))
            {
                folders.AddRange((await _storage.ListSubfoldersAsync($"{YearPath(selected)}{folder}/")).Select(board => ($"{folder}/{board}", board)));
            }
            else
            {
                folders.Add((folder, folder));
            }
        }

        return (null, new Viewer(isOwner, years, selected,
            folders.Where(f => DocumentationAuthorization.CanSeeFolder(member, isOwner, f.Name)).ToList()));
    }

    private static string? Folder(Viewer viewer, string? folder) =>
        viewer.Folders.Select(f => f.Name).FirstOrDefault(f => f == folder);

    private async Task<string?> DocumentAsync(Viewer viewer, string? folder, string? name) =>
        Folder(viewer, folder) is { } found
            ? (await _storage.ListDocumentsInFolderAsync(FolderPath(viewer.FiscalYear, found))).FirstOrDefault(d => d.FileName == name)?.FilePath
            : null;

    private string YearPath(string fiscalYear) => $"docs/{_environment.EnvironmentName}/{fiscalYear}/";

    private string FolderPath(string fiscalYear, string folder) => $"{YearPath(fiscalYear)}{folder}";

    private void SetAudit(ClaimsPrincipal user) => _audit.SetUser(user.Identity?.Name, DocumentationAuthorization.UserId(user));
}

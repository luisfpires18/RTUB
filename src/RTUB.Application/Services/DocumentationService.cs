using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// The React /documentation (React track 022,
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
/// #226 F01: a year's folders, boards and documents all come from ONE storage listing of docs/{environment}/{year}/
/// (<see cref="DocumentTreeCache"/>, kept 30 s and dropped on every write), instead of the year listing, then the
/// Logistics listing, then one listing per folder - and the download no longer lists three times before signing.
/// </summary>
public sealed partial class DocumentationService : IDocumentationService
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

    private readonly DocumentTreeCache _trees;

    public DocumentationService(IDocumentStorageService storage, IFiscalYearService fiscalYears, IDbContextFactory<ApplicationDbContext> contexts,
        IHostEnvironment environment, AuditContext audit, DocumentTreeCache trees)
    {
        _storage = storage;
        _fiscalYears = fiscalYears;
        _contexts = contexts;
        _environment = environment;
        _audit = audit;
        _trees = trees;
    }

    /// <summary>The caller, the validated year, the folders of that year they may see, and that year's storage listing.</summary>
    private sealed record Viewer(bool IsOwner, bool CanManage, IReadOnlyList<string> Years, string FiscalYear, IReadOnlyList<(string Name, string Label)> Folders,
        IReadOnlyList<DocumentMetadata> Tree);

    public async Task<EventResult<DocumentationDto>> GetAsync(string? fiscalYear, ClaimsPrincipal user)
    {
        var (refusal, viewer) = await ViewerAsync(fiscalYear, user);
        if (viewer is null)
        {
            return new EventResult<DocumentationDto>(refusal!.Status, Errors: refusal.Errors);
        }

        var current = FiscalYearHelper.GetCurrentFiscalYearString();
        var folders = viewer.Folders.Select(f =>
            new DocumentFolderDto(f.Name, f.Label, Documents(viewer.Tree, FolderPath(viewer.FiscalYear, f.Name))
                .Select(d => new DocumentFileDto(d.FileName, d.Extension, d.SizeBytes)).ToList())).ToList();

        return EventResult<DocumentationDto>.Ok(new DocumentationDto(
            viewer.Years.Select(y => new MemberOptionDto(y, y == current ? $"{y} (ATUAL)" : y)).ToList(),
            viewer.FiscalYear,
            folders,
            Extensions,
            MaxFileBytes,
            viewer.CanManage));
    }

    public async Task<EventResult<DocumentLinkDto>> GetDownloadAsync(string? fiscalYear, string? folder, string? name, ClaimsPrincipal user)
    {
        var (refusal, viewer) = await ViewerAsync(fiscalYear, user);
        if (viewer is null)
        {
            return new EventResult<DocumentLinkDto>(refusal!.Status, Errors: refusal.Errors);
        }

        if (DocumentPath(viewer, folder, name) is not { } path)
        {
            return EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
        }

        return await _storage.GetDocumentUrlAsync(path, forceDownload: true) is { } url
            ? EventResult<DocumentLinkDto>.Ok(new DocumentLinkDto(url))
            : EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
    }
}

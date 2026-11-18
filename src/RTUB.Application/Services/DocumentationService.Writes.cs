// Documentacao: create and delete folders, upload and delete documents.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class DocumentationService
{
    public async Task<EventResult<DocumentFolderDto>> CreateFolderAsync(DocumentFolderInput input, ClaimsPrincipal user)
    {
        // Folders are always created in the current fiscal year, whatever year the page shows.
        var (refusal, viewer) = await ViewerAsync(null, user);
        if (viewer is null)
        {
            return new EventResult<DocumentFolderDto>(refusal!.Status, Errors: refusal.Errors);
        }

        if (!viewer.CanManage)
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

        if (Subfolders(viewer.Tree, YearPath(viewer.FiscalYear)).Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return EventResult<DocumentFolderDto>.Invalid("name", "Uma pasta com este nome já existe.");
        }

        SetAudit(user);
        await _storage.CreateFolderAsync(FolderPath(viewer.FiscalYear, name));
        _trees.Forget(YearPath(viewer.FiscalYear));
        return EventResult<DocumentFolderDto>.Ok(new DocumentFolderDto(name, name, Array.Empty<DocumentFileDto>()));
    }

    public async Task<EventResult<bool>> DeleteFolderAsync(string? fiscalYear, string? folder, ClaimsPrincipal user)
    {
        var (refusal, viewer) = await ViewerAsync(fiscalYear, user);
        if (viewer is null)
        {
            return new EventResult<bool>(refusal!.Status, Errors: refusal.Errors);
        }

        if (!viewer.CanManage)
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        if (Folder(viewer, folder) is not { } found)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        SetAudit(user);
        var path = FolderPath(viewer.FiscalYear, found);
        await DropCardFilesAsync(user, path.EndsWith('/') ? path : path + "/", folder: true); // rows first, as for a document
        await _storage.DeleteFolderAsync(path);
        _trees.Forget(YearPath(viewer.FiscalYear));
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
        if (!viewer.CanManage && Documents(viewer.Tree, path).Any(d => d.FileName == name))
        {
            return EventResult<DocumentFileDto>.Invalid("file", "Já existe um documento com este nome nesta pasta.");
        }

        SetAudit(user);
        await _storage.UploadDocumentAsync(path, name, upload.Content,
            string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType);
        _trees.Forget(YearPath(viewer.FiscalYear));
        return EventResult<DocumentFileDto>.Ok(new DocumentFileDto(name, Path.GetExtension(name), upload.Length));
    }

    public async Task<EventResult<bool>> DeleteDocumentAsync(string? fiscalYear, string? folder, string? name, ClaimsPrincipal user)
    {
        var (refusal, viewer) = await ViewerAsync(fiscalYear, user);
        if (viewer is null)
        {
            return new EventResult<bool>(refusal!.Status, Errors: refusal.Errors);
        }

        if (!viewer.CanManage)
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        if (DocumentPath(viewer, folder, name) is not { } path)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        // The card rows go first: a storage failure then leaves a document no card points at (delete again), never a card
        // pointing at a deleted file; a failed save deletes nothing.
        SetAudit(user);
        await DropCardFilesAsync(user, path, folder: false);
        await _storage.DeleteDocumentAsync(path);
        _trees.Forget(YearPath(viewer.FiscalYear));
        return EventResult<bool>.Ok(true);
    }

    /// <summary>
    /// A Logística card file is the same object as its Documentação document (docs/react-logistics.md → Files): deleting it
    /// here removes it from its card too, with a line in the card's history.
    /// </summary>
    private async Task DropCardFilesAsync(ClaimsPrincipal user, string path, bool folder)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var rows = await (folder
            ? db.LogisticsCardAttachments.Where(a => a.StoragePath != null && a.StoragePath.StartsWith(path))
            : db.LogisticsCardAttachments.Where(a => a.StoragePath == path)).ToListAsync();
        if (rows.Count == 0)
        {
            return;
        }

        var actor = DocumentationAuthorization.UserId(user);
        foreach (var row in rows)
        {
            db.LogisticsCardAttachments.Remove(row);
            db.LogisticsCardActivities.Add(new LogisticsCardActivity
            {
                CardId = row.CardId, ActorId = actor, Action = "attachment", Detail = $"Anexo removido em Documentação: {row.Name}",
            });
        }

        await db.SaveChangesAsync();
    }
}
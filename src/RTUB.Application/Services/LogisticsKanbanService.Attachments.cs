// Logistics: card attachments and links.
using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class LogisticsKanbanService
{
    /// <summary>Owner, collaborators and managers attach: the board files' types and 10 MB limit; stored in the board's Documentação folder.</summary>
    public async Task<EventResult<LogisticsCardDetailDto>> UploadAttachmentAsync(int cardId, LogisticsFileUpload upload, ClaimsPrincipal user)
    {
        var name = SafeFileName(upload.FileName);
        if (upload.Length <= 0 || upload.Length > MaxFileBytes)
        {
            return EventResult<LogisticsCardDetailDto>.Invalid("file", "O ficheiro é demasiado grande. Tamanho máximo: 10MB");
        }

        if (name is null || !FileTypes.Contains(upload.ContentType))
        {
            return EventResult<LogisticsCardDetailDto>.Invalid("file", "Tipo de ficheiro não suportado. Formatos permitidos: PDF, Word, Excel, TXT, PNG, JPG");
        }

        // Checked first (no upload for a refused caller), then the row is written in the locked change; the object is
        // removed again if that change is refused meanwhile.
        var (refused, db, card, access) = await OpenCardAsync<LogisticsCardDetailDto>(cardId, user);
        if (refused is not null)
        {
            return refused;
        }

        await db!.DisposeAsync();
        if (!access!.CanWork)
        {
            return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
        }

        if (access.ReadOnly)
        {
            return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Closed);
        }

        _audit.SetUser(user.Identity?.Name, access.UserId);
        var path = await StoreInDocumentationAsync(access.Board, name, upload.Content, upload.ContentType);
        name = Path.GetFileName(path);
        var result = await ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, c, a) =>
        {
            if (await db.LogisticsCardAttachments.CountAsync(x => x.CardId == c.Id) >= MaxAttachments)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("file", $"No máximo {MaxAttachments} anexos por cartão.");
            }

            db.LogisticsCardAttachments.Add(new LogisticsCardAttachment
            {
                CardId = c.Id, Kind = CardAttachmentKind.File, Name = name, StoragePath = path, ContentType = upload.ContentType,
                SizeBytes = upload.Length, UploadedById = a.UserId,
            });
            Log(db, c.Id, a, "attachment", $"Ficheiro anexado: {Cut(name)}");
            return null;
        });

        if (result.Status != EventResultStatus.Ok)
        {
            await DeleteStoredAsync(new List<string> { path });
        }

        return result;
    }

    public Task<EventResult<LogisticsCardDetailDto>> AddLinkAsync(int cardId, LogisticsLinkInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, card, access) =>
        {
            var raw = input.Url?.Trim() ?? "";
            if (raw.Length > 2000 || raw.Any(char.IsWhiteSpace) || !Uri.TryCreate(raw, UriKind.Absolute, out var url)
                || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp) || string.IsNullOrEmpty(url.Host))
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("url", "Indique um endereço http:// ou https:// válido.");
            }

            var name = input.Name?.Trim() is { Length: > 0 } given ? given : url.Host;
            if (name.Length > 200)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("name", "O nome não pode exceder 200 caracteres.");
            }

            if (await db.LogisticsCardAttachments.CountAsync(x => x.CardId == card.Id) >= MaxAttachments)
            {
                return EventResult<LogisticsCardDetailDto>.Invalid("url", $"No máximo {MaxAttachments} anexos por cartão.");
            }

            db.LogisticsCardAttachments.Add(new LogisticsCardAttachment
            {
                CardId = card.Id, Kind = CardAttachmentKind.Link, Name = name, Url = url.AbsoluteUri, UploadedById = access.UserId,
            });
            Log(db, card.Id, access, "attachment", $"Ligação adicionada: {Cut(name)}");
            return null;
        });

    /// <summary>Any viewer: a file's pre-signed download, or the link's address. The attachment must belong to this card.</summary>
    public async Task<EventResult<DocumentLinkDto>> GetAttachmentAsync(int cardId, int attachmentId, ClaimsPrincipal user)
    {
        var (refused, db, _, _) = await OpenCardAsync<DocumentLinkDto>(cardId, user);
        if (refused is not null)
        {
            return refused;
        }

        await using (db)
        {
            if (await db!.LogisticsCardAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId && a.CardId == cardId) is not { } attachment)
            {
                return EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
            }

            if (attachment.Kind == CardAttachmentKind.Link)
            {
                return EventResult<DocumentLinkDto>.Ok(new DocumentLinkDto(attachment.Url!));
            }

            return await _storage.GetDocumentUrlAsync(attachment.StoragePath!, forceDownload: true) is { } url
                ? EventResult<DocumentLinkDto>.Ok(new DocumentLinkDto(url))
                : EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
        }
    }

    /// <summary>
    /// A manager, or the uploader while they still work on the card (owner or collaborator): a former collaborator only
    /// reads. Checked in the same locked change as the delete (<see cref="ChangeCardAsync"/>), so an assignment removed
    /// meanwhile refuses it. The stored file goes after the row.
    /// </summary>
    public async Task<EventResult<LogisticsCardDetailDto>> DeleteAttachmentAsync(int cardId, int attachmentId, ClaimsPrincipal user)
    {
        string? stored = null;
        var result = await ChangeCardAsync(cardId, user, assignedMayChange: true, async (db, card, access) =>
        {
            if (await db.LogisticsCardAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.CardId == card.Id) is not { } attachment)
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.NotFound);
            }

            if (!access.Manager && (attachment.UploadedById is null || attachment.UploadedById != access.UserId))
            {
                return EventResult<LogisticsCardDetailDto>.Fail(EventResultStatus.Forbidden);
            }

            stored = attachment.StoragePath;
            db.LogisticsCardAttachments.Remove(attachment);
            Log(db, card.Id, access, "attachment", $"Anexo removido: {Cut(attachment.Name)}");
            return null;
        });

        if (result.Status == EventResultStatus.Ok && stored is not null)
        {
            _audit.SetUser(user.Identity?.Name, LogisticsAuthorization.UserId(user));
            await DeleteStoredAsync(new List<string> { stored });
        }

        return result;
    }

    private static async Task<List<string>> AttachmentPathsAsync(ApplicationDbContext db, Expression<Func<LogisticsCard, bool>> cards)
    {
        var ids = db.LogisticsCards.Where(cards).Select(c => c.Id);
        return await db.LogisticsCardAttachments.Where(a => ids.Contains(a.CardId) && a.StoragePath != null).Select(a => a.StoragePath!).ToListAsync();
    }

    /// <summary>Best-effort, after the rows are gone: a leftover object is only storage, never a broken card.</summary>
    private async Task DeleteStoredAsync(List<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                await _storage.DeleteDocumentAsync(path);
                _trees.Forget(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete the logistics card file {Path}", path);
            }
        }
    }

    // ---------------------------------------------------------------- the details
}
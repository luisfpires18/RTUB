// Logistics: board files kept in the Documentacao folder.
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class LogisticsKanbanService
{
    /// <summary>
    /// The board's files: its Documentação folder in every fiscal year, newest first, without its cards' attachments (those
    /// show on their card). Ids are opaque: "f:name" this fiscal year, "l:year/name" an older one - the key is always rebuilt
    /// from the board, never taken from input.
    /// </summary>
    private async Task<List<(LogisticsFileDto Dto, string Path)>> BoardFileEntriesAsync(LogisticsBoard board)
    {
        var entries = new List<(LogisticsFileDto, string)>();
        if (string.IsNullOrEmpty(board.LegacyFolder))
        {
            return entries;
        }

        HashSet<string> cardFiles;
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            cardFiles = (await AttachmentPathsAsync(db, c => c.List.BoardId == board.Id)).ToHashSet(StringComparer.Ordinal);
        }

        var current = CurrentYear;
        foreach (var year in await DocsYearsAsync())
        {
            foreach (var d in await ListAsync(DocsFolder(year, board.LegacyFolder), board.Name))
            {
                if (!cardFiles.Contains(d.FilePath))
                {
                    entries.Add(year == current
                        ? (new LogisticsFileDto(d.FileName, d.Extension, d.SizeBytes, $"f:{d.FileName}", null), d.FilePath)
                        : (new LogisticsFileDto(d.FileName, d.Extension, d.SizeBytes, $"l:{year}/{d.FileName}", year), d.FilePath));
                }
            }
        }

        return entries;
    }

    private async Task<List<LogisticsFileDto>> BoardFilesAsync(LogisticsBoard board) => (await BoardFileEntriesAsync(board)).Select(e => e.Dto).ToList();

    private async Task<List<DocumentMetadata>> ListAsync(string folder, string boardName)
    {
        try
        {
            return await _storage.ListDocumentsInFolderAsync(folder);
        }
        catch (Exception ex)
        {
            // The old page treated the files as "not critical" too: the board still opens.
            _logger.LogWarning(ex, "Could not list the files of logistics board {Board}", boardName);
            return new();
        }
    }

    public async Task<EventResult<IReadOnlyList<LogisticsFileDto>>> GetFilesAsync(int boardId, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeBoardAsync<IReadOnlyList<LogisticsFileDto>>(db, boardId, user);
        return refused ?? EventResult<IReadOnlyList<LogisticsFileDto>>.Ok(await BoardFilesAsync(board!));
    }

    public async Task<EventResult<LogisticsFileDto>> UploadFileAsync(int boardId, LogisticsFileUpload upload, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeBoardAsync<LogisticsFileDto>(db, boardId, user, manage: true, writable: true);
        if (refused is not null)
        {
            return refused;
        }

        var name = SafeFileName(upload.FileName);
        if (upload.Length <= 0 || upload.Length > MaxFileBytes)
        {
            return EventResult<LogisticsFileDto>.Invalid("file", "O ficheiro é demasiado grande. Tamanho máximo: 10MB");
        }

        if (name is null || !FileTypes.Contains(upload.ContentType))
        {
            return EventResult<LogisticsFileDto>.Invalid("file", "Tipo de ficheiro não suportado. Formatos permitidos: PDF, Word, Excel, TXT, PNG, JPG");
        }

        _audit.SetUser(user.Identity?.Name, LogisticsAuthorization.UserId(user));
        var stored = Path.GetFileName(await StoreInDocumentationAsync(board!, name, upload.Content, upload.ContentType));
        return EventResult<LogisticsFileDto>.Ok(new LogisticsFileDto(stored, Path.GetExtension(stored), upload.Length, $"f:{stored}", null));
    }

    public async Task<EventResult<DocumentLinkDto>> GetFileAsync(int boardId, string? fileId, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeBoardAsync<DocumentLinkDto>(db, boardId, user);
        if (refused is not null)
        {
            return refused;
        }

        if (await FilePathAsync(board!, fileId) is not { } path)
        {
            return EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
        }

        return await _storage.GetDocumentUrlAsync(path, forceDownload: true) is { } url
            ? EventResult<DocumentLinkDto>.Ok(new DocumentLinkDto(url))
            : EventResult<DocumentLinkDto>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<bool>> DeleteFileAsync(int boardId, string? fileId, ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var (refused, board) = await SeeBoardAsync<bool>(db, boardId, user, manage: true, writable: true);
        if (refused is not null)
        {
            return refused;
        }

        if (await FilePathAsync(board!, fileId) is not { } path)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        _audit.SetUser(user.Identity?.Name, LogisticsAuthorization.UserId(user));
        await _storage.DeleteDocumentAsync(path);
        _trees.Forget(path); // the same document in Documentação
        return EventResult<bool>.Ok(true);
    }

    /// <summary>
    /// The key of one of the board's listed files, by its id - or, for older pages, by its bare name (newest year first).
    /// </summary>
    private async Task<string?> FilePathAsync(LogisticsBoard board, string? fileId)
    {
        if (string.IsNullOrEmpty(fileId))
        {
            return null;
        }

        var entries = await BoardFileEntriesAsync(board);
        return (entries.FirstOrDefault(e => e.Dto.Id == fileId).Path)
            ?? entries.FirstOrDefault(e => e.Dto.Name == fileId).Path;
    }

    // ---------------------------------------------------------------- helpers
}
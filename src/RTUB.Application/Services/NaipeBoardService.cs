using System.Security.Claims;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The React /naipes and /naipes/config pages (docs/react-naipes.md). Same data and
/// behaviour through <see cref="INaipeService"/> (which keeps the R2 storage, the audit log and the push to everyone on
/// a new item), rules now enforced here (<see cref="NaipesAuthorization"/>):
/// - the picker lists the visible instruments in their display order (the settings are created on first use);
/// - the chosen instrument's items, searched over title and description, by display order then creation
///   (<see cref="INaipeContentFilterService"/>);
/// - add: a video (MP4, MOV, WebM or 3GP, ≤100 MB) or an image (JPEG, PNG, WebP or GIF, ≤10 MB), each checked by its
///   bytes and stored with the type the server decided; a title (≤200; blank gets the "Guitarra_Video_3" default), an
///   optional description (≤1000) and a display order (0.1-999.9); edit (title, description, order) and delete: the
///   member who added it, or Admin / Owner;
/// - comments (≤1000), newest first; delete: the author, or Admin / Owner; one play counted per video opening;
/// - settings (Admin / Owner): picture (JPEG, PNG or WebP, ≤5 MB), visibility and display order (0-999).
/// No schema change.
/// </summary>
public sealed partial class NaipeBoardService : INaipeBoardService
{
    public const long MaxVideoBytes = 100 * 1024 * 1024;

    public const long MaxImageBytes = 10 * 1024 * 1024;

    public const long MaxPictureBytes = 5 * 1024 * 1024;

    public const int MaxTitleLength = 200;

    public const int MaxDescriptionLength = 1000;

    public const int MaxCommentLength = 1000;

    public const decimal MinSortOrder = 0.1m;

    public const decimal MaxSortOrder = 999.9m;

    public const int MaxTypeSortOrder = 999;

    // The stored MIME type is decided here, from the extension (videos) or the bytes (images), never copied from the
    // browser's header: an upload is played or shown from the media store as that type.
    private static readonly Dictionary<string, string> VideoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = "video/mp4", [".m4v"] = "video/mp4", [".mov"] = "video/quicktime", [".webm"] = "video/webm", [".3gp"] = "video/3gpp",
    };

    private static readonly Dictionary<string, string> VideoTypesByHeader = new(StringComparer.OrdinalIgnoreCase)
    {
        ["video/mp4"] = "video/mp4", ["video/x-m4v"] = "video/mp4", ["video/quicktime"] = "video/quicktime", ["video/webm"] = "video/webm",
        ["video/3gpp"] = "video/3gpp",
    };

    private static readonly string[] PictureTypes = { "image/jpeg", "image/png", "image/webp" };

    private readonly INaipeService _naipes;

    private readonly INaipeContentFilterService _filter;

    public NaipeBoardService(INaipeService naipes, INaipeContentFilterService filter)
    {
        _naipes = naipes;
        _filter = filter;
    }

    public async Task<EventResult<NaipeBoardDto>> GetAsync(string? instrument, string? search, ClaimsPrincipal user)
    {
        if (!user.IsMember())
        {
            return EventResult<NaipeBoardDto>.Fail(EventResultStatus.SignInRequired);
        }

        InstrumentType? selected = null;
        if (!string.IsNullOrWhiteSpace(instrument))
        {
            if (!TryInstrument(instrument, out var type))
            {
                return EventResult<NaipeBoardDto>.Invalid("instrument", "Instrumento inválido.");
            }

            selected = type;
        }

        await _naipes.InitializeTypeConfigsAsync();
        var instruments = (await _naipes.GetVisibleTypeConfigsAsync())
            .Select(c => new NaipeInstrumentDto(c.InstrumentType.ToString(), c.InstrumentTypeName, c.PictureUrl))
            .ToList();
        if (selected is { } picked && instruments.All(i => i.Value != picked.ToString()))
        {
            selected = null; // a hidden instrument is off the board, never offered
        }

        var all = await _naipes.GetAllContentAsync();
        var forInstrument = _filter.FilterContent(all, selected, string.Empty);
        var shown = _filter.FilterContent(all, selected, search?.Trim() ?? string.Empty);

        return EventResult<NaipeBoardDto>.Ok(new NaipeBoardDto(
            instruments,
            selected?.ToString(),
            shown.Select(c => Item(c, user)).ToList(),
            forInstrument.Count,
            Enum.GetValues<InstrumentType>().Select(i => new MemberOptionDto(i.ToString(), StatusHelper.GetInstrumentDisplay(i))).ToList(),
            all.Count > 0 ? all.Max(c => c.SortOrder) + 1 : 1,
            NaipesAuthorization.CanManage(user)));
    }

    public async Task<EventResult<NaipeItemDto>> CreateAsync(NaipeUpload upload, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return EventResult<NaipeItemDto>.Fail(EventResultStatus.SignInRequired);
        }

        var errors = new Dictionary<string, string[]>();
        if (!TryInstrument(upload.Instrument, out var type))
        {
            errors["instrument"] = new[] { "Escolha o instrumento." };
        }

        string? mimeType = null;
        if (upload.Length <= 0)
        {
            errors["file"] = new[] { upload.IsVideo ? "Escolha um ficheiro de vídeo." : "Escolha um ficheiro de imagem." };
        }
        else if ((mimeType = upload.IsVideo ? VideoType(upload) : ImageType(upload.Content)) is null)
        {
            errors["file"] = new[] { upload.IsVideo ? "O vídeo tem de ser MP4, MOV, WebM ou 3GP." : "A imagem tem de ser JPEG, PNG, WebP ou GIF." };
        }
        else if (upload.Length > (upload.IsVideo ? MaxVideoBytes : MaxImageBytes))
        {
            errors["file"] = new[] { upload.IsVideo ? "O vídeo não pode exceder 100 MB." : "A imagem não pode exceder 10 MB." };
        }

        var description = Clean(upload.Description);
        ValidateText(errors, Clean(upload.Title) ?? "-", description);
        var sortOrder = ParseSortOrder(upload.SortOrder, errors);

        if (errors.Count > 0)
        {
            return Invalid<NaipeItemDto>(errors);
        }

        // An untitled item is "<Instrumento>_<Video|Imagem>_<n>".
        var all = await _naipes.GetAllContentAsync();
        var title = Clean(upload.Title)
            ?? $"{StatusHelper.GetInstrumentDisplay(type)}_{(upload.IsVideo ? "Video" : "Imagem")}_{all.Count(c => c.InstrumentType == type && c.IsVideo == upload.IsVideo) + 1}";
        var created = await _naipes.CreateContentAsync(type, title, description, upload.Content,
            StoredFileName(upload.FileName, mimeType!), mimeType!, upload.IsVideo, sortOrder, me);
        return EventResult<NaipeItemDto>.Ok(Item(await _naipes.GetContentByIdAsync(created.Id) ?? created, user)); // with its author
    }

    public async Task<EventResult<NaipeItemDto>> UpdateAsync(int id, NaipeItemInput input, ClaimsPrincipal user)
    {
        var (_, refused) = await EditableAsync<NaipeItemDto>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        var errors = new Dictionary<string, string[]>();
        var title = Clean(input.Title);
        var description = Clean(input.Description);
        if (title is null)
        {
            errors["title"] = new[] { "O título é obrigatório." };
        }

        ValidateText(errors, title ?? "-", description);
        if (input.SortOrder is not { } sortOrder || sortOrder < MinSortOrder || sortOrder > MaxSortOrder)
        {
            errors["sortOrder"] = new[] { "A ordem deve estar entre 0.1 e 999.9." };
            sortOrder = 0;
        }

        if (errors.Count > 0)
        {
            return Invalid<NaipeItemDto>(errors);
        }

        await _naipes.UpdateContentAsync(id, title!, description, sortOrder, user.UserId()!, NaipesAuthorization.CanManage(user));
        var updated = await _naipes.GetContentByIdAsync(id);
        return updated is null ? EventResult<NaipeItemDto>.Fail(EventResultStatus.NotFound) : EventResult<NaipeItemDto>.Ok(Item(updated, user));
    }

    public async Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user)
    {
        var (_, refused) = await EditableAsync<bool>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        // The file leaves R2, and the item goes with its comments and plays.
        await _naipes.DeleteContentAsync(id, user.UserId()!, NaipesAuthorization.CanManage(user));
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> PlayedAsync(int id, ClaimsPrincipal user)
    {
        var (item, refused) = await ItemAsync<bool>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        if (!item.IsVideo)
        {
            return EventResult<bool>.Invalid("id", "Só os vídeos contam reproduções.");
        }

        await _naipes.IncrementPlayCountAsync(id, user.UserId());
        return EventResult<bool>.Ok(true);
    }
}

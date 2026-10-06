using System.Globalization;
using System.Security.Claims;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /naipes and /naipes/config behind the React pages (task 033, docs/react-naipes.md). Same data and
/// behaviour through <see cref="INaipeService"/> (which keeps the R2 storage, the audit log and the push to everyone on
/// a new item), rules now enforced here (<see cref="NaipesAuthorization"/>; the old pages only hid buttons):
/// - the picker lists the visible instruments in their display order (the settings are created on first use, as before);
/// - the chosen instrument's items, searched over title and description, by display order then creation
///   (<see cref="INaipeContentFilterService"/>);
/// - add: a video (MP4, MOV, WebM or 3GP, ≤100 MB) or an image (JPEG, PNG, WebP or GIF, ≤10 MB), each checked by its
///   bytes and stored with the type the server decided; a title (≤200; blank gets the old "Guitarra_Video_3" default), an
///   optional description (≤1000) and a display order (0.1-999.9); edit (title, description, order) and delete: the
///   member who added it, or Admin / Owner;
/// - comments (≤1000), newest first; delete: the author, or Admin / Owner; one play counted per video opening;
/// - settings (Admin / Owner): picture (JPEG, PNG or WebP, ≤5 MB), visibility and display order (0-999).
/// No schema change.
/// </summary>
public sealed class NaipeBoardService : INaipeBoardService
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
        if (!NaipesAuthorization.IsMember(user))
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
            selected = null; // a hidden instrument is off the board, as the old picker never offered it
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
        if (NaipesAuthorization.UserId(user) is not { } me)
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

        // As the old page: an untitled item is "<Instrumento>_<Video|Imagem>_<n>".
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

        await _naipes.UpdateContentAsync(id, title!, description, sortOrder, NaipesAuthorization.UserId(user)!, NaipesAuthorization.CanManage(user));
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

        // The old path: the file leaves R2, and the item goes with its comments and plays.
        await _naipes.DeleteContentAsync(id, NaipesAuthorization.UserId(user)!, NaipesAuthorization.CanManage(user));
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

        await _naipes.IncrementPlayCountAsync(id, NaipesAuthorization.UserId(user));
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<IReadOnlyList<NaipeCommentItemDto>>> GetCommentsAsync(int id, ClaimsPrincipal user)
    {
        var (_, refused) = await ItemAsync<IReadOnlyList<NaipeCommentItemDto>>(id, user);
        return refused ?? EventResult<IReadOnlyList<NaipeCommentItemDto>>.Ok(await CommentsAsync(id, user));
    }

    public async Task<EventResult<IReadOnlyList<NaipeCommentItemDto>>> AddCommentAsync(int id, NaipeCommentInput input, ClaimsPrincipal user)
    {
        var (_, refused) = await ItemAsync<IReadOnlyList<NaipeCommentItemDto>>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        var text = Clean(input.Text);
        if (text is null)
        {
            return EventResult<IReadOnlyList<NaipeCommentItemDto>>.Invalid("text", "Escreva o comentário.");
        }

        if (text.Length > MaxCommentLength)
        {
            return EventResult<IReadOnlyList<NaipeCommentItemDto>>.Invalid("text", $"O comentário não pode exceder {MaxCommentLength} caracteres.");
        }

        await _naipes.AddCommentAsync(id, NaipesAuthorization.UserId(user)!, text);
        return EventResult<IReadOnlyList<NaipeCommentItemDto>>.Ok(await CommentsAsync(id, user));
    }

    public async Task<EventResult<IReadOnlyList<NaipeCommentItemDto>>> DeleteCommentAsync(int id, int commentId, ClaimsPrincipal user)
    {
        var (_, refused) = await ItemAsync<IReadOnlyList<NaipeCommentItemDto>>(id, user);
        if (refused is not null)
        {
            return refused;
        }

        var me = NaipesAuthorization.UserId(user)!;
        if ((await _naipes.GetCommentsAsync(id, me)).FirstOrDefault(c => c.Id == commentId) is not { } comment)
        {
            return EventResult<IReadOnlyList<NaipeCommentItemDto>>.Fail(EventResultStatus.NotFound);
        }

        if (comment.AuthorId != me && !NaipesAuthorization.CanManage(user))
        {
            return EventResult<IReadOnlyList<NaipeCommentItemDto>>.Fail(EventResultStatus.Forbidden);
        }

        await _naipes.DeleteCommentAsync(commentId, me, NaipesAuthorization.CanManage(user));
        return EventResult<IReadOnlyList<NaipeCommentItemDto>>.Ok(await CommentsAsync(id, user));
    }

    public async Task<EventResult<IReadOnlyList<NaipeTypeSettingDto>>> GetSettingsAsync(ClaimsPrincipal user)
    {
        if (SettingsRefusal(user) is { } refused)
        {
            return refused;
        }

        await _naipes.InitializeTypeConfigsAsync();
        return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Ok(await SettingsAsync());
    }

    public async Task<EventResult<IReadOnlyList<NaipeTypeSettingDto>>> UpdateSettingAsync(int id, NaipeTypeSettingInput input, ClaimsPrincipal user)
    {
        var (setting, refused) = await SettingAsync(id, user);
        if (refused is not null)
        {
            return refused;
        }

        if (input.SortOrder is not { } sortOrder || sortOrder < 0 || sortOrder > MaxTypeSortOrder)
        {
            return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Invalid("sortOrder", $"A ordem deve estar entre 0 e {MaxTypeSortOrder}.");
        }

        await _naipes.UpdateTypeConfigAsync(id, setting.PictureUrl, input.IsVisible, sortOrder);
        return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Ok(await SettingsAsync());
    }

    public async Task<EventResult<IReadOnlyList<NaipeTypeSettingDto>>> SetPictureAsync(int id, NaipePictureUpload picture, ClaimsPrincipal user)
    {
        var (_, refused) = await SettingAsync(id, user);
        if (refused is not null)
        {
            return refused;
        }

        if (picture.Length <= 0 || !PictureTypes.Contains(picture.ContentType, StringComparer.OrdinalIgnoreCase) || !LooksLikeImage(picture.Content))
        {
            return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Invalid("picture", "A imagem tem de ser JPEG, PNG ou WebP.");
        }

        if (picture.Length > MaxPictureBytes)
        {
            return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Invalid("picture", "A imagem não pode exceder 5 MB.");
        }

        // The old path: stored under the instrument's typeconfig folder; the previous picture is deleted.
        await _naipes.UploadTypeConfigPictureAsync(id, picture.Content,
            string.IsNullOrWhiteSpace(picture.FileName) ? "naipe.jpg" : picture.FileName, picture.ContentType.ToLowerInvariant());
        return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Ok(await SettingsAsync());
    }

    public async Task<EventResult<IReadOnlyList<NaipeTypeSettingDto>>> RemovePictureAsync(int id, ClaimsPrincipal user)
    {
        var (setting, refused) = await SettingAsync(id, user);
        if (refused is not null)
        {
            return refused;
        }

        // As before, "Remover imagem" clears the reference only (the stored file stays).
        await _naipes.UpdateTypeConfigAsync(id, null, setting.IsVisible, setting.SortOrder);
        return EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Ok(await SettingsAsync());
    }

    // ---------- helpers ----------

    private static NaipeItemDto Item(NaipeContentDto c, ClaimsPrincipal user) => new(
        c.Id,
        c.InstrumentType.ToString(),
        StatusHelper.GetInstrumentDisplay(c.InstrumentType),
        c.Title,
        c.Description,
        c.Url,
        c.MimeType,
        c.IsVideo,
        c.SortOrder,
        c.CreatedByUserName,
        c.CreatedAt,
        c.PlayCount,
        c.CommentCount,
        NaipesAuthorization.CanEdit(user, c.CreatedByUserId));

    private async Task<IReadOnlyList<NaipeCommentItemDto>> CommentsAsync(int id, ClaimsPrincipal user)
    {
        var me = NaipesAuthorization.UserId(user);
        var manage = NaipesAuthorization.CanManage(user);
        return (await _naipes.GetCommentsAsync(id, me))
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new NaipeCommentItemDto(c.Id, c.AuthorName, c.AuthorAvatarUrl, c.Text, c.CreatedAt, manage || c.AuthorId == me))
            .ToList();
    }

    private async Task<(NaipeContentDto Item, EventResult<T>? Refused)> ItemAsync<T>(int id, ClaimsPrincipal user)
    {
        if (!NaipesAuthorization.IsMember(user))
        {
            return (null!, EventResult<T>.Fail(EventResultStatus.SignInRequired));
        }

        return await _naipes.GetContentByIdAsync(id) is { } item ? (item, null) : (null!, EventResult<T>.Fail(EventResultStatus.NotFound));
    }

    private async Task<(NaipeContentDto Item, EventResult<T>? Refused)> EditableAsync<T>(int id, ClaimsPrincipal user)
    {
        var (item, refused) = await ItemAsync<T>(id, user);
        if (refused is not null)
        {
            return (item, refused);
        }

        return NaipesAuthorization.CanEdit(user, item.CreatedByUserId) ? (item, null) : (item, EventResult<T>.Fail(EventResultStatus.Forbidden));
    }

    private static EventResult<IReadOnlyList<NaipeTypeSettingDto>>? SettingsRefusal(ClaimsPrincipal user) =>
        !NaipesAuthorization.IsMember(user) ? EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Fail(EventResultStatus.SignInRequired)
        : !NaipesAuthorization.CanManage(user) ? EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Fail(EventResultStatus.Forbidden)
        : null;

    private async Task<(NaipeTypeSettingDto Setting, EventResult<IReadOnlyList<NaipeTypeSettingDto>>? Refused)> SettingAsync(int id, ClaimsPrincipal user)
    {
        if (SettingsRefusal(user) is { } refused)
        {
            return (null!, refused);
        }

        return (await SettingsAsync()).FirstOrDefault(s => s.Id == id) is { } setting
            ? (setting, null)
            : (null!, EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Fail(EventResultStatus.NotFound));
    }

    /// <summary>Every instrument, by display order then instrument (the repository's order, as the old page loaded it).</summary>
    private async Task<IReadOnlyList<NaipeTypeSettingDto>> SettingsAsync() =>
        (await _naipes.GetAllTypeConfigsAsync())
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.InstrumentType)
            .Select(c => new NaipeTypeSettingDto(c.Id, c.InstrumentType.ToString(), c.InstrumentTypeName, c.PictureUrl, c.IsVisible, c.SortOrder))
            .ToList();

    private static bool TryInstrument(string? value, out InstrumentType type) =>
        Enum.TryParse(value?.Trim(), ignoreCase: false, out type) && Enum.IsDefined(type) && !int.TryParse(value, out _);

    private static void ValidateText(Dictionary<string, string[]> errors, string title, string? description)
    {
        if (title.Length > MaxTitleLength)
        {
            errors["title"] = new[] { $"O título não pode ter mais de {MaxTitleLength} caracteres." };
        }

        if (description is { Length: > MaxDescriptionLength })
        {
            errors["description"] = new[] { $"A descrição não pode ter mais de {MaxDescriptionLength} caracteres." };
        }
    }

    private static decimal ParseSortOrder(string? value, Dictionary<string, string[]> errors)
    {
        if (decimal.TryParse(value?.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var order)
            && order >= MinSortOrder && order <= MaxSortOrder)
        {
            return order;
        }

        errors["sortOrder"] = new[] { "A ordem deve estar entre 0.1 e 999.9." };
        return 0;
    }

    private static EventResult<T> Invalid<T>(Dictionary<string, string[]> errors) => new(EventResultStatus.Invalid, Errors: errors);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // The extension of the stored object follows the type decided above, whatever the browser called the file.
    private static string StoredFileName(string? fileName, string mimeType)
    {
        var name = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
        var extension = mimeType switch
        {
            "video/mp4" => ".mp4", "video/quicktime" => ".mov", "video/webm" => ".webm", "video/3gpp" => ".3gp",
            "image/png" => ".png", "image/webp" => ".webp", "image/gif" => ".gif", _ => ".jpg",
        };
        return (string.IsNullOrWhiteSpace(name) ? (mimeType.StartsWith("video/", StringComparison.Ordinal) ? "video" : "imagem") : name) + extension;
    }

    // A video needs a known extension (or, without one, a known declared type) and a matching container signature:
    // ISO base media (MP4, MOV, 3GP: a box type at bytes 4-7) or EBML (WebM).
    private static string? VideoType(NaipeUpload upload)
    {
        var extension = Path.GetExtension(upload.FileName ?? string.Empty);
        var mimeType = string.IsNullOrEmpty(extension)
            ? VideoTypesByHeader.GetValueOrDefault(upload.ContentType.Trim())
            : VideoTypes.GetValueOrDefault(extension);

        if (mimeType is null || Head(upload.Content) is not { } head)
        {
            return null;
        }

        var box = System.Text.Encoding.ASCII.GetString(head, 4, 4);
        var isoMedia = box is "ftyp" or "moov" or "mdat" or "wide" or "free" or "skip";
        var ebml = head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3;
        return (mimeType == "video/webm" ? ebml : isoMedia) ? mimeType : null;
    }

    // The image type comes from the bytes alone.
    private static string? ImageType(Stream content) => Head(content) is not { } head ? null
        : head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF ? "image/jpeg"
        : head[0] == 0x89 && head[1] == 'P' && head[2] == 'N' && head[3] == 'G' ? "image/png"
        : head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P' ? "image/webp"
        : head[0] == 'G' && head[1] == 'I' && head[2] == 'F' && head[3] == '8' ? "image/gif"
        : null;

    // The first 12 bytes, the stream rewound; null when the stream cannot rewind or is shorter.
    private static byte[]? Head(Stream content)
    {
        if (!content.CanSeek)
        {
            return null;
        }

        var head = new byte[12];
        var read = content.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        content.Position = 0;
        return read == head.Length ? head : null;
    }

    private static bool LooksLikeImage(Stream content) => ImageType(content) is "image/jpeg" or "image/png" or "image/webp";
}

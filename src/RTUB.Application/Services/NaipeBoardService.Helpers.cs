// Naipe board: shared helpers (lookups, validation, file types).
using System.Globalization;
using System.Security.Claims;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class NaipeBoardService
{
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
        var me = user.UserId();
        var manage = NaipesAuthorization.CanManage(user);
        return (await _naipes.GetCommentsAsync(id, me))
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new NaipeCommentItemDto(c.Id, c.AuthorName, c.AuthorAvatarUrl, c.Text, c.CreatedAt, manage || c.AuthorId == me))
            .ToList();
    }

    private async Task<(NaipeContentDto Item, EventResult<T>? Refused)> ItemAsync<T>(int id, ClaimsPrincipal user)
    {
        if (!user.IsMember())
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
        !user.IsMember() ? EventResult<IReadOnlyList<NaipeTypeSettingDto>>.Fail(EventResultStatus.SignInRequired)
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

    /// <summary>Every instrument, by display order then instrument (the repository's order).</summary>
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

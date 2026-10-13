namespace RTUB.Application.DTOs;

// The React /naipes and /naipes/config (task 033, docs/react-naipes.md): each instrument's teaching videos and images,
// their comments, and the Admin / Owner settings for the instrument picker.

/// <summary>
/// The page: the visible instruments (the picker), the chosen one's items (searched, by display order), how many items
/// that instrument has before the search, and what the member may do.
/// </summary>
public sealed record NaipeBoardDto(
    IReadOnlyList<NaipeInstrumentDto> Instruments,
    string? Instrument,
    IReadOnlyList<NaipeItemDto> Items,
    int TotalForInstrument,
    IReadOnlyList<MemberOptionDto> InstrumentOptions,
    decimal NextSortOrder,
    bool CanConfigure);

/// <summary>An instrument in the picker: its value, its name and its picture (null = the default icon).</summary>
public sealed record NaipeInstrumentDto(string Value, string Label, string? PictureUrl);

/// <summary>One video or image. <c>CanEdit</c>: the member who added it, or Admin / Owner (edit and delete).</summary>
public sealed record NaipeItemDto(
    int Id,
    string Instrument,
    string InstrumentLabel,
    string Title,
    string? Description,
    string Url,
    string MimeType,
    bool IsVideo,
    decimal SortOrder,
    string CreatedBy,
    DateTime CreatedAt,
    int PlayCount,
    int CommentCount,
    bool CanEdit);

/// <summary>A comment, newest first. <c>CanDelete</c>: its author, or Admin / Owner.</summary>
public sealed record NaipeCommentItemDto(int Id, string AuthorName, string AuthorAvatarUrl, string Text, DateTime CreatedAt, bool CanDelete);

/// <summary>A new video or image as uploaded: the file, and the form's fields (a blank title gets the old default).</summary>
public sealed record NaipeUpload(
    Stream Content,
    string? FileName,
    string ContentType,
    long Length,
    string? Instrument,
    bool IsVideo,
    string? Title,
    string? Description,
    string? SortOrder);

public sealed record NaipeItemInput(string? Title, string? Description, decimal? SortOrder);

public sealed record NaipeCommentInput(string? Text);

/// <summary>An instrument's settings (Admin / Owner): picture, visibility on /naipes and display order.</summary>
public sealed record NaipeTypeSettingDto(int Id, string Instrument, string Label, string? PictureUrl, bool IsVisible, int SortOrder);

public sealed record NaipeTypeSettingInput(bool IsVisible, int? SortOrder);

/// <summary>An instrument picture as uploaded: JPEG, PNG or WebP, at most 5 MB.</summary>
public sealed record NaipePictureUpload(Stream Content, string? FileName, string ContentType, long Length);

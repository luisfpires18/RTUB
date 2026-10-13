namespace RTUB.Application.DTOs;

// Contracts of the React /gallery timeline (React track 009, docs/react-gallery.md). Built for the
// browser and the caller: a visitor never receives a members-only item, its URL or a person tag;
// no uploader, user id of an uploader, audit field or EF entity leaves the server.

/// <summary>One page of the timeline plus the filters the caller may use.</summary>
public sealed record GalleryTimelineDto(
    bool IsMember,
    IReadOnlyList<GalleryItemDto> Items,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<int> Years,
    IReadOnlyList<GalleryPersonDto> People);

/// <summary>
/// A photo or video. <c>Url</c> is null when the stored link is not a usable https/same-site URL
/// (the page shows a placeholder). <c>People</c> is always empty for visitors.
/// </summary>
public sealed record GalleryItemDto(
    int Id,
    string Title,
    string Type,
    string? Url,
    int Year,
    int? Month,
    int? Day,
    bool MembersOnly,
    IReadOnlyList<GalleryPersonDto> People,
    bool CanEdit = false);

/// <summary>A tagged member, for members only. <c>Id</c> is the person filter key.</summary>
public sealed record GalleryPersonDto(string Id, string Name);

/// <summary>
/// Timeline filters; anything out of range is clamped or ignored. <c>PublicOnly</c> narrows a
/// member's view to what visitors see (the home preview); it can never widen anyone's.
/// </summary>
public sealed record GalleryQuery(int Page = 1, int PageSize = 24, int? Year = null, string? Search = null, string? PersonId = null, bool PublicOnly = false);

// ---------- management (members, React track 015) ----------

/// <summary>An item as its edit form needs it (the uploader, Admin or Owner only). <c>Date</c> is "yyyy-MM-dd".</summary>
public sealed record GalleryEditDto(int Id, string Title, string Type, string? Url, string Date, bool MembersOnly, IReadOnlyList<GalleryPersonDto> People);

/// <summary>A member who can be tagged: user id (signed-in members only), name, full name and avatar.</summary>
public sealed record GalleryTaggableDto(string Id, string Name, string? FullName, string AvatarUrl);

/// <summary>
/// A file as the browser sends it, with its details. <c>Date</c> "yyyy-MM-dd": the 1st of January keeps only the
/// year, the 1st of another month the year and month (the old form's rule). <c>PersonIds</c>: who appears.
/// </summary>
public sealed record GalleryUpload(Stream Content, string FileName, string ContentType, long Length, string? Title, string? Date, bool MembersOnly, IReadOnlyList<string> PersonIds);

public sealed record GalleryEditInput(string? Title, string? Date, bool MembersOnly, IReadOnlyList<string>? PersonIds);

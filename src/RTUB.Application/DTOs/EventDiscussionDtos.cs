namespace RTUB.Application.DTOs;

// ---------- discussion (members, React track 013) ----------

/// <summary>Who wrote something: nickname (or name), full name, avatar and category badge. Never an id, email or phone.</summary>
public sealed record EventAuthorDto(string Name, string? FullName, string AvatarUrl, string? Badge);

/// <summary>
/// An event's conversation for a signed-in member: posts pinned first, then by latest activity (as before),
/// each with its comments oldest first. The <c>Can*</c> flags are the server's rules for the caller.
/// </summary>
public sealed record EventDiscussionDto(bool CanModerate, IReadOnlyList<EventPostDto> Posts);

public sealed record EventPostDto(
    int Id,
    string Title,
    string Body,
    EventAuthorDto Author,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    bool Edited,
    bool Pinned,
    bool Locked,
    bool Mine,
    bool CanEdit,
    bool CanDelete,
    bool CanComment,
    IReadOnlyList<EventCommentDto> Comments,
    EventTransportDto? Transport);

public sealed record EventCommentDto(int Id, string Body, EventAuthorDto Author, DateTime CreatedAt, bool Edited, bool CanEdit, bool CanDelete);

/// <summary>A lift offer. <c>CanManage</c>: the driver or Admin/Owner may add and remove passengers.</summary>
public sealed record EventTransportDto(string Vehicle, int TotalSeats, string? Notes, bool CanManage, IReadOnlyList<EventPassengerDto> Passengers);

/// <summary>A passenger row (its id, not the member's); <c>CanRemove</c>: the driver, Admin/Owner or the passenger.</summary>
public sealed record EventPassengerDto(int Id, EventAuthorDto Member, bool Mine, bool CanRemove);

/// <summary>A new post or an edit. A blank title is taken from the start of the text.</summary>
public sealed record EventPostInput(string? Title, string? Body);

public sealed record EventCommentInput(string? Body);

/// <summary>Admin/Owner: pin to the top, or lock (no new comments).</summary>
public sealed record EventPostFlagsInput(bool Pinned, bool Locked);

/// <summary>A lift offer: what car, how many seats (2-20) and optional notes (≤500).</summary>
public sealed record EventTransportInput(string? Vehicle, int Seats, string? Notes);

public sealed record EventPassengerInput(string? UserId);

// ---------- contacts (Mod and above, React track 013) ----------

/// <summary>Who was called about an event and what they said; members not contacted yet follow.</summary>
public sealed record EventContactsDto(IReadOnlyList<EventContactRowDto> Contacted, IReadOnlyList<EventContactRowDto> NotContacted);

/// <summary>
/// One member: user id (Mod and above only), name, avatar and phone, and for a contacted one the answer
/// (true going, false not going, null undecided), notes and when.
/// </summary>
public sealed record EventContactRowDto(
    string UserId,
    string Name,
    string? FullName,
    string AvatarUrl,
    string? Phone,
    bool? WillAttend,
    string? Notes,
    DateTime? ContactedAt);

public sealed record EventContactInput(bool? WillAttend, string? Notes);

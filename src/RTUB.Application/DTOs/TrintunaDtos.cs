namespace RTUB.Application.DTOs;

// Contracts of the React /trintuna (docs/react-trintuna.md). Owner and listed members only. No user id leaves the
// server except a gathering's organizerUserId for the Owner (the edit form) and the access list (Owner only).

/// <summary>Próximos (soonest first, undated after) and Passados (historical or before today, newest first).</summary>
public sealed record TrintunaBoardDto(bool CanManage, IReadOnlyList<GatheringDto> Upcoming, IReadOnlyList<GatheringDto> Past);

public sealed record TrintunaPersonDto(string DisplayName, string? AvatarUrl);

/// <summary>
/// One gathering. <c>Date</c> is "yyyy-MM-dd" or null. <c>MyStatus</c>: "going", "notGoing" or null.
/// <c>Poll</c> has the shape of the portal's MeetingPoll (counts only, no voter names).
/// </summary>
public sealed record GatheringDto(
    int Id,
    int Number,
    string Title,
    string Place,
    string? Description,
    string? Date,
    TrintunaPersonDto? Organizer,
    string? OrganizerUserId,
    bool CanEdit,
    bool Past,
    int GoingCount,
    string? MyStatus,
    GatheringPollDto Poll);

/// <summary>The date poll as the portal's MeetingPoll; <c>Id</c> is the gathering id.</summary>
public sealed record GatheringPollDto(
    int Id,
    string Audience,
    string AudienceLabel,
    string Question,
    DateTime CreatedAt,
    DateTime? ClosesAt,
    bool Closed,
    bool CanManage,
    bool CanVote,
    bool CanDelete,
    int? MyOptionId,
    int TotalVotes,
    IReadOnlyList<GatheringPollOptionDto> Options);

/// <summary><c>Date</c> is "yyyy-MM-dd" (the edit form); <c>Text</c> is its Portuguese label.</summary>
public sealed record GatheringPollOptionDto(int Id, string Text, string Date, int Votes);

/// <summary>Create / edit. <c>PollDates</c>: 0-12 distinct "yyyy-MM-dd". There is no date field: see <c>SettleAsync</c>.</summary>
public sealed record GatheringInput(int Number, string? Place, string? Description, string? OrganizerUserId, IReadOnlyList<string>? PollDates);

public sealed record GatheringOptionInput(int OptionId);

public sealed record GatheringPresenceInput(bool WillAttend);

/// <summary>A member on (or for) the access list.</summary>
public sealed record TrintunaMemberDto(string Id, string DisplayName, string? AvatarUrl);

public sealed record TrintunaAccessInput(IReadOnlyList<string>? UserIds);

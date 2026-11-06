namespace RTUB.Application.DTOs;

// Contracts of the React /logistics and /logistics/{id} ("Logística", React track 023; were the Blazor Logistics.razor and
// LogisticsBoard.razor). Signed-in members, Leitões refused unless they manage. A member id leaves the server only
// where a manager needs it to change an assignment; members see names and avatars, as the old pages showed.

public sealed record LogisticsEventDto(int Id, string Name, DateTime Date);

/// <summary>A board card on /logistics: name, description, event, completion.</summary>
public sealed record LogisticsBoardSummaryDto(int Id, string Name, string Description, LogisticsEventDto? Event, bool IsCompleted, DateTime? CompletedAt);

/// <summary>Active and completed boards, newest first (as before). <c>CanManage</c>: Mod, Admin, Owner.</summary>
public sealed record LogisticsBoardsDto(IReadOnlyList<LogisticsBoardSummaryDto> Active, IReadOnlyList<LogisticsBoardSummaryDto> Completed, bool CanManage);

public sealed record LogisticsBoardInput(string? Name, string? Description, int? EventId);

public sealed record LogisticsBoardStateInput(bool Completed);

public sealed record LogisticsLabelDto(string Text, string Color);

public sealed record LogisticsPersonDto(string DisplayName, string? FullName, string? AvatarUrl);

/// <summary>
/// A card face. <c>Checklist</c> = (done, total); <c>Links</c> = linked cards; <c>AssignedTo</c> = the single
/// "Atribuir a" member - the card's owner ("Responsável", task 066); <c>Assignments</c> = how many collaborators
/// ("Membros atribuídos"); <c>Mine</c> = the caller is the owner or a collaborator (task 065: "Atribuído a si");
/// <c>Priority</c> Low / Normal / High / Urgent; <c>Comments</c>, <c>Attachments</c> = counts (task 066).
/// </summary>
public sealed record LogisticsCardDto(
    int Id,
    string Title,
    string Description,
    string Status,
    IReadOnlyList<LogisticsLabelDto> Labels,
    DateTime? StartDate,
    DateTime? DueDate,
    string? EventName,
    LogisticsPersonDto? AssignedTo,
    int Assignments,
    int ChecklistDone,
    int ChecklistTotal,
    int Links,
    bool Mine,
    string Priority,
    int Comments,
    int Attachments);

public sealed record LogisticsListDto(int Id, string Name, IReadOnlyList<LogisticsCardDto> Cards);

/// <summary>
/// One board: lists by position, cards by position (ties by creation). <c>Labels</c>: every label text on the board,
/// for the filter. <c>Files</c>: the board's files (shared by its cards, as before).
/// </summary>
public sealed record LogisticsBoardDto(
    int Id,
    string Name,
    string Description,
    LogisticsEventDto? Event,
    bool IsCompleted,
    IReadOnlyList<LogisticsListDto> Lists,
    IReadOnlyList<string> Labels,
    IReadOnlyList<LogisticsFileDto> Files,
    bool CanManage);

public sealed record LogisticsChecklistItemDto(string Task, bool Done);

public sealed record LogisticsLinkDto(int? CardId, string Name);

/// <summary>An assignee. The id is needed to remove the assignment, so only managers get it (null for everyone else).</summary>
public sealed record LogisticsAssigneeDto(string? UserId, string DisplayName, string? FullName, string? AvatarUrl);

/// <summary>
/// The card details (task 066: the card workspace). Every member who sees the board reads them. <c>AssignedTo</c> is the
/// owner, <c>Assignments</c> the collaborators (the owner left out). <c>CanManage</c> = may change everything (owner,
/// collaborators, labels, card links, delete); <c>CanWork</c> = owner, collaborator or manager: edits the card's own
/// fields and checklist, changes the status, comments and attaches. <c>Version</c> goes back with a field edit; a stale
/// one is refused (409).
/// </summary>
public sealed record LogisticsCardDetailDto(
    LogisticsCardDto Card,
    int ListId,
    LogisticsAssigneeDto? AssignedTo,
    IReadOnlyList<LogisticsChecklistItemDto> Checklist,
    IReadOnlyList<LogisticsLinkDto> Links,
    IReadOnlyList<LogisticsAssigneeDto> Assignments,
    bool CanManage,
    bool CanWork,
    int Version,
    IReadOnlyList<LogisticsCommentDto> Comments,
    IReadOnlyList<LogisticsAttachmentDto> Attachments,
    IReadOnlyList<LogisticsActivityDto> Activity);

/// <summary>A comment. <c>Mentions</c> = the mentioned members' names; <c>CanEdit</c> (author) and <c>CanDelete</c> (author or manager) for the caller.</summary>
public sealed record LogisticsCommentDto(int Id, LogisticsPersonDto? Author, string Body, IReadOnlyList<string> Mentions, DateTime CreatedAt,
    DateTime? EditedAt, bool CanEdit, bool CanDelete);

/// <summary>A card attachment: <c>Kind</c> "File" or "Link"; <c>Host</c> = a link's site; <c>CanDelete</c> (uploader or manager) for the caller.</summary>
public sealed record LogisticsAttachmentDto(int Id, string Kind, string Name, string? Host, long SizeBytes, string? Extension,
    LogisticsPersonDto? AddedBy, DateTime CreatedAt, bool CanDelete);

/// <summary>One history line: who, when, and what (already in pt-PT).</summary>
public sealed record LogisticsActivityDto(LogisticsPersonDto? Actor, string Action, string Detail, DateTime At);

public sealed record LogisticsListInput(string? Name);

public sealed record LogisticsPositionInput(int Position);

public sealed record LogisticsCardCreateInput(string? Title, string? Description, string? AssignedToUserId);

/// <summary>The core fields; dates are calendar days (yyyy-MM-dd), as the old date inputs.</summary>
/// <summary>
/// The card's own fields. <c>Version</c> = the version the edit started from (stale: 409). <c>AssignedToUserId</c> is the
/// owner: only a manager changes or clears it (anyone else sends none - kept - or the current one; another id is 403). <c>Priority</c> null keeps it.
/// </summary>
public sealed record LogisticsCardInput(string? Title, string? Description, string? StartDate, string? DueDate, string? AssignedToUserId,
    string? Priority = null, int? Version = null);

public sealed record LogisticsStatusInput(string? Status);

public sealed record LogisticsMoveInput(int ListId, int Position);

public sealed record LogisticsLabelsInput(IReadOnlyList<LogisticsLabelDto>? Labels);


/// <summary>Tick or untick one checklist item (task 065). <c>Task</c> must still be the item's text, so a list a manager just changed is not ticked blind.</summary>
public sealed record LogisticsChecklistToggleInput(string? Task, bool Done);

/// <summary>
/// Task 066: one checklist item at a time, so two people editing the list never overwrite each other. Add: <c>Task</c>.
/// Rename: <c>Expected</c> (the item's current text) and <c>Task</c>. Remove: <c>Expected</c>.
/// </summary>
public sealed record LogisticsChecklistItemInput(string? Task, string? Expected = null);

/// <summary>A comment (task 066): plain text ≤ 2000 and the ids of the members it mentions (≤ 20, board viewers). An edit keeps the mentions.</summary>
public sealed record LogisticsCommentInput(string? Body, IReadOnlyList<string>? MentionUserIds = null);

/// <summary>An external link attached to a card (task 066): http(s) only; <c>Name</c> defaults to the site.</summary>
public sealed record LogisticsLinkInput(string? Url, string? Name);

public sealed record LogisticsLinksInput(IReadOnlyList<int>? CardIds);

public sealed record LogisticsAssignmentInput(string? UserId);

public sealed record LogisticsMemberOptionDto(string Id, string DisplayName, string? FullName, string? AvatarUrl);

/// <summary>"Criar Lembrete": a card of the board, the frequency, the first date (local time) and the members.</summary>
public sealed record LogisticsReminderInput(int CardId, string? Frequency, DateTime? NextReminderDate, IReadOnlyList<string>? UserIds);

public sealed record LogisticsReminderDto(int Id, int CardId, string Frequency, DateTime NextReminderDate, int Members);

public sealed record LogisticsFileDto(string Name, string Extension, long SizeBytes);

public sealed record LogisticsFileUpload(Stream Content, string FileName, string ContentType, long Length);

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
/// "Atribuir a" member of the creation form; <c>Assignments</c> = how many "Membros atribuídos".
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
    int Links);

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

/// <summary>A manager's assignee: the id is needed to remove the assignment.</summary>
public sealed record LogisticsAssigneeDto(string UserId, string DisplayName, string? FullName, string? AvatarUrl);

/// <summary>The card details (managers): everything the old "Detalhes do Cartão" modal edited.</summary>
public sealed record LogisticsCardDetailDto(
    LogisticsCardDto Card,
    int ListId,
    LogisticsAssigneeDto? AssignedTo,
    IReadOnlyList<LogisticsChecklistItemDto> Checklist,
    IReadOnlyList<LogisticsLinkDto> Links,
    IReadOnlyList<LogisticsAssigneeDto> Assignments);

public sealed record LogisticsListInput(string? Name);

public sealed record LogisticsPositionInput(int Position);

public sealed record LogisticsCardCreateInput(string? Title, string? Description, string? AssignedToUserId);

/// <summary>The core fields; dates are calendar days (yyyy-MM-dd), as the old date inputs.</summary>
public sealed record LogisticsCardInput(string? Title, string? Description, string? StartDate, string? DueDate, string? AssignedToUserId);

public sealed record LogisticsStatusInput(string? Status);

public sealed record LogisticsMoveInput(int ListId, int Position);

public sealed record LogisticsLabelsInput(IReadOnlyList<LogisticsLabelDto>? Labels);

public sealed record LogisticsChecklistInput(IReadOnlyList<LogisticsChecklistItemDto>? Items);

public sealed record LogisticsLinksInput(IReadOnlyList<int>? CardIds);

public sealed record LogisticsAssignmentInput(string? UserId);

public sealed record LogisticsMemberOptionDto(string Id, string DisplayName, string? FullName, string? AvatarUrl);

/// <summary>"Criar Lembrete": a card of the board, the frequency, the first date (local time) and the members.</summary>
public sealed record LogisticsReminderInput(int CardId, string? Frequency, DateTime? NextReminderDate, IReadOnlyList<string>? UserIds);

public sealed record LogisticsReminderDto(int Id, int CardId, string Frequency, DateTime NextReminderDate, int Members);

public sealed record LogisticsFileDto(string Name, string Extension, long SizeBytes);

public sealed record LogisticsFileUpload(Stream Content, string FileName, string ContentType, long Length);

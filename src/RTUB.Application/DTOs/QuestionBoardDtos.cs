namespace RTUB.Application.DTOs;

// Contracts of the React /questions (task 031; was the Blazor "Perguntas aos Órgãos Sociais"). Signed-in members only.
// People are shown by name and avatar only (GovernanceMemberDto); no contact field leaves the server.

/// <summary>
/// One page of open or closed questions, by latest activity (newest first), as the old page listed them.
/// </summary>
public sealed record QuestionPageDto(IReadOnlyList<QuestionSummaryDto> Items, int Total, int Page, int PageSize);

/// <summary>Status: unanswered, answered, inDiscussion, closed. Dates are UTC (ISO 8601). The flags are for the caller.</summary>
public sealed record QuestionSummaryDto(
    int Id,
    string Title,
    string Content,
    GovernanceMemberDto Author,
    QuestionRecipientDto Recipient,
    string Status,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    int ReplyCount,
    bool IsMine,
    bool CanReply,
    bool CanRemind);

/// <summary>Who the question is for, and in which role ("Direção - Presidente", or just the position elsewhere).</summary>
public sealed record QuestionRecipientDto(string DisplayName, string? AvatarUrl, string Role);

public sealed record QuestionDetailDto(QuestionSummaryDto Question, IReadOnlyList<QuestionReplyDto> Replies);

/// <summary><c>FromRecipient</c>: written by the member the question is for (shown with their role).</summary>
public sealed record QuestionReplyDto(int Id, string Content, GovernanceMemberDto Author, bool FromRecipient, DateTime CreatedAt);

/// <summary>
/// A member who can be asked: one entry per Órgãos Sociais position they hold. <c>Id</c> is their user id, used to ask
/// and to filter the list.
/// </summary>
public sealed record QuestionRecipientOptionDto(string Id, string DisplayName, string? FullName, string? AvatarUrl, string Position, string Role);

public sealed record QuestionInput(string? Title, string? Content, string? RecipientId, string? Position);

public sealed record QuestionReplyInput(string? Content);

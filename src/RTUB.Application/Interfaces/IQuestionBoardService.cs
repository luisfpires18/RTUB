using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React /questions ("Perguntas aos Órgãos Sociais", task 031; was the Blazor Questions.razor): members ask the
/// holders of an Órgãos Sociais position, and the two take turns replying. Notifications stay in <see cref="IQuestionService"/>.
/// </summary>
public interface IQuestionBoardService
{
    /// <summary>One page (10 by default, at most 50) of open (<paramref name="closed"/> false) or closed questions.</summary>
    Task<EventResult<QuestionPageDto>> GetPageAsync(bool closed, string? search, string? recipientId, int page, int pageSize, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<QuestionRecipientOptionDto>>> GetRecipientsAsync(ClaimsPrincipal user);

    Task<EventResult<QuestionDetailDto>> GetAsync(int id, ClaimsPrincipal user);

    Task<EventResult<QuestionDetailDto>> AskAsync(QuestionInput input, ClaimsPrincipal user);

    Task<EventResult<QuestionDetailDto>> ReplyAsync(int id, QuestionReplyInput input, ClaimsPrincipal user);

    Task<EventResult<QuestionDetailDto>> CloseAsync(int id, ClaimsPrincipal user);

    Task<EventResult<bool>> RemindAsync(int id, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user);
}

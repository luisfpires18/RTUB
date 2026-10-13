using System.Security.Claims;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Helpers;

/// <summary>
/// The "Perguntas aos Órgãos Sociais" rules (task 031, docs/react-requests-questions.md), enforced server-side by
/// <c>QuestionBoardService</c>. Same as the Blazor /questions ([Authorize]; the rest decided by the question):
///
/// - Visitors: nothing (401); the page sends them to sign in.
/// - Any signed-in member (Leitões included, as before): every question and its replies; ask a question to a member
///   holding an Órgãos Sociais position.
/// - Replies take turns: the recipient answers, then only the author can reply, then only the recipient, and so on.
///   Nobody else replies, and nobody replies to a closed question.
/// - The author only: close, delete (soft), and remind the recipient while it is the recipient's turn.
/// No role (Admin, Owner) overrides any of this, as before.
/// </summary>
public static class QuestionsAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static string? UserId(ClaimsPrincipal user) => IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    public static bool IsAuthor(Question question, string me) => question.AuthorId == me;

    public static bool IsRecipient(Question question, string me) => question.AssignedMemberId == me;

    public static bool CanReply(Question question, string me) =>
        question.Status != QuestionStatus.Closed
        && (question.IsAwaitingUserReply ? IsAuthor(question, me) : IsRecipient(question, me));

    public static bool CanRemind(Question question, string me) =>
        IsAuthor(question, me) && question.Status != QuestionStatus.Closed && !CanReply(question, me);
}

using System.Security.Claims;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Helpers;

/// <summary>
/// The "Perguntas aos Órgãos Sociais" rules (docs/react-requests-questions.md), enforced server-side by
/// <c>QuestionBoardService</c>:
///
/// - Visitors: nothing; the page sends them to sign in.
/// - Any signed-in member (Leitões included): every question and its replies; ask a question to a member
///   holding an Órgãos Sociais position.
/// - Replies take turns: the recipient answers, then only the author can reply, then only the recipient, and so on.
///   Nobody else replies, and nobody replies to a closed question.
/// - The author only: close, delete (soft), and remind the recipient while it is the recipient's turn.
/// No role (Admin, Owner) overrides any of this.
/// </summary>
public static class QuestionsAuthorization
{
    public static bool IsAuthor(Question question, string me) => question.AuthorId == me;

    public static bool IsRecipient(Question question, string me) => question.AssignedMemberId == me;

    public static bool CanReply(Question question, string me) =>
        question.Status != QuestionStatus.Closed
        && (question.IsAwaitingUserReply ? IsAuthor(question, me) : IsRecipient(question, me));

    public static bool CanRemind(Question question, string me) =>
        IsAuthor(question, me) && question.Status != QuestionStatus.Closed && !CanReply(question, me);
}

namespace RTUB.Core.Enums;

/// <summary>
/// Status of a question posted to Orgãos Sociais members
/// </summary>
public enum QuestionStatus
{
    /// <summary>
    /// Question is awaiting a response from the assigned member
    /// </summary>
    Unanswered,

    /// <summary>
    /// Question has been answered by the assigned member
    /// </summary>
    Answered,

    /// <summary>
    /// Question is in an ongoing discussion between user and member
    /// </summary>
    InDiscussion
}

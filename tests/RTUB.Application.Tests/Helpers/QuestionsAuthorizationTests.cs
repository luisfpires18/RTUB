using FluentAssertions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// The Perguntas rules (docs/react-requests-questions.md). Replies take turns: the recipient answers first, then the
/// author, then the recipient again. The author reminds the recipient only while it is the recipient's turn. No role
/// changes any of this: the rules read only who the author and the recipient are.
/// </summary>
public class QuestionsAuthorizationTests
{
    private const string Author = "author";
    private const string Recipient = "recipient";
    private const string Other = "someone-else";

    [Fact]
    public void ANewQuestion_IsTheRecipientsTurn_AndTheAuthorMayRemindThem()
    {
        var question = NewQuestion();

        QuestionsAuthorization.CanReply(question, Recipient).Should().BeTrue();
        QuestionsAuthorization.CanReply(question, Author).Should().BeFalse();
        QuestionsAuthorization.CanRemind(question, Author).Should().BeTrue();
        QuestionsAuthorization.CanRemind(question, Recipient).Should().BeFalse("only the author reminds");
    }

    [Fact]
    public void OnceAnswered_ItIsTheAuthorsTurn_AndThereIsNobodyToRemind()
    {
        var question = NewQuestion();
        question.MarkAsAnswered();

        QuestionsAuthorization.CanReply(question, Author).Should().BeTrue();
        QuestionsAuthorization.CanReply(question, Recipient).Should().BeFalse();
        QuestionsAuthorization.CanRemind(question, Author).Should().BeFalse("the author is the one who has to reply");
    }

    [Fact]
    public void AfterTheAuthorRepliesAgain_TheTurnGoesBackToTheRecipient()
    {
        var question = NewQuestion();
        question.MarkAsAnswered();
        question.MarkAsInDiscussion();

        QuestionsAuthorization.CanReply(question, Recipient).Should().BeTrue();
        QuestionsAuthorization.CanReply(question, Author).Should().BeFalse();
        QuestionsAuthorization.CanRemind(question, Author).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AClosedQuestion_TakesNoReplyAndNoReminder_WhoeversTurnItWas(bool answeredFirst)
    {
        var question = NewQuestion();
        if (answeredFirst)
        {
            question.MarkAsAnswered();
        }

        question.Close();

        foreach (var member in new[] { Author, Recipient, Other })
        {
            QuestionsAuthorization.CanReply(question, member).Should().BeFalse();
            QuestionsAuthorization.CanRemind(question, member).Should().BeFalse();
        }
    }

    [Fact]
    public void SomeoneElse_IsNeitherAuthorNorRecipient_AndNeverRepliesOrReminds()
    {
        var question = NewQuestion();

        QuestionsAuthorization.IsAuthor(question, Author).Should().BeTrue();
        QuestionsAuthorization.IsRecipient(question, Recipient).Should().BeTrue();
        QuestionsAuthorization.IsAuthor(question, Other).Should().BeFalse();
        QuestionsAuthorization.IsRecipient(question, Other).Should().BeFalse();
        QuestionsAuthorization.CanReply(question, Other).Should().BeFalse();
        QuestionsAuthorization.CanRemind(question, Other).Should().BeFalse();

        question.MarkAsAnswered();
        QuestionsAuthorization.CanReply(question, Other).Should().BeFalse();
    }

    private static Question NewQuestion() =>
        Question.Create("Quotas deste ano", "Quando se pagam as quotas deste ano?", Author, Position.PrimeiroTesoureiro, Recipient);
}

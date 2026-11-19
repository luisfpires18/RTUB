using RTUB.Application.DTOs;
using RTUB.Application.Services;

namespace RTUB.Application.Factories;

// Push factory: questions.
public partial class PushNotificationFactory
{
    /// <summary>
    /// Creates a push notification for a new question.
    /// </summary>
    public SendPushNotificationDto CreateNewQuestionNotification(string questionTitle, string authorName, int questionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);

        var questionsUrl = QuestionsUrl;

        return new SendPushNotificationDto
        {
            Title = "Nova Pergunta",
            Body = $"{authorName} fez uma pergunta para si: {TruncateContent(questionTitle, 100)}",
            Icon = "/icons/rtub-logo-192.png",
            Url = questionsUrl,
            Tag = $"question-{questionId}",
            PreferenceKey = PushPreferenceCatalog.Questions
        };
    }

    /// <summary>
    /// Creates a push notification for a question reply from the author.
    /// </summary>
    public SendPushNotificationDto CreateQuestionReplyNotification(string replyPreview, int replyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replyPreview);

        var questionsUrl = QuestionsUrl;

        return new SendPushNotificationDto
        {
            Title = "Nova Resposta à Pergunta",
            Body = TruncateContent(replyPreview, 150),
            Icon = "/icons/rtub-logo-192.png",
            Url = questionsUrl,
            Tag = $"question-reply-{replyId}",
            PreferenceKey = PushPreferenceCatalog.Questions
        };
    }

    /// <summary>
    /// Creates a push notification when the assigned member answers a question.
    /// </summary>
    public SendPushNotificationDto CreateQuestionAnsweredNotification(string replyPreview, int replyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replyPreview);

        var questionsUrl = QuestionsUrl;

        return new SendPushNotificationDto
        {
            Title = "Pergunta Respondida",
            Body = $"A sua pergunta foi respondida: {TruncateContent(replyPreview, 100)}",
            Icon = "/icons/rtub-logo-192.png",
            Url = questionsUrl,
            Tag = $"question-reply-{replyId}",
            PreferenceKey = PushPreferenceCatalog.Questions
        };
    }

    /// <summary>
    /// Creates a push notification for a question reminder.
    /// </summary>
    public SendPushNotificationDto CreateQuestionReminderNotification(string questionTitle, string authorName, int questionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);

        var questionsUrl = QuestionsUrl;

        return new SendPushNotificationDto
        {
            Title = "Lembrete: Pergunta Pendente",
            Body = $"{authorName} enviou um lembrete para a sua pergunta: {TruncateContent(questionTitle, 100)}",
            Icon = "/icons/rtub-logo-192.png",
            Url = questionsUrl,
            Tag = $"question-reminder-{questionId}",
            PreferenceKey = PushPreferenceCatalog.Questions
        };
    }

    /// <summary>
    /// Creates a push notification for pending questions (background service).
    /// </summary>
    public SendPushNotificationDto CreatePendingQuestionsNotification(int questionCount, string? firstAuthorNickname)
    {

        var questionsUrl = QuestionsUrl;

        return new SendPushNotificationDto
        {
            Title = questionCount == 1 ? "Pergunta Pendente" : $"{questionCount} Perguntas Pendentes",
            Body = questionCount == 1
                ? $"Tem uma pergunta à espera da sua resposta de {firstAuthorNickname ?? "um membro"}"
                : $"Tem {questionCount} perguntas à espera da sua resposta",
            Icon = "/icons/rtub-logo-192.png",
            Url = questionsUrl,
            Tag = "question-reminder",
            PreferenceKey = PushPreferenceCatalog.Questions
        };
    }

    /// <summary>
    /// Truncates content to a maximum length with ellipsis.
    /// </summary>
    private static string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
        {
            return content;
        }
        return content[..(maxLength - 3)] + "...";
    }
}

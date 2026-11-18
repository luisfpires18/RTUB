using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

// Push factory contract: pending-work reminders, questions, summary and member notices.
public partial interface IPushNotificationFactory
{
    /// <summary>
    /// Creates a push notification for pending public request reminders (sent to admins daily).
    /// </summary>
    /// <param name="pendingCount">Number of pending public requests</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreatePendingPublicRequestsReminderNotification(int pendingCount);

    /// <summary>
    /// The daily "As minhas ações pendentes" summary, only for a member whose page has something pending. No
    /// count and no item details (lock screens); the link opens /pending-actions.
    /// </summary>
    SendPushNotificationDto CreatePendingActionsReminderNotification();

    /// <summary>
    /// Creates a push notification for pending meeting request reminders.
    /// </summary>
    /// <param name="meetingRequest">The pending meeting request</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreatePendingMeetingRequestReminderNotification(MeetingRequest meetingRequest);

    /// <summary>
    /// Creates a push notification for pending rehearsal approvals.
    /// </summary>
    /// <param name="pendingRehearsalCount">Number of rehearsals with pending attendance approvals</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreatePendingRehearsalApprovalsReminderNotification(int pendingRehearsalCount);

    /// <summary>
    /// Creates a push notification for a new question.
    /// </summary>
    /// <param name="questionTitle">Title of the question</param>
    /// <param name="authorName">Name of the user who asked the question</param>
    /// <param name="questionId">ID of the question</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateNewQuestionNotification(string questionTitle, string authorName, int questionId);

    /// <summary>
    /// Creates a push notification for a question reply.
    /// </summary>
    /// <param name="replyPreview">Preview of the reply content</param>
    /// <param name="replyId">ID of the reply</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateQuestionReplyNotification(string replyPreview, int replyId);

    /// <summary>
    /// Creates a push notification for a question reply from the assigned member.
    /// </summary>
    /// <param name="replyPreview">Preview of the reply content</param>
    /// <param name="replyId">ID of the reply</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateQuestionAnsweredNotification(string replyPreview, int replyId);

    /// <summary>
    /// Creates a push notification for a question reminder.
    /// </summary>
    /// <param name="questionTitle">Title of the question</param>
    /// <param name="authorName">Name of the user who asked the question</param>
    /// <param name="questionId">ID of the question</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateQuestionReminderNotification(string questionTitle, string authorName, int questionId);

    /// <summary>
    /// Creates a push notification for pending questions (background service).
    /// </summary>
    /// <param name="questionCount">Number of pending questions</param>
    /// <param name="firstAuthorNickname">Nickname of the first question's author</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreatePendingQuestionsNotification(int questionCount, string? firstAuthorNickname);

    /// <summary>
    /// Creates a push notification when new naipe content (video or image) is published.
    /// Sent to all subscribed users.
    /// </summary>
    /// <param name="contentTitle">The title of the content</param>
    /// <param name="instrumentType">The instrument; its display name is the title, its value the /naipes filter</param>
    /// <param name="isVideo">True if the content is a video, false if it's an image</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateNaipeContentNotification(string contentTitle, InstrumentType instrumentType, bool isVideo);

    /// <summary>
    /// Creates a push notification for weekly summary of events, rehearsals, and meetings.
    /// Sent to all users every Monday.
    /// </summary>
    /// <param name="eventCount">Number of events this week</param>
    /// <param name="rehearsalCount">Number of rehearsals this week</param>
    /// <param name="meetingCount">Number of meetings this week</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateWeeklySummaryNotification(int eventCount, int rehearsalCount, int meetingCount);

    /// <summary>
    /// Creates a push notification when a meeting request is rejected or expired.
    /// Sent to the request author.
    /// </summary>
    /// <param name="meetingRequest">The meeting request that was rejected</param>
    /// <param name="isExpired">Whether the request expired due to date</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateMeetingRequestRejectedNotification(MeetingRequest meetingRequest, bool isExpired);

    /// <summary>
    /// Creates a push notification for calotes (debt) reminder.
    /// Sent to users who owe money to the tuna.
    /// </summary>
    /// <param name="amountOwed">The amount the user owes</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateCalotesReminderNotification(decimal amountOwed);

    /// <summary>
    /// Creates a reminder push notification for a member to participate in activities.
    /// </summary>
    /// <param name="userDisplayName">Display name of the member receiving the reminder</param>
    /// <param name="userId">The member's user ID</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateMemberActivityReminderNotification(string? userDisplayName, string userId);

    /// <summary>
    /// Creates a push notification when a member is reactivated.
    /// Sent to all users.
    /// </summary>
    /// <param name="userNickname">The nickname of the member being reactivated</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateMemberReactivatedNotification(string userNickname);

    /// <summary>
    /// Creates a push notification warning a member that retirement is approaching (1 month away).
    /// Sent to the specific member.
    /// </summary>
    /// <param name="userNickname">The nickname of the member approaching retirement</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateMemberRetirementWarningNotification(string userNickname);

    /// <summary>
    /// Creates a push notification when a user is tagged in a gallery media item.
    /// Sent to the tagged user.
    /// </summary>
    /// <param name="mediaId">The gallery item; the link opens it (/gallery?item=)</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateGalleryTagNotification(int mediaId);

    /// <summary>Messaging v2: a new chat message, without its text; links to /messages?c={id}.</summary>
    SendPushNotificationDto CreateConversationMessageNotification(string senderName, string? groupTitle, int conversationId);

    /// <summary>
    /// Creates a push notification when a user is @mentioned in a post or comment.
    /// Sent only to the mentioned user.
    /// </summary>
    /// <param name="event">The event the discussion belongs to</param>
    /// <param name="mentionerNickname">The nickname of the user who wrote the mention</param>
    /// <param name="postTitle">The title of the post where the mention occurred</param>
    /// <param name="isComment">True if the mention is in a comment; false if in a post body</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateMentionNotification(Event @event, string mentionerNickname, string postTitle, bool isComment);

    /// <summary>The /profile test push: fixed text, opens the push section of /profile.</summary>
    SendPushNotificationDto CreatePushTestNotification();

    /// <summary>
    /// Logística: assignment, mention or reminder for one card. Card title and board name only - never the
    /// card's details or a comment's text. Opens the card on its board.
    /// </summary>
    SendPushNotificationDto CreateLogisticsNotification(LogisticsNotice kind, int boardId, int cardId, string cardTitle, string boardName,
        string? actorName = null, DateTime? dueDate = null);
}

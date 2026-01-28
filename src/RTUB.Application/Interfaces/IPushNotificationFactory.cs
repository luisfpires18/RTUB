using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Factory for creating push notification DTOs for various notification scenarios.
/// Centralizes all push notification construction logic for consistency and reusability.
/// </summary>
public interface IPushNotificationFactory
{
    /// <summary>
    /// Creates a push notification for an event (new event or reminder).
    /// </summary>
    /// <param name="event">The event to notify about</param>
    /// <param name="isReminder">True if this is a reminder notification, false for new event notification</param>
    /// <param name="baseUrl">The base URL of the application (e.g., "https://rtub.example.com")</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateEventNotification(Event @event, bool isReminder, string baseUrl);

    /// <summary>
    /// Creates a custom push notification for an event, similar to rehearsal custom notifications.
    /// Uses the event context for URL and tag but allows a fully custom body message.
    /// </summary>
    /// <param name="event">The event to notify about.</param>
    /// <param name="customBody">Custom message body provided by the user.</param>
    /// <param name="baseUrl">The base URL of the application (e.g., "https://rtub.example.com").</param>
    /// <returns>A SendPushNotificationDto ready to be sent.</returns>
    SendPushNotificationDto CreateEventCustomNotification(Event @event, string customBody, string baseUrl);

    /// <summary>
    /// Creates a custom push notification for a rehearsal.
    /// </summary>
    /// <param name="rehearsal">The rehearsal to notify about</param>
    /// <param name="customBody">Custom message body provided by the user</param>
    /// <param name="baseUrl">The base URL of the application (e.g., "https://rtub.example.com")</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalNotification(Rehearsal rehearsal, string customBody, string baseUrl);

    /// <summary>
    /// Creates a reminder push notification for a rehearsal (used by background schedulers).
    /// </summary>
    /// <param name="rehearsal">The rehearsal to remind about.</param>
    /// <param name="baseUrl">The base URL of the application.</param>
    /// <returns>A SendPushNotificationDto ready to be sent.</returns>
    SendPushNotificationDto CreateRehearsalReminderNotification(Rehearsal rehearsal, string baseUrl);

    /// <summary>
    /// Creates a push notification for event repertoire changes.
    /// Sent to all users enrolled in the event.
    /// </summary>
    /// <param name="event">The event whose repertoire changed</param>
    /// <param name="songTitle">The title of the song that was added or changed</param>
    /// <param name="isAdded">True if song was added, false if it was changed/updated</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateEventRepertoireNotification(Event @event, string songTitle, bool isAdded, string baseUrl);

    /// <summary>
    /// Creates a push notification for new discussion posts.
    /// Sent to all users enrolled in the event.
    /// </summary>
    /// <param name="event">The event with the new discussion post</param>
    /// <param name="authorNickname">The nickname of the post author</param>
    /// <param name="postTitle">The title of the post</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateDiscussionPostNotification(Event @event, string authorNickname, string postTitle, string baseUrl);

    /// <summary>
    /// Creates a push notification when someone enrolls in an event.
    /// Sent to all users enrolled in the event (WillAttend = true).
    /// </summary>
    /// <param name="event">The event being enrolled in</param>
    /// <param name="userDisplayName">The display name of the user who enrolled</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateEventEnrollmentNotification(Event @event, string userDisplayName, string baseUrl);

    /// <summary>
    /// Creates a push notification for when a user cancels their event enrollment.
    /// Sent to all other users enrolled in the event (WillAttend = true).
    /// </summary>
    /// <param name="event">The event enrollment is being cancelled for</param>
    /// <param name="userDisplayName">The display name of the user who cancelled</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateEventCancellationNotification(Event @event, string userDisplayName, string baseUrl);

    /// <summary>
    /// Creates a push notification when someone marks that they won't attend an event.
    /// Sent to all users enrolled in the event with WillAttend = true.
    /// </summary>
    /// <param name="event">The event being marked as not attending</param>
    /// <param name="userDisplayName">The display name of the user who won't attend</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateEventNonEnrollmentNotification(Event @event, string userDisplayName, string baseUrl);

    /// <summary>
    /// Creates a push notification for new 1st place in leaderboard.
    /// Sent to all users.
    /// </summary>
    /// <param name="userNickname">The nickname of the user who reached 1st place</param>
    /// <param name="level">The level the user is at</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateLeaderboardFirstPlaceNotification(string userNickname, int level, string baseUrl);

    /// <summary>
    /// Creates a push notification for new meetings.
    /// Recipients vary by meeting type (AGO/AGE exclude leitões, CV only for veterans).
    /// </summary>
    /// <param name="meeting">The meeting to notify about</param>
    /// <param name="isReminder">True if this is a reminder notification, false for new meeting notification</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateMeetingNotification(Meeting meeting, bool isReminder, string baseUrl);

    /// <summary>
    /// Creates a custom push notification for a meeting, mirroring the rehearsal custom notification pattern.
    /// Uses meeting context for URL and tag while allowing a custom body message.
    /// </summary>
    /// <param name="meeting">The meeting to notify about.</param>
    /// <param name="customBody">Custom message body provided by the user.</param>
    /// <param name="baseUrl">The base URL of the application.</param>
    /// <returns>A SendPushNotificationDto ready to be sent.</returns>
    SendPushNotificationDto CreateMeetingCustomNotification(Meeting meeting, string customBody, string baseUrl);

    /// <summary>
    /// Creates a push notification for new performance requests.
    /// Sent only to admins.
    /// </summary>
    /// <param name="request">The request to notify about</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRequestNotification(Request request, string baseUrl);

    /// <summary>
    /// Creates a push notification when someone marks attendance for a rehearsal.
    /// Sent to all users with pending attendance on that rehearsal (WillAttend = true).
    /// </summary>
    /// <param name="rehearsal">The rehearsal being attended</param>
    /// <param name="userDisplayName">The display name of the user who marked attendance</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalAttendanceNotification(Rehearsal rehearsal, string userDisplayName, string baseUrl);

    /// <summary>
    /// Creates a push notification when a user cancels their rehearsal attendance.
    /// Sent to all other users with pending attendance on that rehearsal (WillAttend = true).
    /// </summary>
    /// <param name="rehearsal">The rehearsal attendance is being cancelled for</param>
    /// <param name="userDisplayName">The display name of the user who cancelled</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalCancellationNotification(Rehearsal rehearsal, string userDisplayName, string baseUrl);

    /// <summary>
    /// Creates a push notification when someone marks that they won't attend a rehearsal.
    /// Sent to all users with pending attendance on that rehearsal (WillAttend = true).
    /// </summary>
    /// <param name="rehearsal">The rehearsal being marked as not attending</param>
    /// <param name="userDisplayName">The display name of the user who won't attend</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalNonAttendanceNotification(Rehearsal rehearsal, string userDisplayName, string baseUrl);

    /// <summary>
    /// Creates a push notification when admin approves a user's rehearsal attendance.
    /// </summary>
    /// <param name="rehearsal">The rehearsal</param>
    /// <param name="approverName">The name of the admin who approved</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalAttendanceApprovalNotification(Rehearsal rehearsal, string approverName, string baseUrl);

    /// <summary>
    /// Creates a push notification when admin rejects a user's rehearsal attendance.
    /// </summary>
    /// <param name="rehearsal">The rehearsal</param>
    /// <param name="rejectorName">The name of the admin who rejected</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalAttendanceRejectionNotification(Rehearsal rehearsal, string rejectorName, string baseUrl);

    /// <summary>
    /// Creates a push notification when someone comments on a user's leaderboard profile.
    /// </summary>
    /// <param name="authorName">The name of the comment author</param>
    /// <param name="targetUserName">The name of the user being commented on</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateLeaderboardCommentNotification(string authorName, string targetUserName, string baseUrl);

    /// <summary>
    /// Creates a push notification when someone likes a user's comment.
    /// </summary>
    /// <param name="likerName">The name of the user who liked the comment</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateLeaderboardCommentLikeNotification(string likerName, string baseUrl);

    /// <summary>
    /// Creates a push notification when someone uploads a video to an event.
    /// </summary>
    /// <param name="event">The event the video was uploaded to</param>
    /// <param name="uploaderName">The name of the user who uploaded the video</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateEventVideoUploadNotification(Event @event, string uploaderName, string baseUrl);

    /// <summary>
    /// Creates a push notification when someone uploads a video to a song.
    /// </summary>
    /// <param name="song">The song the video was uploaded to</param>
    /// <param name="uploaderName">The name of the user who uploaded the video</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateSongVideoUploadNotification(Song song, string uploaderName, string baseUrl);

    /// <summary>
    /// Creates a push notification for pending public request reminders (sent to admins daily).
    /// </summary>
    /// <param name="pendingCount">Number of pending public requests</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreatePendingPublicRequestsReminderNotification(int pendingCount, string baseUrl);

    /// <summary>
    /// Creates a push notification for pending meeting request reminders.
    /// </summary>
    /// <param name="meetingRequest">The pending meeting request</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreatePendingMeetingRequestReminderNotification(MeetingRequest meetingRequest, string baseUrl);

    /// <summary>
    /// Creates a push notification for pending rehearsal approvals.
    /// </summary>
    /// <param name="pendingRehearsalCount">Number of rehearsals with pending attendance approvals</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreatePendingRehearsalApprovalsReminderNotification(int pendingRehearsalCount, string baseUrl);

    /// <summary>
    /// Creates a push notification for a new question.
    /// </summary>
    /// <param name="questionTitle">Title of the question</param>
    /// <param name="authorName">Name of the user who asked the question</param>
    /// <param name="questionId">ID of the question</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateNewQuestionNotification(string questionTitle, string authorName, int questionId, string baseUrl);

    /// <summary>
    /// Creates a push notification for a question reply.
    /// </summary>
    /// <param name="replyPreview">Preview of the reply content</param>
    /// <param name="replyId">ID of the reply</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateQuestionReplyNotification(string replyPreview, int replyId, string baseUrl);

    /// <summary>
    /// Creates a push notification for a question reply from the assigned member.
    /// </summary>
    /// <param name="replyPreview">Preview of the reply content</param>
    /// <param name="replyId">ID of the reply</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateQuestionAnsweredNotification(string replyPreview, int replyId, string baseUrl);

    /// <summary>
    /// Creates a push notification for a question reminder.
    /// </summary>
    /// <param name="questionTitle">Title of the question</param>
    /// <param name="authorName">Name of the user who asked the question</param>
    /// <param name="questionId">ID of the question</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateQuestionReminderNotification(string questionTitle, string authorName, int questionId, string baseUrl);

    /// <summary>
    /// Creates a push notification for pending questions (background service).
    /// </summary>
    /// <param name="questionCount">Number of pending questions</param>
    /// <param name="firstAuthorNickname">Nickname of the first question's author</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreatePendingQuestionsNotification(int questionCount, string? firstAuthorNickname, string baseUrl);

    /// <summary>
    /// Creates a push notification when new naipe content (video or image) is published.
    /// Sent to all subscribed users.
    /// </summary>
    /// <param name="contentTitle">The title of the content</param>
    /// <param name="instrumentTypeName">The display name of the instrument type</param>
    /// <param name="isVideo">True if the content is a video, false if it's an image</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateNaipeContentNotification(string contentTitle, string instrumentTypeName, bool isVideo, string baseUrl);

    /// <summary>
    /// Creates a push notification for weekly summary of events, rehearsals, and meetings.
    /// Sent to all users every Monday.
    /// </summary>
    /// <param name="eventCount">Number of events this week</param>
    /// <param name="rehearsalCount">Number of rehearsals this week</param>
    /// <param name="meetingCount">Number of meetings this week</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateWeeklySummaryNotification(int eventCount, int rehearsalCount, int meetingCount, string baseUrl);

    /// <summary>
    /// Creates a push notification for a logistics card reminder.
    /// Sent to target users at the scheduled frequency.
    /// </summary>
    /// <param name="card">The logistics card to remind about</param>
    /// <param name="boardName">The name of the board containing the card</param>
    /// <param name="boardId">The ID of the board containing the card</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateCardReminderNotification(LogisticsCard card, string boardName, int boardId, string baseUrl);

    /// <summary>
    /// Creates a push notification for a new bet.
    /// Sent to all subscribed users.
    /// </summary>
    /// <param name="bet">The bet to notify about</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateBetNotification(Bet bet, string baseUrl);

    /// <summary>
    /// Creates a push notification for a bet reminder.
    /// Sent to all subscribed users.
    /// </summary>
    /// <param name="bet">The bet to remind about</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateBetReminderNotification(Bet bet, string baseUrl);

    /// <summary>
    /// Creates a push notification for calotes (debt) reminder.
    /// Sent to users who owe money to the tuna.
    /// </summary>
    /// <param name="amountOwed">The amount the user owes</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateCalotesReminderNotification(decimal amountOwed, string baseUrl);

    /// <summary>
    /// Creates a reminder push notification for a member to participate in activities.
    /// </summary>
    /// <param name="userDisplayName">Display name of the member receiving the reminder</param>
    /// <param name="userId">The member's user ID</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateMemberActivityReminderNotification(string userDisplayName, string userId, string baseUrl);
}

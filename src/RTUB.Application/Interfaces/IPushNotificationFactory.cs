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
    /// Creates a custom push notification for a rehearsal.
    /// </summary>
    /// <param name="rehearsal">The rehearsal to notify about</param>
    /// <param name="customBody">Custom message body provided by the user</param>
    /// <param name="baseUrl">The base URL of the application (e.g., "https://rtub.example.com")</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalNotification(Rehearsal rehearsal, string customBody, string baseUrl);

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
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateMeetingNotification(Meeting meeting, string baseUrl);

    /// <summary>
    /// Creates a push notification for new performance requests.
    /// Sent only to admins.
    /// </summary>
    /// <param name="request">The request to notify about</param>
    /// <param name="baseUrl">The base URL of the application</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRequestNotification(Request request, string baseUrl);

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
}

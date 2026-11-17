using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Factory for creating push notification DTOs for various notification scenarios.
/// Centralizes all push notification construction logic for consistency and reusability.
/// </summary>
public partial interface IPushNotificationFactory
{
    /// <summary>
    /// Creates a push notification for an event (new event or reminder).
    /// </summary>
    /// <param name="event">The event to notify about</param>
    /// <param name="isReminder">True if this is a reminder notification, false for new event notification</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateEventNotification(Event @event, bool isReminder);

    /// <summary>
    /// Creates a custom push notification for an event, similar to rehearsal custom notifications.
    /// Uses the event context for URL and tag but allows a fully custom body message.
    /// </summary>
    /// <param name="event">The event to notify about.</param>
    /// <param name="customBody">Custom message body provided by the user.</param>
    /// <returns>A SendPushNotificationDto ready to be sent.</returns>
    SendPushNotificationDto CreateEventCustomNotification(Event @event, string customBody);

    /// <summary>
    /// Creates a custom push notification for a rehearsal.
    /// </summary>
    /// <param name="rehearsal">The rehearsal to notify about</param>
    /// <param name="customBody">Custom message body provided by the user</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalNotification(Rehearsal rehearsal, string customBody);

    /// <summary>
    /// Creates a reminder push notification for a rehearsal (used by background schedulers).
    /// </summary>
    /// <param name="rehearsal">The rehearsal to remind about.</param>
    /// <returns>A SendPushNotificationDto ready to be sent.</returns>
    SendPushNotificationDto CreateRehearsalReminderNotification(Rehearsal rehearsal);

    /// <summary>
    /// Creates a push notification for new discussion posts.
    /// Sent to all users enrolled in the event.
    /// </summary>
    /// <param name="event">The event with the new discussion post</param>
    /// <param name="authorNickname">The nickname of the post author</param>
    /// <param name="postTitle">The title of the post</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateDiscussionPostNotification(Event @event, string authorNickname, string postTitle);

    /// <summary>
    /// Creates a push notification for new discussion comments.
    /// Sent to the post owner and any previous commenters.
    /// </summary>
    SendPushNotificationDto CreateDiscussionCommentNotification(Event @event, string authorNickname, string postTitle);

    /// <summary>
    /// Creates a push notification when someone enrolls in an event.
    /// Sent to all users enrolled in the event (WillAttend = true).
    /// </summary>
    /// <param name="event">The event being enrolled in</param>
    /// <param name="userDisplayName">The display name of the user who enrolled</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateEventEnrollmentNotification(Event @event, string userDisplayName);

    /// <summary>
    /// Creates a push notification for when a user cancels their event enrollment.
    /// Sent to all other users enrolled in the event (WillAttend = true).
    /// </summary>
    /// <param name="event">The event enrollment is being cancelled for</param>
    /// <param name="userDisplayName">The display name of the user who cancelled</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateEventCancellationNotification(Event @event, string userDisplayName);

    /// <summary>
    /// Creates a push notification when someone marks that they won't attend an event.
    /// Sent to all users enrolled in the event with WillAttend = true.
    /// </summary>
    /// <param name="event">The event being marked as not attending</param>
    /// <param name="userDisplayName">The display name of the user who won't attend</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateEventNonEnrollmentNotification(Event @event, string userDisplayName);

    /// <summary>
    /// Creates a push notification for new 1st place in leaderboard.
    /// Sent to all users.
    /// </summary>
    /// <param name="userNickname">The nickname of the user who reached 1st place</param>
    /// <param name="level">The level the user is at</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateLeaderboardFirstPlaceNotification(string userNickname, int level);

    /// <summary>
    /// Creates a push notification for new meetings.
    /// Recipients vary by meeting type (AGO/AGE exclude leitões, CV only for veterans).
    /// </summary>
    /// <param name="meeting">The meeting to notify about</param>
    /// <param name="isReminder">True if this is a reminder notification, false for new meeting notification</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateMeetingNotification(Meeting meeting, bool isReminder);

    /// <summary>
    /// Creates a custom push notification for a meeting, mirroring the rehearsal custom notification pattern.
    /// Uses meeting context for URL and tag while allowing a custom body message.
    /// </summary>
    /// <param name="meeting">The meeting to notify about.</param>
    /// <param name="customBody">Custom message body provided by the user.</param>
    /// <returns>A SendPushNotificationDto ready to be sent.</returns>
    SendPushNotificationDto CreateMeetingCustomNotification(Meeting meeting, string customBody);

    /// <summary>
    /// Creates a push notification for a new poll (Votação). Recipients are the poll's own audience
    /// (MeetingAccess.CanVoteInPoll): the caller picks them.
    /// </summary>
    /// <param name="poll">The poll that was created.</param>
    /// <param name="audienceLabel">The poll's body as members read it ("Conselho de Veteranos").</param>
    /// <returns>A SendPushNotificationDto ready to be sent.</returns>
    SendPushNotificationDto CreateMeetingPollNotification(MeetingPoll poll, string audienceLabel);

    /// <summary>
    /// Creates a push notification for new performance requests.
    /// Sent only to admins.
    /// </summary>
    /// <param name="request">The request to notify about</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRequestNotification(Request request);

    /// <summary>
    /// Creates a push notification when someone marks attendance for a rehearsal.
    /// Sent to all users with pending attendance on that rehearsal (WillAttend = true).
    /// </summary>
    /// <param name="rehearsal">The rehearsal being attended</param>
    /// <param name="userDisplayName">The display name of the user who marked attendance</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalAttendanceNotification(Rehearsal rehearsal, string userDisplayName);

    /// <summary>
    /// Creates a push notification when a user cancels their rehearsal attendance.
    /// Sent to all other users with pending attendance on that rehearsal (WillAttend = true).
    /// </summary>
    /// <param name="rehearsal">The rehearsal attendance is being cancelled for</param>
    /// <param name="userDisplayName">The display name of the user who cancelled</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalCancellationNotification(Rehearsal rehearsal, string userDisplayName);

    /// <summary>
    /// Creates a push notification when someone marks that they won't attend a rehearsal.
    /// Sent to all users with pending attendance on that rehearsal (WillAttend = true).
    /// </summary>
    /// <param name="rehearsal">The rehearsal being marked as not attending</param>
    /// <param name="userDisplayName">The display name of the user who won't attend</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalNonAttendanceNotification(Rehearsal rehearsal, string userDisplayName);

    /// <summary>
    /// Creates a push notification when admin approves a user's rehearsal attendance.
    /// </summary>
    /// <param name="rehearsal">The rehearsal</param>
    /// <param name="approverName">The name of the admin who approved</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalAttendanceApprovalNotification(Rehearsal rehearsal, string approverName);

    /// <summary>
    /// Creates a push notification when admin rejects a user's rehearsal attendance.
    /// </summary>
    /// <param name="rehearsal">The rehearsal</param>
    /// <param name="rejectorName">The name of the admin who rejected</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateRehearsalAttendanceRejectionNotification(Rehearsal rehearsal, string rejectorName);

    /// <summary>
    /// Creates a push notification when someone comments on a user's leaderboard profile.
    /// </summary>
    /// <param name="authorName">The name of the comment author</param>
    /// <param name="targetUserName">The name of the user being commented on</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateLeaderboardCommentNotification(string authorName, string targetUserName);

    /// <summary>
    /// Creates a push notification when someone likes a user's comment.
    /// </summary>
    /// <param name="likerName">The name of the user who liked the comment</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateLeaderboardCommentLikeNotification(string likerName);

    /// <summary>
    /// Creates a push notification when someone uploads a video to an event.
    /// </summary>
    /// <param name="event">The event the video was uploaded to</param>
    /// <param name="uploaderName">The name of the user who uploaded the video</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateEventVideoUploadNotification(Event @event, string uploaderName);

    /// <summary>
    /// Creates a push notification when someone uploads a video to a song.
    /// </summary>
    /// <param name="song">The song the video was uploaded to</param>
    /// <param name="uploaderName">The name of the user who uploaded the video</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    SendPushNotificationDto CreateSongVideoUploadNotification(Song song, string uploaderName);

    /// <summary>A song is new in Aprender (task 097); <paramref name="message"/> replaces the default text.</summary>
    SendPushNotificationDto CreateSongLearnNotification(int songId, string songTitle, string? message);
}

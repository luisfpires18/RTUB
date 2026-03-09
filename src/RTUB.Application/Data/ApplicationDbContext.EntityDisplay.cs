using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Data;

public partial class ApplicationDbContext
{
    /// <summary>
    /// Resolves a UserId to a user's nickname from the local cache.
    /// Returns null if user is not found in cache.
    /// </summary>
    private string? ResolveUserIdToNickname(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        var user = Users.Local.FirstOrDefault(u => u.Id == userId);
        return user?.Nickname ?? user?.UserName;
    }

    /// <summary>
    /// Resolves the display name for an entity based on its type and ID.
    /// Optimized to only use Local cache to avoid database queries during SaveChanges.
    /// </summary>
    private string? GetEntityDisplayName(EntityEntry entry)
    {
        var entityType = entry.Entity.GetType().Name;

        try
        {
            switch (entityType)
            {
                case "Song":
                    if (entry.Entity is Song song)
                        return song.Title;
                    break;

                case "Event":
                    if (entry.Entity is Event evt)
                        return evt.Name;
                    break;

                case "Enrollment":
                    if (entry.Entity is Enrollment enrollment)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var userName = enrollment.User?.Nickname
                            ?? enrollment.User?.UserName
                            ?? ResolveUserIdToNickname(enrollment.UserId);
                        var eventName = enrollment.Event?.Name
                            ?? Events.Local.FirstOrDefault(e => e.Id == enrollment.EventId)?.Name;
                        var attendStatus = enrollment.WillAttend ? "Vai" : "Não vai";

                        if (userName != null && eventName != null)
                            return $"{userName} - {eventName} - {attendStatus}";
                        if (eventName != null)
                            return $"{eventName} - {attendStatus}";
                        if (userName != null)
                            return $"{userName} - {attendStatus}";
                        return null; // Neither user name nor event found - will fall back to entity ID display
                    }
                    break;

                case "EventRepertoire":
                    if (entry.Entity is EventRepertoire repertoire)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var eventName = repertoire.Event?.Name
                            ?? Events.Local.FirstOrDefault(e => e.Id == repertoire.EventId)?.Name;
                        var songTitle = repertoire.Song?.Title
                            ?? Songs.Local.FirstOrDefault(s => s.Id == repertoire.SongId)?.Title;

                        if (eventName != null && songTitle != null)
                            return $"{eventName} - {songTitle}";
                        return eventName ?? songTitle; // Return partial if one is missing
                    }
                    break;

                case "RehearsalAttendance":
                    if (entry.Entity is RehearsalAttendance attendance)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var userName = attendance.User?.Nickname
                            ?? attendance.User?.UserName
                            ?? ResolveUserIdToNickname(attendance.UserId);
                        var rehearsal = attendance.Rehearsal
                            ?? Rehearsals.Local.FirstOrDefault(r => r.Id == attendance.RehearsalId);
                        var attendStatus = attendance.WillAttend ? "Vai" : "Não vai";

                        if (userName != null && rehearsal != null)
                            return $"{userName} - {rehearsal.Date:yyyy-MM-dd} - {attendStatus}";
                        if (rehearsal != null)
                            return $"{rehearsal.Date:yyyy-MM-dd} - {attendStatus}";
                        if (userName != null)
                            return $"{userName} - {attendStatus}";
                        return null; // Neither user name nor rehearsal found - will fall back to entity ID display
                    }
                    break;

                case "MeetingParticipation":
                    if (entry.Entity is MeetingParticipation meetingParticipation)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var userName = meetingParticipation.User?.Nickname
                            ?? meetingParticipation.User?.UserName
                            ?? ResolveUserIdToNickname(meetingParticipation.UserId);
                        var participationMeeting = meetingParticipation.Meeting
                            ?? Meetings.Local.FirstOrDefault(m => m.Id == meetingParticipation.MeetingId);
                        var attendStatus = meetingParticipation.WillAttend ? "Vai" : "Não vai";

                        if (userName != null && participationMeeting != null)
                            return $"{userName} - {participationMeeting.Title} - {attendStatus}";
                        if (participationMeeting != null)
                            return $"{participationMeeting.Title} - {attendStatus}";
                        if (userName != null)
                            return $"{userName} - {attendStatus}";
                        return null; // Neither user name nor meeting found - will fall back to entity ID display
                    }
                    break;

                case "RoleAssignment":
                    if (entry.Entity is RoleAssignment roleAssignment)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var userName = roleAssignment.User?.Nickname
                            ?? roleAssignment.User?.UserName
                            ?? ResolveUserIdToNickname(roleAssignment.UserId)
                            ?? roleAssignment.UserId;
                        return $"{userName} - {roleAssignment.Position}";
                    }
                    break;

                case "SongYouTubeUrl":
                    if (entry.Entity is SongYouTubeUrl youtubeUrl)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var songTitle = youtubeUrl.Song?.Title
                            ?? Songs.Local.FirstOrDefault(s => s.Id == youtubeUrl.SongId)?.Title;
                        return songTitle;
                    }
                    break;

                case "Transaction":
                    if (entry.Entity is Transaction transaction && transaction.ActivityId.HasValue)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var activityName = transaction.Activity?.Name
                            ?? Activities.Local.FirstOrDefault(a => a.Id == transaction.ActivityId.Value)?.Name;
                        return activityName;
                    }
                    break;

                case "Activity":
                    if (entry.Entity is Activity activity2)
                        return activity2.Name;
                    break;

                case "Album":
                    if (entry.Entity is Album album)
                        return album.Title;
                    break;

                case "Instrument":
                    if (entry.Entity is Instrument instrument)
                        return $"{instrument.Category} - {instrument.Name}";
                    break;

                case "Label":
                    if (entry.Entity is Label label && !string.IsNullOrEmpty(label.Content))
                    {
                        return label.Content.Length > 100
                            ? label.Content[..100] + "..."
                            : label.Content;
                    }
                    break;

                case "Product":
                    if (entry.Entity is Product product)
                        return product.Name;
                    break;

                case "Rehearsal":
                    if (entry.Entity is Rehearsal rehearsal2)
                        return rehearsal2.Date.ToString("yyyy-MM-dd");
                    break;

                case "Report":
                    if (entry.Entity is Report report)
                        return report.Title;
                    break;

                case "MbwayTransfer":
                    if (entry.Entity is MbwayTransfer mbwayTransfer)
                        return $"MBWAY - {mbwayTransfer.Date:dd/MM/yyyy} €{mbwayTransfer.Amount:F2}";
                    break;

                case "NerbaOrder":
                    if (entry.Entity is NerbaOrder nerbaOrder)
                        return $"Nerba - {nerbaOrder.Item} (x{nerbaOrder.Stock})";
                    break;

                case "Request":
                    // Request doesn't have a specific name field, use ID
                    return null;

                case "Slideshow":
                    if (entry.Entity is Slideshow slideshow)
                        return slideshow.Title;
                    break;

                case "LogisticsBoard":
                    if (entry.Entity is LogisticsBoard logisticsBoard)
                        return logisticsBoard.Name;
                    break;

                case "LogisticsList":
                    if (entry.Entity is LogisticsList logisticsList)
                        return logisticsList.Name;
                    break;

                case "LogisticsCard":
                    if (entry.Entity is LogisticsCard logisticsCard)
                        return logisticsCard.Title;
                    break;

                case "Meeting":
                    if (entry.Entity is Meeting meeting)
                        return meeting.Title;
                    break;

                case "MeetingRequest":
                    if (entry.Entity is MeetingRequest meetingRequest)
                        return meetingRequest.Title;
                    break;

                case "LeaderboardComment":
                    if (entry.Entity is LeaderboardComment leaderboardComment)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var targetName = leaderboardComment.TargetUser?.Nickname
                            ?? leaderboardComment.TargetUser?.UserName
                            ?? ResolveUserIdToNickname(leaderboardComment.TargetUserId)
                            ?? leaderboardComment.TargetUserId;
                        var authorName = leaderboardComment.Author?.Nickname
                            ?? leaderboardComment.Author?.UserName
                            ?? ResolveUserIdToNickname(leaderboardComment.AuthorId)
                            ?? leaderboardComment.AuthorId;
                        return $"{authorName} → {targetName}";
                    }
                    break;

                case "LeaderboardCommentLike":
                    if (entry.Entity is LeaderboardCommentLike commentLike)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var userName = commentLike.User?.Nickname
                            ?? commentLike.User?.UserName
                            ?? ResolveUserIdToNickname(commentLike.UserId)
                            ?? commentLike.UserId;
                        var likedComment = commentLike.Comment
                            ?? LeaderboardComments.Local.FirstOrDefault(c => c.Id == commentLike.CommentId);
                        if (likedComment != null)
                        {
                            var targetName = likedComment.TargetUser?.Nickname
                                ?? likedComment.TargetUser?.UserName
                                ?? ResolveUserIdToNickname(likedComment.TargetUserId)
                                ?? likedComment.TargetUserId;
                            var commentPreview = likedComment.Text.Length > 30
                                ? likedComment.Text[..30] + "..."
                                : likedComment.Text;
                            return $"{userName} liked {targetName}'s comment: {commentPreview}";
                        }
                        return $"{userName} liked comment";
                    }
                    break;

                case "Post":
                    if (entry.Entity is Post post)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var postDiscussion = post.Discussion
                            ?? Discussions.Local.FirstOrDefault(d => d.Id == post.DiscussionId);
                        if (postDiscussion != null)
                        {
                            var postEvent = postDiscussion.Event
                                ?? Events.Local.FirstOrDefault(e => e.Id == postDiscussion.EventId);
                            if (postEvent != null)
                                return $"{postEvent.Name} - {post.Title}";
                        }
                        return post.Title;
                    }
                    break;

                case "Comment":
                    if (entry.Entity is Comment comment)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var authorName = comment.Author?.Nickname
                            ?? comment.Author?.UserName
                            ?? ResolveUserIdToNickname(comment.AuthorId)
                            ?? comment.AuthorId;
                        var bodyPreview = comment.Body.Length > 50
                            ? comment.Body[..50] + "..."
                            : comment.Body;

                        // Try to get event name through Post -> Discussion -> Event
                        var commentPost = comment.Post
                            ?? Posts.Local.FirstOrDefault(p => p.Id == comment.PostId);
                        if (commentPost != null)
                        {
                            var commentDiscussion = commentPost.Discussion
                                ?? Discussions.Local.FirstOrDefault(d => d.Id == commentPost.DiscussionId);
                            if (commentDiscussion != null)
                            {
                                var commentEvent = commentDiscussion.Event
                                    ?? Events.Local.FirstOrDefault(e => e.Id == commentDiscussion.EventId);
                                if (commentEvent != null)
                                    return $"{commentEvent.Name} - {authorName}: {bodyPreview}";
                            }
                        }

                        return $"{authorName}: {bodyPreview}";
                    }
                    break;

                case "MemberInstrument":
                    if (entry.Entity is MemberInstrument memberInstrument)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var userName = memberInstrument.Member?.Nickname
                            ?? memberInstrument.Member?.UserName
                            ?? ResolveUserIdToNickname(memberInstrument.MemberId)
                            ?? memberInstrument.MemberId;
                        var instrumentName = InstrumentTypeHelper.GetDisplayName(memberInstrument.InstrumentType);
                        return $"{userName} - {instrumentName}";
                    }
                    break;

                case "PushSubscription":
                    if (entry.Entity is PushSubscription pushSubscription)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var userName = pushSubscription.User?.Nickname
                            ?? pushSubscription.User?.UserName
                            ?? ResolveUserIdToNickname(pushSubscription.UserId)
                            ?? pushSubscription.UserId;
                        return userName;
                    }
                    break;

                case "Question":
                    if (entry.Entity is Question question)
                        return question.Title;
                    break;

                case "Bet":
                    if (entry.Entity is Bet bet)
                        return bet.Title;
                    break;

                case "UserBet":
                    if (entry.Entity is UserBet userBet)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var betEntity2 = userBet.Bet
                            ?? Bets.Local.FirstOrDefault(b => b.Id == userBet.BetId);
                        var betOptionEntity = userBet.BetOption
                            ?? BetOptions.Local.FirstOrDefault(o => o.Id == userBet.BetOptionId);
                        var betName2 = betEntity2?.Title ?? $"Aposta #{userBet.BetId}";
                        var optionName = betOptionEntity?.Title ?? $"Opção #{userBet.BetOptionId}";
                        return $"{betName2} - {optionName}";
                    }
                    break;

                case "BetComment":
                    if (entry.Entity is BetComment betComment)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var authorName = betComment.Author?.Nickname
                            ?? betComment.Author?.UserName
                            ?? ResolveUserIdToNickname(betComment.AuthorId)
                            ?? betComment.AuthorId;
                        var betEntity = betComment.Bet
                            ?? Bets.Local.FirstOrDefault(b => b.Id == betComment.BetId);
                        var betName = betEntity?.Title ?? $"Aposta #{betComment.BetId}";
                        var textPreview = string.IsNullOrEmpty(betComment.Text)
                            ? "[Media]"
                            : (betComment.Text.Length > 50 ? betComment.Text[..50] + "..." : betComment.Text);
                        return $"{authorName} em {betName}: {textPreview}";
                    }
                    break;

                case "GameScore":
                    if (entry.Entity is GameScore gameScore)
                    {
                        // Try to get the game title from local cache based on GameKey
                        var game = Games.Local.FirstOrDefault(g => g.Key == gameScore.GameKey);
                        var gameName = game?.Title ?? gameScore.GameKey;
                        return gameName;
                    }
                    break;

                case "Character":
                    if (entry.Entity is Character character)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var userName = character.User?.Nickname
                            ?? character.User?.UserName
                            ?? ResolveUserIdToNickname(character.UserId)
                            ?? character.UserId;
                        return $"Character - {userName} (Level {character.Level})";
                    }
                    break;
            }
        }
        catch (InvalidOperationException)
        {
            // If resolution fails due to database query issues, return null (will fall back to ID display)
            return null;
        }

        return null;
    }
}

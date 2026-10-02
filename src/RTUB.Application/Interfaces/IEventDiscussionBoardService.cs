using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// An event's conversation behind the React /events/{id}/discussion (React track 013). Every rule is decided
/// here from the caller; writes go through the existing Post, Comment, Transportation and Mention services, so
/// their push notifications are unchanged. Every write answers the whole conversation.
/// </summary>
public interface IEventDiscussionBoardService
{
    Task<EventResult<EventDiscussionDto>> GetAsync(int eventId, ClaimsPrincipal user);

    Task<EventResult<EventDiscussionDto>> AddPostAsync(int eventId, EventPostInput input, ClaimsPrincipal user);

    Task<EventResult<EventDiscussionDto>> EditPostAsync(int eventId, int postId, EventPostInput input, ClaimsPrincipal user);

    Task<EventResult<EventDiscussionDto>> DeletePostAsync(int eventId, int postId, ClaimsPrincipal user);

    Task<EventResult<EventDiscussionDto>> SetFlagsAsync(int eventId, int postId, EventPostFlagsInput input, ClaimsPrincipal user);

    Task<EventResult<EventDiscussionDto>> AddCommentAsync(int eventId, int postId, EventCommentInput input, ClaimsPrincipal user);

    Task<EventResult<EventDiscussionDto>> EditCommentAsync(int eventId, int commentId, EventCommentInput input, ClaimsPrincipal user);

    Task<EventResult<EventDiscussionDto>> DeleteCommentAsync(int eventId, int commentId, ClaimsPrincipal user);

    Task<EventResult<EventDiscussionDto>> AddTransportAsync(int eventId, EventTransportInput input, ClaimsPrincipal user);

    Task<EventResult<EventDiscussionDto>> EditTransportAsync(int eventId, int postId, EventTransportInput input, ClaimsPrincipal user);

    /// <summary>Members the driver (or Admin/Owner) may seat: not expelled, not already in that car.</summary>
    Task<EventResult<IReadOnlyList<EventMemberOptionDto>>> SearchPassengersAsync(int eventId, int postId, string? query, ClaimsPrincipal user);

    Task<EventResult<EventDiscussionDto>> AddPassengerAsync(int eventId, int postId, EventPassengerInput input, ClaimsPrincipal user);

    Task<EventResult<EventDiscussionDto>> RemovePassengerAsync(int eventId, int postId, int passengerId, ClaimsPrincipal user);
}

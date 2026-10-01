using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Admin/Owner event management beyond create/edit/delete (React track 012A, docs/react-events.md):
/// the image, cancel / reactivate, and the email and push notices the old /member/events offered.
/// Every call decides who may do it from the caller; the existing event, storage, email and push
/// services do the work, so their behaviour is unchanged.
/// </summary>
public interface IEventAdminService
{
    /// <summary>Replaces the event's image (the old one is deleted from storage, as before).</summary>
    Task<EventResult<bool>> SetImageAsync(int id, EventImageUpload image, ClaimsPrincipal user);

    /// <summary>Removes the event's image (new in 012A; the old page could only replace it).</summary>
    Task<EventResult<bool>> RemoveImageAsync(int id, ClaimsPrincipal user);

    /// <summary>
    /// Cancels an upcoming event with a reason. As before, this deletes every enrollment; the email to
    /// subscribers is optional and never undoes the cancellation if it fails.
    /// </summary>
    Task<EventResult<EventNoticeResultDto>> CancelAsync(int id, EventCancelInput input, ClaimsPrincipal user, string baseUrl);

    /// <summary>Reactivates a cancelled upcoming event. No notice, and the deleted enrollments do not come back.</summary>
    Task<EventResult<bool>> ReactivateAsync(int id, ClaimsPrincipal user);

    /// <summary>The counts the notice modal shows before anything is sent.</summary>
    Task<EventResult<EventNoticeAudienceDto>> GetNoticeAudienceAsync(int id, ClaimsPrincipal user);

    /// <summary>Sends one email or push notice about an upcoming, not cancelled event.</summary>
    Task<EventResult<EventNoticeResultDto>> SendNoticeAsync(int id, EventNoticeInput input, ClaimsPrincipal user, string baseUrl);

    /// <summary>The event's prizes with their ids, for the management modal (012B).</summary>
    Task<EventResult<IReadOnlyList<EventPrizeDto>>> GetPrizesAsync(int id, ClaimsPrincipal user);

    /// <summary>Adds a prize to a past festival, as the old page allowed; answers the event's prizes.</summary>
    Task<EventResult<IReadOnlyList<EventPrizeDto>>> AddPrizeAsync(int id, EventPrizeInput input, ClaimsPrincipal user);

    /// <summary>Renames one of the event's prizes; answers the event's prizes.</summary>
    Task<EventResult<IReadOnlyList<EventPrizeDto>>> UpdatePrizeAsync(int id, int prizeId, EventPrizeInput input, ClaimsPrincipal user);

    /// <summary>Deletes one of the event's prizes (a hard delete, as before); answers the prizes left.</summary>
    Task<EventResult<IReadOnlyList<EventPrizeDto>>> DeletePrizeAsync(int id, int prizeId, ClaimsPrincipal user);

    /// <summary>The event's videos in their order, for the management modal (012C).</summary>
    Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> GetVideosAsync(int id, ClaimsPrincipal user);

    /// <summary>
    /// Uploads a video to a past event through the existing storage and <see cref="IEventService.AddVideoAsync"/>
    /// (same key convention, same push to the other members); answers the event's videos.
    /// </summary>
    Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> AddVideoAsync(int id, EventVideoUpload upload, ClaimsPrincipal user);

    /// <summary>Renames one of the event's videos; answers the event's videos.</summary>
    Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> RenameVideoAsync(int id, int videoId, EventVideoTitleInput input, ClaimsPrincipal user);

    /// <summary>Sets the event's video order; answers the event's videos.</summary>
    Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> ReorderVideosAsync(int id, EventVideoOrderInput input, ClaimsPrincipal user);

    /// <summary>Deletes a video: its stored file (when this environment owns it) and its row, as before; answers the videos left.</summary>
    Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> DeleteVideoAsync(int id, int videoId, ClaimsPrincipal user);
}

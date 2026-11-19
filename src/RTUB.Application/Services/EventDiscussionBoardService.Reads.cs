// Event discussion board: board loading and result helpers.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class EventDiscussionBoardService
{
    // ---------- reads ----------

    private sealed record PostFacts(string AuthorId, bool Pinned, bool Locked, int? TransportId, int Passengers)
    {
        public bool HasTransport => TransportId is not null;
    }

    /// <summary>A live post of this event's discussion, or null (a post id only counts under its own event).</summary>
    private async Task<PostFacts?> PostAsync(int eventId, int postId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Posts.AsNoTracking()
            .Where(p => p.Id == postId && !p.IsDeleted && p.Discussion.EventId == eventId)
            .Select(p => new PostFacts(p.AuthorId, p.IsPinned, p.IsLocked,
                p.Transportation == null ? null : p.Transportation.Id,
                p.Transportation == null ? 0 : p.Transportation.Passengers.Count))
            .FirstOrDefaultAsync();
    }

    private async Task<string?> CommentAuthorAsync(int eventId, int commentId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Comments.AsNoTracking()
            .Where(c => c.Id == commentId && !c.IsDeleted && !c.Post.IsDeleted && c.Post.Discussion.EventId == eventId)
            .Select(c => c.AuthorId)
            .FirstOrDefaultAsync();
    }

    private async Task<EventDiscussionDto> LoadAsync(int eventId, ClaimsPrincipal user)
    {
        var me = user.UserId();
        var moderator = EventsAuthorization.CanManage(user);
        await using var db = await _contexts.CreateDbContextAsync();
        // ponytail: the whole conversation at once (≤6 posts per event today); page it if one grows large.
        var posts = await db.Posts.AsNoTracking()
            .Where(p => p.Discussion.EventId == eventId && !p.IsDeleted)
            .Include(p => p.Author)
            .Include(p => p.Comments.Where(c => !c.IsDeleted)).ThenInclude(c => c.Author)
            .Include(p => p.Transportation).ThenInclude(t => t!.Passengers).ThenInclude(x => x.Passenger)
            .AsSplitQuery()
            .ToListAsync();

        return new EventDiscussionDto(moderator, posts
            .OrderByDescending(p => p.IsPinned).ThenByDescending(p => p.LastActivityAt).ThenByDescending(p => p.Id)
            .Select(p => new EventPostDto(
                p.Id,
                p.Title,
                p.Body,
                Author(p.Author),
                Utc(p.CreatedAt),
                Utc(p.LastActivityAt),
                p.IsEdited,
                p.IsPinned,
                p.IsLocked,
                p.AuthorId == me,
                p.AuthorId == me,
                CanDelete(p.AuthorId, user),
                !p.IsLocked,
                p.Comments.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                    .Select(c => new EventCommentDto(c.Id, c.Body, Author(c.Author), Utc(c.CreatedAt), c.IsEdited,
                        c.AuthorId == me, CanDelete(c.AuthorId, user)))
                    .ToList(),
                p.Transportation is not { } t ? null : new EventTransportDto(
                    t.VehicleDescription,
                    t.TotalSeats,
                    t.Notes,
                    CanSeat(p.AuthorId, user),
                    t.Passengers.Where(x => x.Passenger != null).OrderBy(x => x.Id)
                        .Select(x => new EventPassengerDto(x.Id, Author(x.Passenger), x.PassengerId == me,
                            CanSeat(p.AuthorId, user) || x.PassengerId == me))
                        .ToList())))
            .ToList());
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static EventAuthorDto Author(ApplicationUser? u) => u is null
        ? new EventAuthorDto("Membro", null, EventAgendaService.DefaultAvatar, null)
        : Author(u.Nickname, u.FirstName, u.LastName, u.ImageUrl, u.Categories, u.Positions);

    /// <summary>Nickname first, as "Quem vai" shows people; Magister, else the primary category, as the badge.</summary>
    internal static EventAuthorDto Author(string? nickname, string? first, string? last, string? imageUrl,
        List<MemberCategory>? categories, List<Position>? positions)
    {
        var fullName = $"{first} {last}".Trim();
        var category = new ApplicationUser { Categories = categories ?? new List<MemberCategory>() }.GetPrimaryCategory();
        return new EventAuthorDto(
            string.IsNullOrWhiteSpace(nickname) ? (fullName.Length == 0 ? "Membro" : fullName) : nickname,
            fullName.Length == 0 ? null : fullName,
            EventAgendaService.IsSafeUrl(imageUrl) ? imageUrl! : EventAgendaService.DefaultAvatar,
            positions?.Contains(Position.Magister) == true ? "MAGISTER"
                : category is { } c ? StatusHelper.GetCategoryDisplay(c) : null);
    }

    private static EventResult<EventDiscussionDto> Ok(EventDiscussionDto value) => EventResult<EventDiscussionDto>.Ok(value);

    private static EventResult<EventDiscussionDto> Fail(EventResultStatus status) => EventResult<EventDiscussionDto>.Fail(status);

    private static EventResult<EventDiscussionDto> Invalid(string field, string message) => EventResult<EventDiscussionDto>.Invalid(field, message);
}

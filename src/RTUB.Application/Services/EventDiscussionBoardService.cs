using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The React event discussion page (React track 013, docs/react-events.md).
/// Same rules, now enforced here instead of only hidden in the UI:
/// - any signed-in member reads, posts, comments (not on a locked post) and offers a lift;
/// - authors edit their own posts, comments and lift offers;
/// - authors delete their own; the Owner deletes anyone's (as before; Admin did not);
/// - Admin/Owner pin and lock posts and manage any car's passengers;
/// - the driver seats and unseats passengers; a passenger may leave.
/// Writes reuse PostService / CommentService (push to the event's members and to mentions, as before),
/// TransportationService and MentionService. No schema change.
/// </summary>
public sealed partial class EventDiscussionBoardService : IEventDiscussionBoardService
{
    public const int TitleMin = 3;

    public const int TitleMax = 120;

    public const int BodyMin = 3;

    public const int BodyMax = 5000;

    public const int CommentMax = 2000;

    public const int VehicleMax = 200;

    public const int TransportNotesMax = 500;

    public const int SearchLimit = 20;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    private readonly IDiscussionService _discussions;

    private readonly IPostService _posts;

    private readonly ICommentService _comments;

    private readonly ITransportationService _transport;

    private readonly IMentionService _mentions;

    public EventDiscussionBoardService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IDiscussionService discussions,
        IPostService posts,
        ICommentService comments,
        ITransportationService transport,
        IMentionService mentions)
    {
        _contexts = contexts;
        _discussions = discussions;
        _posts = posts;
        _comments = comments;
        _transport = transport;
        _mentions = mentions;
    }

    public async Task<EventResult<EventDiscussionDto>> GetAsync(int eventId, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        return Ok(await LoadAsync(eventId, user));
    }

    public async Task<EventResult<EventDiscussionDto>> AddPostAsync(int eventId, EventPostInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        if (ValidatePost(input) is not { } valid)
        {
            return PostErrors(input);
        }

        var discussion = await _discussions.GetOrCreateForEventAsync(eventId);
        var mentions = await _mentions.ParseAndResolveAsync(valid.Body);
        await _posts.CreateAsync(discussion.Id, Me(user), valid.Title, valid.Body, mentions);
        return Ok(await LoadAsync(eventId, user));
    }

    public async Task<EventResult<EventDiscussionDto>> EditPostAsync(int eventId, int postId, EventPostInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        var post = await PostAsync(eventId, postId);
        if (post is null || post.HasTransport)
        {
            return Fail(EventResultStatus.NotFound);
        }

        if (post.AuthorId != Me(user))
        {
            return Fail(EventResultStatus.Forbidden);
        }

        if (ValidatePost(input) is not { } valid)
        {
            return PostErrors(input);
        }

        await _posts.UpdateAsync(postId, valid.Title, valid.Body);
        return Ok(await LoadAsync(eventId, user));
    }

    public async Task<EventResult<EventDiscussionDto>> DeletePostAsync(int eventId, int postId, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        var post = await PostAsync(eventId, postId);
        if (post is null)
        {
            return Fail(EventResultStatus.NotFound);
        }

        if (!CanDelete(post.AuthorId, user))
        {
            return Fail(EventResultStatus.Forbidden);
        }

        await _posts.SoftDeleteAsync(postId);
        return Ok(await LoadAsync(eventId, user));
    }

    public async Task<EventResult<EventDiscussionDto>> SetFlagsAsync(int eventId, int postId, EventPostFlagsInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        var post = await PostAsync(eventId, postId);
        if (post is null)
        {
            return Fail(EventResultStatus.NotFound);
        }

        if (!EventsAuthorization.CanManage(user))
        {
            return Fail(EventResultStatus.Forbidden);
        }

        if (input.Pinned != post.Pinned)
        {
            await (input.Pinned ? _posts.PinAsync(postId) : _posts.UnpinAsync(postId));
        }

        if (input.Locked != post.Locked)
        {
            await (input.Locked ? _posts.LockAsync(postId) : _posts.UnlockAsync(postId));
        }

        return Ok(await LoadAsync(eventId, user));
    }

    public async Task<EventResult<EventDiscussionDto>> AddCommentAsync(int eventId, int postId, EventCommentInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        var post = await PostAsync(eventId, postId);
        if (post is null)
        {
            return Fail(EventResultStatus.NotFound);
        }

        if (post.Locked)
        {
            return Invalid("body", "Esta conversa está fechada a novos comentários.");
        }

        if (CommentError(input.Body) is { } error)
        {
            return Invalid("body", error);
        }

        var body = input.Body!.Trim();
        await _comments.CreateAsync(postId, Me(user), body, await _mentions.ParseAndResolveAsync(body));
        return Ok(await LoadAsync(eventId, user));
    }

    public async Task<EventResult<EventDiscussionDto>> EditCommentAsync(int eventId, int commentId, EventCommentInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        var authorId = await CommentAuthorAsync(eventId, commentId);
        if (authorId is null)
        {
            return Fail(EventResultStatus.NotFound);
        }

        if (authorId != Me(user))
        {
            return Fail(EventResultStatus.Forbidden);
        }

        if (CommentError(input.Body) is { } error)
        {
            return Invalid("body", error);
        }

        await _comments.UpdateAsync(commentId, input.Body!.Trim());
        return Ok(await LoadAsync(eventId, user));
    }

    public async Task<EventResult<EventDiscussionDto>> DeleteCommentAsync(int eventId, int commentId, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        var authorId = await CommentAuthorAsync(eventId, commentId);
        if (authorId is null)
        {
            return Fail(EventResultStatus.NotFound);
        }

        if (!CanDelete(authorId, user))
        {
            return Fail(EventResultStatus.Forbidden);
        }

        await _comments.SoftDeleteAsync(commentId);
        return Ok(await LoadAsync(eventId, user));
    }
}

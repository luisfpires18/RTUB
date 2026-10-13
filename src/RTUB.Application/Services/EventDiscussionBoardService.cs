using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /events/{id}/discussion behind the React page (React track 013, docs/react-events.md).
/// Same rules, now enforced here instead of only hidden in the UI:
/// - any signed-in member reads, posts, comments (not on a locked post) and offers a lift;
/// - authors edit their own posts, comments and lift offers;
/// - authors delete their own; the Owner deletes anyone's (as before; Admin did not);
/// - Admin/Owner pin and lock posts and manage any car's passengers;
/// - the driver seats and unseats passengers; a passenger may leave.
/// Writes reuse PostService / CommentService (push to the event's members and to mentions, as before),
/// TransportationService and MentionService. No schema change.
/// </summary>
public sealed class EventDiscussionBoardService : IEventDiscussionBoardService
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

    public async Task<EventResult<EventDiscussionDto>> AddTransportAsync(int eventId, EventTransportInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        if (TransportErrors(input, 0) is { Count: > 0 } errors)
        {
            return new EventResult<EventDiscussionDto>(EventResultStatus.Invalid, Errors: errors);
        }

        // As before: the offer is a post (title = the car, text = the notes or "Transporte") plus its seats.
        var vehicle = input.Vehicle!.Trim();
        var notes = Blank(input.Notes);
        var discussion = await _discussions.GetOrCreateForEventAsync(eventId);
        var post = await _posts.CreateAsync(discussion.Id, Me(user), vehicle, notes ?? "Transporte");
        await _transport.CreateForPostAsync(post.Id, vehicle, input.Seats, notes);
        return Ok(await LoadAsync(eventId, user));
    }

    public async Task<EventResult<EventDiscussionDto>> EditTransportAsync(int eventId, int postId, EventTransportInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        var post = await PostAsync(eventId, postId);
        if (post?.TransportId is not { } transportId)
        {
            return Fail(EventResultStatus.NotFound);
        }

        if (post.AuthorId != Me(user))
        {
            return Fail(EventResultStatus.Forbidden);
        }

        if (TransportErrors(input, post.Passengers) is { Count: > 0 } errors)
        {
            return new EventResult<EventDiscussionDto>(EventResultStatus.Invalid, Errors: errors);
        }

        var vehicle = input.Vehicle!.Trim();
        var notes = Blank(input.Notes);
        await _transport.UpdateAsync(transportId, vehicle, input.Seats, notes);
        await _posts.UpdateAsync(postId, vehicle, notes ?? "Transporte");
        return Ok(await LoadAsync(eventId, user));
    }

    public async Task<EventResult<IReadOnlyList<EventMemberOptionDto>>> SearchPassengersAsync(int eventId, int postId, string? query, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return EventResult<IReadOnlyList<EventMemberOptionDto>>.Fail(refused.Status);
        }

        var post = await PostAsync(eventId, postId);
        if (post?.TransportId is not { } transportId)
        {
            return EventResult<IReadOnlyList<EventMemberOptionDto>>.Fail(EventResultStatus.NotFound);
        }

        if (!CanSeat(post.AuthorId, user))
        {
            return EventResult<IReadOnlyList<EventMemberOptionDto>>.Fail(EventResultStatus.Forbidden);
        }

        var q = EventParticipantsAdminService.Fold(query);
        if (q.Length == 0)
        {
            return EventResult<IReadOnlyList<EventMemberOptionDto>>.Ok(Array.Empty<EventMemberOptionDto>());
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var seated = db.TransportationPassengers.Where(p => p.TransportationId == transportId).Select(p => p.PassengerId);
        // ponytail: every member in memory (~100) to fold accents the Portuguese way; page it if it grows.
        var members = await db.Users.AsNoTracking()
            .Where(u => !u.IsExpelled && !seated.Contains(u.Id))
            .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl })
            .ToListAsync();

        return EventResult<IReadOnlyList<EventMemberOptionDto>>.Ok(members
            .Select(u => (u, full: $"{u.FirstName} {u.LastName}".Trim()))
            .Where(x => EventParticipantsAdminService.Fold(x.u.Nickname).Contains(q) || EventParticipantsAdminService.Fold(x.full).Contains(q))
            .OrderBy(x => string.IsNullOrWhiteSpace(x.u.Nickname) ? x.full : x.u.Nickname, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .Take(SearchLimit)
            .Select(x =>
            {
                var a = Author(x.u.Nickname, x.u.FirstName, x.u.LastName, x.u.ImageUrl, null, null);
                return new EventMemberOptionDto(x.u.Id, a.Name, a.FullName, a.AvatarUrl);
            })
            .ToList());
    }

    public async Task<EventResult<EventDiscussionDto>> AddPassengerAsync(int eventId, int postId, EventPassengerInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        var post = await PostAsync(eventId, postId);
        if (post?.TransportId is not { } transportId)
        {
            return Fail(EventResultStatus.NotFound);
        }

        if (!CanSeat(post.AuthorId, user))
        {
            return Fail(EventResultStatus.Forbidden);
        }

        await using (var db = await _contexts.CreateDbContextAsync())
        {
            if (string.IsNullOrWhiteSpace(input.UserId)
                || !await db.Users.AnyAsync(u => u.Id == input.UserId && !u.IsExpelled))
            {
                return Invalid("userId", "Escolha um membro da lista.");
            }
        }

        try
        {
            await _transport.AddPassengerAsync(transportId, input.UserId);
        }
        catch (InvalidOperationException ex)
        {
            // "already in this car" / "no seats left", from TransportationService.
            return Invalid("userId", ex.Message);
        }

        return Ok(await LoadAsync(eventId, user));
    }

    public async Task<EventResult<EventDiscussionDto>> RemovePassengerAsync(int eventId, int postId, int passengerId, ClaimsPrincipal user)
    {
        if (await RefusalAsync(eventId, user) is { } refused)
        {
            return refused;
        }

        var post = await PostAsync(eventId, postId);
        if (post?.TransportId is not { } transportId)
        {
            return Fail(EventResultStatus.NotFound);
        }

        string? passengerUserId;
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            passengerUserId = await db.TransportationPassengers
                .Where(p => p.Id == passengerId && p.TransportationId == transportId)
                .Select(p => p.PassengerId)
                .FirstOrDefaultAsync();
        }

        if (passengerUserId is null)
        {
            return Fail(EventResultStatus.NotFound);
        }

        if (!CanSeat(post.AuthorId, user) && passengerUserId != Me(user))
        {
            return Fail(EventResultStatus.Forbidden);
        }

        await _transport.RemovePassengerAsync(transportId, passengerUserId);
        return Ok(await LoadAsync(eventId, user));
    }

    // ---------- rules ----------

    private static string Me(ClaimsPrincipal user) => EventsAuthorization.UserId(user)!;

    /// <summary>Authors delete their own; the Owner deletes anyone's (the old page's rule).</summary>
    private static bool CanDelete(string authorId, ClaimsPrincipal user) =>
        authorId == EventsAuthorization.UserId(user) || EventsAuthorization.IsOwner(user);

    /// <summary>The driver, or Admin/Owner, seats and unseats passengers.</summary>
    private static bool CanSeat(string driverId, ClaimsPrincipal user) =>
        driverId == EventsAuthorization.UserId(user) || EventsAuthorization.CanManage(user);

    private async Task<EventResult<EventDiscussionDto>?> RefusalAsync(int eventId, ClaimsPrincipal user)
    {
        if (EventsAuthorization.UserId(user) is null)
        {
            return Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Events.AnyAsync(e => e.Id == eventId) ? null : Fail(EventResultStatus.NotFound);
    }

    private sealed record ValidPost(string Title, string Body);

    private static ValidPost? ValidatePost(EventPostInput input)
    {
        var body = input.Body?.Trim() ?? string.Empty;
        var title = TitleFor(input.Title, body);
        return PostErrorsFor(input).Count == 0 ? new ValidPost(title, body) : null;
    }

    /// <summary>A blank title is taken from the start of the text (the table requires 3-120 characters).</summary>
    internal static string TitleFor(string? title, string body)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        var text = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= 60 ? text : text[..59].TrimEnd() + "…";
    }

    private static Dictionary<string, string[]> PostErrorsFor(EventPostInput input)
    {
        var errors = new Dictionary<string, string[]>();
        var body = input.Body?.Trim() ?? string.Empty;
        var title = TitleFor(input.Title, body);
        if (body.Length < BodyMin)
        {
            errors["body"] = new[] { $"Escreve pelo menos {BodyMin} caracteres." };
        }
        else if (body.Length > BodyMax)
        {
            errors["body"] = new[] { $"A mensagem não pode exceder {BodyMax} caracteres." };
        }
        else if (title.Length < TitleMin)
        {
            errors["title"] = new[] { $"O título deve ter pelo menos {TitleMin} caracteres." };
        }

        if (title.Length > TitleMax)
        {
            errors["title"] = new[] { $"O título não pode exceder {TitleMax} caracteres." };
        }

        return errors;
    }

    private static EventResult<EventDiscussionDto> PostErrors(EventPostInput input) =>
        new(EventResultStatus.Invalid, Errors: PostErrorsFor(input));

    private static string? CommentError(string? body)
    {
        var text = body?.Trim() ?? string.Empty;
        return text.Length == 0 ? "Escreve o comentário."
            : text.Length > CommentMax ? $"O comentário não pode exceder {CommentMax} caracteres."
            : null;
    }

    private static Dictionary<string, string[]> TransportErrors(EventTransportInput input, int seated)
    {
        var errors = new Dictionary<string, string[]>();
        var vehicle = input.Vehicle?.Trim() ?? string.Empty;
        if (vehicle.Length == 0)
        {
            errors["vehicle"] = new[] { "A descrição da viatura é obrigatória." };
        }
        else if (vehicle.Length > VehicleMax)
        {
            errors["vehicle"] = new[] { $"A descrição não pode exceder {VehicleMax} caracteres." };
        }

        if (input.Seats is < 2 or > 20)
        {
            errors["seats"] = new[] { "O número de lugares deve estar entre 2 e 20." };
        }
        else if (input.Seats < seated)
        {
            errors["seats"] = new[] { $"Já há {seated} pessoas nesta viatura." };
        }

        if ((input.Notes?.Trim().Length ?? 0) > TransportNotesMax)
        {
            errors["notes"] = new[] { $"As notas não podem exceder {TransportNotesMax} caracteres." };
        }

        return errors;
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

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
        var me = EventsAuthorization.UserId(user);
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

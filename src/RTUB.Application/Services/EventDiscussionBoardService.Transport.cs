// Event discussion board: transport offers and passengers.
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;

namespace RTUB.Application.Services;

public sealed partial class EventDiscussionBoardService
{
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
}

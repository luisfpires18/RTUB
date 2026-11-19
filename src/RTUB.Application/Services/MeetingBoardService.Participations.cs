using System.Security.Claims;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Services;

// Meeting board: replies, participants and candidates.
public sealed partial class MeetingBoardService
{
    // ---------- participations ----------

    public async Task<EventResult<MeetingCardDto>> RespondAsync(int id, MeetingParticipationInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingCardDto>.Fail(refusal);
        }

        var meeting = await VisibleMeetingAsync(access, id);
        if (meeting is null)
        {
            return EventResult<MeetingCardDto>.Fail(EventResultStatus.NotFound);
        }

        // "Vou / Não vou" only before the day of a meeting that is not cancelled.
        if (meeting.IsCancelled || meeting.Date.Date <= Today)
        {
            return EventResult<MeetingCardDto>.Fail(EventResultStatus.Closed);
        }

        var notes = NullIfBlank(input.Notes);
        if (notes?.Length > MaxNotesLength)
        {
            return EventResult<MeetingCardDto>.Invalid("notes", $"A nota não pode exceder {MaxNotesLength} caracteres.");
        }

        var existing = await _participations.GetParticipationByMeetingAndUserAsync(meeting.Id, access.UserId);
        if (existing is null)
        {
            await _participations.CreateParticipationAsync(access.UserId, meeting.Id, notes, input.WillAttend);
        }
        else
        {
            await _participations.UpdateParticipationAsync(existing.Id, input.WillAttend, notes);
        }

        return EventResult<MeetingCardDto>.Ok(await CardAsync(access, meeting));
    }

    public async Task<EventResult<MeetingParticipantsDto>> GetParticipantsAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingParticipantsDto>.Fail(refusal);
        }

        var (meeting, closed) = await ParticipantsMeetingAsync(access, id);
        return meeting is null
            ? EventResult<MeetingParticipantsDto>.Fail(closed)
            : EventResult<MeetingParticipantsDto>.Ok(await ParticipantsAsync(access, meeting));
    }

    public async Task<EventResult<MeetingParticipantsDto>> RemoveParticipationAsync(int id, int participationId, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingParticipantsDto>.Fail(refusal);
        }

        var (meeting, closed) = await ParticipantsMeetingAsync(access, id);
        if (meeting is null)
        {
            return EventResult<MeetingParticipantsDto>.Fail(closed);
        }

        var participation = await _participations.GetParticipationByIdAsync(participationId);
        if (participation is null || participation.MeetingId != meeting.Id)
        {
            return EventResult<MeetingParticipantsDto>.Fail(EventResultStatus.NotFound);
        }

        if (!access.CanRemoveParticipation(participation))
        {
            return EventResult<MeetingParticipantsDto>.Fail(EventResultStatus.Forbidden);
        }

        await _participations.DeleteParticipationAsync(participation.Id);
        return EventResult<MeetingParticipantsDto>.Ok(await ParticipantsAsync(access, meeting));
    }

    public async Task<EventResult<IReadOnlyList<MeetingCandidateDto>>> GetCandidatesAsync(int id, string? search, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<IReadOnlyList<MeetingCandidateDto>>.Fail(refusal);
        }

        var (meeting, closed) = await ParticipantsMeetingAsync(access, id);
        if (meeting is null)
        {
            return EventResult<IReadOnlyList<MeetingCandidateDto>>.Fail(closed);
        }

        if (!access.CanAddParticipants)
        {
            return EventResult<IReadOnlyList<MeetingCandidateDto>>.Fail(EventResultStatus.Forbidden);
        }

        var q = search?.Trim();
        if (string.IsNullOrEmpty(q))
        {
            return EventResult<IReadOnlyList<MeetingCandidateDto>>.Ok(Array.Empty<MeetingCandidateDto>());
        }

        var taken = (await _participations.GetParticipationsByMeetingIdAsync(meeting.Id)).Select(p => p.UserId).ToHashSet();
        var candidates = (await UsersAsync())
            .Where(u => !taken.Contains(u.Id) && MeetingAccess.CanBeAddedToMeeting(u))
            .Where(u => (u.FirstName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (u.LastName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (u.Nickname?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(u => u.FirstName)
            .Take(MaxCandidates)
            .Select(u => new MeetingCandidateDto(u.Id, u.GetDisplayName(), u.ProfilePictureSrc))
            .ToList();
        return EventResult<IReadOnlyList<MeetingCandidateDto>>.Ok(candidates);
    }

    public async Task<EventResult<MeetingParticipantsDto>> AddParticipantAsync(int id, MeetingAddParticipantInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingParticipantsDto>.Fail(refusal);
        }

        var (meeting, closed) = await ParticipantsMeetingAsync(access, id);
        if (meeting is null)
        {
            return EventResult<MeetingParticipantsDto>.Fail(closed);
        }

        if (!access.CanAddParticipants)
        {
            return EventResult<MeetingParticipantsDto>.Fail(EventResultStatus.Forbidden);
        }

        var target = string.IsNullOrWhiteSpace(input.UserId) ? null : (await UsersAsync()).FirstOrDefault(u => u.Id == input.UserId);
        if (target is null || !MeetingAccess.CanBeAddedToMeeting(target))
        {
            return EventResult<MeetingParticipantsDto>.Invalid("userId", "Escolha um membro da lista.");
        }

        // The search never offers someone who had already answered ("Vou" or "Não vou"), so their answer and note
        // are never overwritten from here.
        if (await _participations.GetParticipationByMeetingAndUserAsync(meeting.Id, target.Id) is not null)
        {
            return EventResult<MeetingParticipantsDto>.Invalid("userId", "Este membro já respondeu a esta reunião.");
        }

        await _participations.CreateParticipationAsync(target.Id, meeting.Id, null, true);
        return EventResult<MeetingParticipantsDto>.Ok(await ParticipantsAsync(access, meeting));
    }
}

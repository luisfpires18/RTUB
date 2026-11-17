using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Meeting board: list, read, form, create, update, delete.
public sealed partial class MeetingBoardService
{
    // ---------- meetings ----------

    public async Task<EventResult<MeetingBoardDto>> GetBoardAsync(string? fiscalYear, string? search, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingBoardDto>.Fail(refusal);
        }

        if (!TryFiscalYear(fiscalYear, out var selected, out var range))
        {
            return EventResult<MeetingBoardDto>.Invalid("fy", "Escolha um ano da lista.");
        }

        IEnumerable<Meeting> meetings = await VisibleMeetingsAsync(access);
        if (range is { } r)
        {
            // The old filter, kept as it was: from 1 September to 31 August at midnight.
            meetings = meetings.Where(m => m.Date >= r.Start && m.Date <= r.End);
        }

        // In memory: the old search ran a case-insensitive Contains inside the SQL query.
        var q = search?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            meetings = meetings.Where(m => m.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                                           || (m.Statement ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var today = Today;
        var list = meetings.ToList();
        var upcoming = list.Where(m => m.Date.Date >= today).OrderBy(m => m.Date).ToList();
        var past = list.Where(m => m.Date.Date < today).OrderByDescending(m => m.Date).ToList();
        var cards = await CardsAsync(access, upcoming.Concat(past).ToList());

        var years = (await _fiscalYears.GetAllFiscalYearsAsync())
            .Where(fy => fy.StartYear >= FiscalYearHelper.AppYearCreated)
            .Select(fy => fy.GetFiscalYearString())
            .OrderByDescending(y => y, StringComparer.Ordinal)
            .ToList();

        return EventResult<MeetingBoardDto>.Ok(new MeetingBoardDto(
            cards.Take(upcoming.Count).ToList(),
            cards.Skip(upcoming.Count).ToList(),
            years,
            FiscalYearHelper.GetCurrentFiscalYearString(),
            selected,
            new MeetingBoardAccessDto(
                access.CanCreateMeetings,
                TypeOptions(access.FormTypes),
                access.CanProposeCv,
                access.CanProposeDirecao,
                access.CanProposeAg,
                access.CanSeeRequests,
                access.CanAddParticipants)));
    }

    public async Task<EventResult<MeetingCardDto>> GetMeetingAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingCardDto>.Fail(refusal);
        }

        var meeting = await VisibleMeetingAsync(access, id);
        return meeting is null
            ? EventResult<MeetingCardDto>.Fail(EventResultStatus.NotFound)
            : EventResult<MeetingCardDto>.Ok(await CardAsync(access, meeting));
    }

    public async Task<EventResult<MeetingFormDto>> GetFormAsync(ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingFormDto>.Fail(refusal);
        }

        if (!access.CanCreateMeetings)
        {
            return EventResult<MeetingFormDto>.Fail(EventResultStatus.Forbidden);
        }

        var users = await UsersAsync();
        return EventResult<MeetingFormDto>.Ok(new MeetingFormDto(
            TypeOptions(access.FormTypes),
            TunoRepresentativeCandidates(users).Select(TunoOption).ToList()));
    }

    public async Task<EventResult<MeetingSavedDto>> CreateAsync(MeetingInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingSavedDto>.Fail(refusal);
        }

        if (!access.CanCreateMeetings)
        {
            return EventResult<MeetingSavedDto>.Fail(EventResultStatus.Forbidden);
        }

        var (meeting, errors) = ReadMeeting(input, access, await UsersAsync(), savedRepresentativeId: null);
        if (meeting is null)
        {
            return Invalid<MeetingSavedDto>(errors);
        }

        meeting.OrganizerUserId = access.UserId;

        // The old service push to the meeting type's audience comes with it.
        var created = await _meetings.CreateMeetingAsync(meeting);
        return EventResult<MeetingSavedDto>.Ok(new MeetingSavedDto(created.Id, await CardOrNullAsync(access, created.Id)));
    }

    public async Task<EventResult<MeetingSavedDto>> UpdateAsync(int id, MeetingInput input, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<MeetingSavedDto>.Fail(refusal);
        }

        var existing = await VisibleMeetingAsync(access, id);
        if (existing is null)
        {
            return EventResult<MeetingSavedDto>.Fail(EventResultStatus.NotFound);
        }

        if (!access.CanManage(existing))
        {
            return EventResult<MeetingSavedDto>.Fail(EventResultStatus.Forbidden);
        }

        var (meeting, errors) = ReadMeeting(input, access, await UsersAsync(), existing.TunoRepresentativeUserId);
        if (meeting is null)
        {
            return Invalid<MeetingSavedDto>(errors);
        }

        // The organizer stays; a cancelled meeting stays cancelled (the old edit form silently reactivated it).
        meeting.Id = existing.Id;
        meeting.OrganizerUserId = existing.OrganizerUserId;
        meeting.IsCancelled = existing.IsCancelled;
        meeting.CancellationReason = existing.CancellationReason;
        await _meetings.UpdateMeetingAsync(meeting);

        return EventResult<MeetingSavedDto>.Ok(new MeetingSavedDto(existing.Id, await CardOrNullAsync(access, existing.Id)));
    }

    public async Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user)
    {
        var (access, refusal) = await AccessAsync(user);
        if (access is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        var meeting = await VisibleMeetingAsync(access, id);
        if (meeting is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (!access.CanManage(meeting))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        await _meetings.DeleteMeetingAsync(meeting.Id);
        return EventResult<bool>.Ok(true);
    }
}

using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// MeetingParticipation service implementation
/// Contains business logic for meeting participation operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class MeetingParticipationService : IMeetingParticipationService
{
    private readonly IMeetingParticipationRepository _participationRepository;

    public MeetingParticipationService(IMeetingParticipationRepository participationRepository)
    {
        _participationRepository = participationRepository;
    }

    public async Task<MeetingParticipation?> GetParticipationByIdAsync(int id)
    {
        return await _participationRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<MeetingParticipation>> GetAllParticipationsAsync()
    {
        return await _participationRepository.GetAllAsync();
    }

    public async Task<IEnumerable<MeetingParticipation>> GetParticipationsByMeetingIdAsync(int meetingId)
    {
        return await _participationRepository.GetByMeetingIdAsync(meetingId);
    }

    public async Task<IEnumerable<MeetingParticipation>> GetParticipationsByUserIdAsync(string userId)
    {
        return await _participationRepository.GetByUserIdAsync(userId);
    }

    public async Task<MeetingParticipation?> GetParticipationByMeetingAndUserAsync(int meetingId, string userId)
    {
        return await _participationRepository.GetByMeetingAndUserAsync(meetingId, userId);
    }

    public async Task<MeetingParticipation> CreateParticipationAsync(
        string userId,
        int meetingId,
        string? notes = null,
        bool willAttend = true)
    {
        var participation = MeetingParticipation.Create(meetingId, userId);
        participation.Notes = notes;
        participation.WillAttend = willAttend;

        var createdParticipation = await _participationRepository.AddAsync(participation);

        return createdParticipation;
    }

    public async Task<MeetingParticipation> UpdateParticipationAsync(
        int participationId,
        bool willAttend,
        string? notes = null)
    {
        var participation = await _participationRepository.GetByIdOrThrowAsync(participationId);

        // Update participation fields
        if (participation.WillAttend != willAttend)
        {
            participation.UpdateAttendance(willAttend);
            // Update enlist/participation time only when attendance intent actually changes
            participation.ParticipatedAt = DateTime.UtcNow;
        }
        participation.UpdateNotes(notes);

        await _participationRepository.UpdateAsync(participation);

        return participation;
    }

    public async Task DeleteParticipationAsync(int id)
    {
        var participation = await _participationRepository.GetByIdOrThrowAsync(id);

        await _participationRepository.DeleteAsync(id);
    }

    public async Task<Dictionary<int, int>> GetParticipationCountsByMeetingIdsAsync(IEnumerable<int> meetingIds)
    {
        var meetingIdList = meetingIds.ToList();
        if (!meetingIdList.Any())
            return new Dictionary<int, int>();

        // Use a single query to get all participation counts
        var counts = await _participationRepository.Query()
            .AsNoTracking()
            .Where(p => meetingIdList.Contains(p.MeetingId) && p.WillAttend)
            .GroupBy(p => p.MeetingId)
            .Select(g => new { MeetingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MeetingId, x => x.Count);

        return counts;
    }
}

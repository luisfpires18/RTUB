using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Meeting board: meeting and request cards.
public sealed partial class MeetingBoardService
{
    // ---------- cards ----------

    private async Task<MeetingCardDto> CardAsync(MeetingAccess access, Meeting meeting) =>
        (await CardsAsync(access, new[] { meeting }))[0];

    private async Task<MeetingCardDto?> CardOrNullAsync(MeetingAccess access, int id) =>
        await VisibleMeetingAsync(access, id) is { } meeting ? await CardAsync(access, meeting) : null;

    private async Task<List<MeetingCardDto>> CardsAsync(MeetingAccess access, IReadOnlyList<Meeting> meetings)
    {
        var today = Today;
        var ids = meetings.Select(m => m.Id).ToList();
        var counts = ids.Count == 0 ? new Dictionary<int, int>() : await _participations.GetParticipationCountsByMeetingIdsAsync(ids);
        var mine = (await _participations.GetParticipationsByUserIdAsync(access.UserId))
            .Where(p => p.UserId == access.UserId)
            .GroupBy(p => p.MeetingId)
            .ToDictionary(g => g.Key, g => g.First());

        // The old page looked atas up for its "past" list only (before today).
        var pastIds = meetings.Where(m => m.Date.Date < today).Select(m => m.Id).ToList();
        var atas = pastIds.Count == 0 ? new Dictionary<int, MeetingAta>() : await _atas.GetByMeetingIdsAsync(pastIds);

        return meetings
            .Select(m => Card(access, m, counts.GetValueOrDefault(m.Id), mine.GetValueOrDefault(m.Id), atas.GetValueOrDefault(m.Id), today))
            .ToList();
    }

    private MeetingCardDto Card(MeetingAccess access, Meeting m, int going, MeetingParticipation? mine, MeetingAta? ata, DateTime today)
    {
        var past = m.Date.Date < today;
        var completed = !m.IsCancelled && m.Date.Date <= today;
        var knownAta = past ? ata : null;
        var canWrite = completed && CanWriteAta(access, m, knownAta);
        var published = knownAta?.Status == MeetingAtaStatus.Published;
        var readable = past && !m.IsCancelled && knownAta is not null && access.CanReadPublishedAta(m) && (published || canWrite);
        var manage = access.CanManage(m);

        return new MeetingCardDto(
            m.Id,
            m.Type.ToString(),
            TypeLabel(m.Type),
            m.Title,
            Format(m.Date),
            m.Location,
            m.Statement ?? string.Empty,
            m.Organizer?.Nickname,
            m.Organizer?.Positions is { Count: > 0 } positions ? PositionHelper.GetDisplayName(positions[0]) : null,
            m.Type == MeetingType.ConselhoVeteranos ? m.TunoRepresentative?.Nickname : null,
            manage ? m.TunoRepresentativeUserId : null,
            m.IsCancelled,
            completed,
            past,
            going,
            mine is null ? null : new MeetingMyParticipationDto(mine.Id, mine.WillAttend, mine.Notes),
            readable ? (published ? "published" : "draft") : null,
            new MeetingCardAccessDto(
                Manage: manage,
                Notify: manage && !m.IsCancelled && m.Date.Date >= today,
                Uncancel: manage && m.IsCancelled,
                Respond: !m.IsCancelled && !completed,
                RemoveOwn: completed && mine is not null,
                Participants: !m.IsCancelled,
                WriteAta: canWrite && !published,
                ViewAta: readable));
    }

    private async Task<MeetingParticipantsDto> ParticipantsAsync(MeetingAccess access, Meeting meeting)
    {
        var participations = (await _participations.GetParticipationsByMeetingIdAsync(meeting.Id)).ToList();
        MeetingParticipantDto Participant(MeetingParticipation p) => new(
            p.Id,
            p.User?.Nickname ?? string.Empty,
            $"{p.User?.FirstName} {p.User?.LastName}".Trim(),
            p.User?.ProfilePictureSrc ?? "/images/default-avatar.webp",
            p.Notes,
            Badge(p.User),
            access.CanRemoveParticipation(p));

        return new MeetingParticipantsDto(
            meeting.Id,
            meeting.Title,
            participations.Where(p => p.WillAttend).Select(Participant).ToList(),
            participations.Where(p => !p.WillAttend).Select(Participant).ToList(),
            access.CanAddParticipants);
    }

    private static string? Badge(ApplicationUser? user)
    {
        if (user is null)
        {
            return null;
        }

        if (user.Positions?.Contains(Position.Magister) == true)
        {
            return "Magister";
        }

        return user.Categories?.Contains(MemberCategory.Tuno) == true ? "Tuno"
            : user.Categories?.Contains(MemberCategory.Caloiro) == true ? "Caloiro"
            : null;
    }

    private MeetingRequestDto RequestDto(MeetingAccess access, MeetingRequest r)
    {
        var pending = r.Status == RequestStatus.Pending;
        return new MeetingRequestDto(
            r.Id,
            r.RequestedMeetingType.ToString(),
            TypeLabel(r.RequestedMeetingType),
            r.Title,
            r.Author?.Nickname ?? "Desconhecido",
            Format(r.ProposedDateTime),
            r.Location,
            r.Description,
            r.Status.ToString(),
            access.OwnsRequest(r),
            access.OwnsRequest(r) && pending,
            access.CanDecide(r) && pending);
    }
}

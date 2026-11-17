using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Meeting polls: access check, DTO mapping and parsing helpers.
public sealed partial class MeetingPollBoardService
{
    private async Task<(MeetingAccess? Access, EventResultStatus Refusal)> AccessAsync(ClaimsPrincipal principal)
    {
        var userId = principal.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        if (string.IsNullOrEmpty(userId))
        {
            return (null, EventResultStatus.SignInRequired);
        }

        await using var db = await contexts.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId);
        if (user is null)
        {
            return (null, EventResultStatus.SignInRequired);
        }

        var access = new MeetingAccess(user, principal.IsInRole("Owner"), principal.IsInRole("Admin"));
        return access.IsBlocked ? (null, EventResultStatus.Forbidden) : (access, EventResultStatus.Ok);
    }

    private static MeetingPollDto ToDto(
        MeetingAccess access,
        MeetingPoll poll,
        DateTime now,
        IReadOnlyDictionary<int, int> counts,
        IReadOnlyDictionary<int, int> ownVotes)
    {
        var eligible = access.CanVoteInPoll(poll.Audience);
        var closed = !IsOpen(poll, now);
        int? ownOptionId = eligible && ownVotes.TryGetValue(poll.Id, out var optionId) ? optionId : null;
        var options = poll.Options
            .OrderBy(option => option.Id)
            .Select(option => new MeetingPollOptionDto(option.Id, option.Text, counts.GetValueOrDefault(option.Id)))
            .ToList();
        var createdAt = PortugalTime.FromUtc(DateTime.SpecifyKind(poll.CreatedAt, DateTimeKind.Utc));

        return new MeetingPollDto(
            poll.Id,
            poll.Audience.ToString(),
            AudienceLabel(poll.Audience),
            poll.Question,
            createdAt.ToString(DateFormat, CultureInfo.InvariantCulture),
            poll.ClosesAt?.ToString(DateFormat, CultureInfo.InvariantCulture),
            closed,
            access.CanManagePoll(poll.Audience),
            eligible && !closed,
            access.OwnsPoll(poll),
            ownOptionId,
            options.Sum(option => option.Votes),
            options);
    }

    private static bool IsOpen(MeetingPoll poll, DateTime now) =>
        !poll.IsClosed && (poll.ClosesAt is null || poll.ClosesAt > now);

    private bool TryParseClosingDate(string? value, out DateTime? closesAt)
    {
        closesAt = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || parsed <= PortugalTime.Now(time))
        {
            return false;
        }

        closesAt = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        return true;
    }

    private static bool TryAudience(string? value, out MeetingPollAudience audience) =>
        Enum.TryParse(value, ignoreCase: false, out audience) && Enum.IsDefined(audience);

    private static string AudienceLabel(MeetingPollAudience audience) => audience switch
    {
        MeetingPollAudience.ConselhoVeteranos => "Conselho de Veteranos",
        MeetingPollAudience.AssembleiaGeral => "Assembleia Geral",
        MeetingPollAudience.Direcao => "Direção",
        _ => throw new ArgumentOutOfRangeException(nameof(audience), audience, "Órgão de votação desconhecido."),
    };
}

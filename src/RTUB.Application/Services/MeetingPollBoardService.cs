using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public sealed partial class MeetingPollBoardService(
    IDbContextFactory<ApplicationDbContext> contexts,
    TimeProvider time,
    IPushNotificationService push,
    IPushNotificationFactory pushFactory,
    ILogger<MeetingPollBoardService> logger) : IMeetingPollBoardService
{
    private const int MaxQuestionLength = 300;
    private const int MaxOptionLength = 200;
    private const int MinOptions = 2;
    private const int MaxOptions = 12;
    private const string DateFormat = "yyyy-MM-dd'T'HH:mm";

    private static readonly MeetingPollAudience[] AudienceOrder =
    {
        MeetingPollAudience.ConselhoVeteranos,
        MeetingPollAudience.AssembleiaGeral,
        MeetingPollAudience.Direcao,
    };

    public async Task<EventResult<MeetingPollBoardDto>> GetBoardAsync(ClaimsPrincipal principal)
    {
        var (access, refusal) = await AccessAsync(principal);
        if (access is null)
        {
            return EventResult<MeetingPollBoardDto>.Fail(refusal);
        }

        await using var db = await contexts.CreateDbContextAsync();
        var polls = await db.MeetingPolls
            .AsNoTracking()
            .Include(poll => poll.Options)
            .OrderByDescending(poll => poll.CreatedAt)
            .ToListAsync();

        // Votes are counted in SQL: the board never needs the vote rows themselves.
        var counts = await db.MeetingPollVotes
            .GroupBy(vote => vote.MeetingPollOptionId)
            .Select(group => new { OptionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.OptionId, item => item.Count);
        var ownVotes = await db.MeetingPollVotes
            .Where(vote => vote.UserId == access.UserId)
            .ToDictionaryAsync(vote => vote.MeetingPollId, vote => vote.MeetingPollOptionId);

        var now = PortugalTime.Now(time);
        var visible = polls
            .Where(poll => access.CanVoteInPoll(poll.Audience) || access.CanManagePoll(poll.Audience))
            .Select(poll => ToDto(access, poll, now, counts, ownVotes))
            .ToList();
        var createAudiences = AudienceOrder
            .Where(access.CanCreatePoll)
            .Select(audience => new MeetingPollAudienceOptionDto(audience.ToString(), AudienceLabel(audience)))
            .ToList();

        return EventResult<MeetingPollBoardDto>.Ok(new MeetingPollBoardDto(createAudiences, visible));
    }
}

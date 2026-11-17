using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Meeting polls: vote, close and delete.
public sealed partial class MeetingPollBoardService
{
    public async Task<EventResult<bool>> VoteAsync(int pollId, MeetingPollVoteInput input, ClaimsPrincipal principal)
    {
        var (access, refusal) = await AccessAsync(principal);
        if (access is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        await using var db = await contexts.CreateDbContextAsync();
        var poll = await db.MeetingPolls
            .Include(item => item.Options)
            .FirstOrDefaultAsync(item => item.Id == pollId);
        if (poll is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        var canVote = access.CanVoteInPoll(poll.Audience);
        if (!canVote)
        {
            return EventResult<bool>.Fail(access.CanManagePoll(poll.Audience)
                ? EventResultStatus.Forbidden
                : EventResultStatus.NotFound);
        }

        if (!IsOpen(poll, PortugalTime.Now(time)))
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        if (!poll.Options.Any(option => option.Id == input.OptionId))
        {
            return EventResult<bool>.Invalid(nameof(input.OptionId), "Escolha uma opção desta votação.");
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var vote = await db.MeetingPollVotes
            .FirstOrDefaultAsync(item => item.MeetingPollId == pollId && item.UserId == userId);
        if (vote is null)
        {
            db.MeetingPollVotes.Add(new MeetingPollVote
            {
                MeetingPollId = pollId,
                MeetingPollOptionId = input.OptionId,
                UserId = userId,
                VotedAt = PortugalTime.Now(time),
            });
        }
        else
        {
            vote.MeetingPollOptionId = input.OptionId;
            vote.VotedAt = PortugalTime.Now(time);
        }

        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<bool>> CloseAsync(int pollId, ClaimsPrincipal principal)
    {
        var (access, refusal) = await AccessAsync(principal);
        if (access is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        await using var db = await contexts.CreateDbContextAsync();
        var poll = await db.MeetingPolls.FirstOrDefaultAsync(item => item.Id == pollId);
        if (poll is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (!access.CanManagePoll(poll.Audience))
        {
            return EventResult<bool>.Fail(access.CanVoteInPoll(poll.Audience)
                ? EventResultStatus.Forbidden
                : EventResultStatus.NotFound);
        }

        var now = PortugalTime.Now(time);
        if (!IsOpen(poll, now))
        {
            return EventResult<bool>.Fail(EventResultStatus.Closed);
        }

        poll.IsClosed = true;
        poll.ClosedAt = now;
        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    /// <summary>
    /// Deletes a poll, open or closed, with its options and votes. Only its creator or Owner
    /// (<see cref="MeetingAccess.OwnsPoll"/>); a member who can see the poll but does not own it is refused, anyone else
    /// is told it does not exist.
    /// </summary>
    public async Task<EventResult<bool>> DeleteAsync(int pollId, ClaimsPrincipal principal)
    {
        var (access, refusal) = await AccessAsync(principal);
        if (access is null)
        {
            return EventResult<bool>.Fail(refusal);
        }

        await using var db = await contexts.CreateDbContextAsync();
        // Votes are removed explicitly and first: their link to an option is NoAction, so EF refuses to drop an option
        // that still has a vote. Options then go with the poll (cascade).
        var poll = await db.MeetingPolls
            .Include(item => item.Options)
            .Include(item => item.Votes)
            .FirstOrDefaultAsync(item => item.Id == pollId);
        if (poll is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (!access.OwnsPoll(poll))
        {
            return EventResult<bool>.Fail(access.CanVoteInPoll(poll.Audience) || access.CanManagePoll(poll.Audience)
                ? EventResultStatus.Forbidden
                : EventResultStatus.NotFound);
        }

        db.MeetingPollVotes.RemoveRange(poll.Votes);
        db.MeetingPolls.Remove(poll);
        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }
}

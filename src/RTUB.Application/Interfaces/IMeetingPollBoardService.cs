using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

public interface IMeetingPollBoardService
{
    Task<EventResult<MeetingPollBoardDto>> GetBoardAsync(ClaimsPrincipal user);

    Task<EventResult<int>> CreateAsync(MeetingPollInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> VoteAsync(int pollId, MeetingPollVoteInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> CloseAsync(int pollId, ClaimsPrincipal user);

    /// <summary>Deletes a poll with its options and votes: its creator or Owner only.</summary>
    Task<EventResult<bool>> DeleteAsync(int pollId, ClaimsPrincipal user);
}

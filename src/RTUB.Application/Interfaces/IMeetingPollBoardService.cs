using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

public interface IMeetingPollBoardService
{
    Task<EventResult<MeetingPollBoardDto>> GetBoardAsync(ClaimsPrincipal user);

    Task<EventResult<int>> CreateAsync(MeetingPollInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> VoteAsync(int pollId, MeetingPollVoteInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> CloseAsync(int pollId, ClaimsPrincipal user);
}

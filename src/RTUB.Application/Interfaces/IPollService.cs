using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Poll operations
/// </summary>
public interface IPollService
{
    Task<Poll?> GetPollAsync(int pollId);
    Task<IEnumerable<Poll>> GetAllPollsAsync(PollStatus? filterStatus = null);
    Task<Poll> CreatePollAsync(string title, string? description, string createdByUserId, PollType pollType, 
        bool isAnonymous, int maxVotesPerUser, DateTime? startDate = null, DateTime? endDate = null);
    Task<PollOption> AddOptionAsync(int pollId, string optionText);
    Task UpdatePollAsync(int pollId, string title, string? description, DateTime? startDate, DateTime? endDate, 
        bool isAnonymous, int maxVotesPerUser);
    Task DeletePollAsync(int pollId);
    Task ClosePollAsync(int pollId);
    Task VoteAsync(int pollId, string userId, List<int> optionIds);
    Task<Dictionary<int, int>> GetPollResultsAsync(int pollId);
    Task<List<int>> GetUserVotesForPollAsync(int pollId, string userId);
    Task<bool> HasUserVotedAsync(int pollId, string userId);
    Task RemoveVoteAsync(int pollId, string userId);
}

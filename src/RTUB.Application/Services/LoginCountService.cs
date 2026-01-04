using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing login count operations
/// </summary>
public class LoginCountService : ILoginCountService
{
    private readonly ILoginCountRepository _loginCountRepository;

    public LoginCountService(ILoginCountRepository loginCountRepository)
    {
        _loginCountRepository = loginCountRepository;
    }

    public async Task<int> GetLoginCountForDateAsync(string userId, DateTime date)
    {
        var loginCount = await _loginCountRepository.GetByUserAndDateAsync(userId, date);
        return loginCount?.Count ?? 0;
    }

    public async Task<List<LoginCount>> GetLoginHistoryAsync(string userId)
    {
        return await _loginCountRepository.GetByUserIdAsync(userId);
    }

    public async Task<List<LoginCount>> GetLoginHistoryForDateRangeAsync(string userId, DateTime startDate, DateTime endDate)
    {
        return await _loginCountRepository.GetByUserIdAndDateRangeAsync(userId, startDate, endDate);
    }

    public async Task<int> GetTotalLoginCountAsync(string userId)
    {
        return await _loginCountRepository.GetTotalLoginCountByUserIdAsync(userId);
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing login count records
/// </summary>
public class LoginCountService : ILoginCountService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginCountService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task RecordLoginAsync(string userId, string? ipAddress = null, string? userAgent = null)
    {
        var loginCount = new LoginCount
        {
            UserId = userId,
            LoginDate = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        _context.LoginCounts.Add(loginCount);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<LoginCount>> GetUserLoginsAsync(string userId)
    {
        return await _context.LoginCounts
            .Where(lc => lc.UserId == userId)
            .OrderByDescending(lc => lc.LoginDate)
            .ToListAsync();
    }

    public async Task<int> GetUserLoginCountAsync(string userId)
    {
        return await _context.LoginCounts
            .Where(lc => lc.UserId == userId)
            .CountAsync();
    }

    public async Task<(IEnumerable<LoginCount> logins, int totalCount)> GetPagedLoginsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = _context.LoginCounts
            .Include(lc => lc.User)
            .AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(lc => lc.LoginDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(lc => lc.LoginDate <= endDate.Value);
        }

        var totalCount = await query.CountAsync();

        var logins = await query
            .OrderByDescending(lc => lc.LoginDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (logins, totalCount);
    }

    public async Task<IEnumerable<(ApplicationUser User, int LoginCount)>> GetTopUsersByLoginCountAsync(int top = 10)
    {
        // Use a single query with Include to avoid N+1 query performance issue
        var topUsers = await _context.LoginCounts
            .Include(lc => lc.User)
            .GroupBy(lc => new { lc.UserId, lc.User })
            .Select(g => new 
            { 
                User = g.Key.User,
                Count = g.Count() 
            })
            .OrderByDescending(x => x.Count)
            .Take(top)
            .ToListAsync();

        return topUsers
            .Where(x => x.User != null)
            .Select(x => (x.User!, x.Count));
    }

    public async Task<int> GetConsecutiveLoginDaysAsync(string userId)
    {
        var logins = await _context.LoginCounts
            .Where(lc => lc.UserId == userId)
            .Select(lc => lc.LoginDate.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();

        if (!logins.Any())
        {
            return 0;
        }

        int consecutiveDays = 1;
        DateTime todayUtc = DateTime.UtcNow.Date;

        // Check if user logged in today or yesterday (using UTC for consistency)
        if (logins[0] != todayUtc && logins[0] != todayUtc.AddDays(-1))
        {
            return 0;
        }

        for (int i = 0; i < logins.Count - 1; i++)
        {
            var diff = (logins[i] - logins[i + 1]).Days;
            if (diff == 1)
            {
                consecutiveDays++;
            }
            else
            {
                break;
            }
        }

        return consecutiveDays;
    }

    public async Task<int> GetTotalUniqueUsersAsync()
    {
        return await _context.LoginCounts
            .Select(lc => lc.UserId)
            .Distinct()
            .CountAsync();
    }

    public async Task<LoginStatistics> GetLoginStatisticsAsync()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var stats = new LoginStatistics
        {
            TotalLogins = await _context.LoginCounts.CountAsync(),
            TotalUniqueUsers = await GetTotalUniqueUsersAsync(),
            LoginsToday = await _context.LoginCounts.CountAsync(lc => lc.LoginDate >= today),
            LoginsThisWeek = await _context.LoginCounts.CountAsync(lc => lc.LoginDate >= weekStart),
            LoginsThisMonth = await _context.LoginCounts.CountAsync(lc => lc.LoginDate >= monthStart)
        };

        return stats;
    }
}

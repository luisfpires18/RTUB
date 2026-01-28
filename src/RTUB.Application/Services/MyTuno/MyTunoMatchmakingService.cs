using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;

namespace RTUB.Application.Services.MyTuno;

public class MyTunoMatchmakingService
{
    private static readonly TimeSpan CooldownWindow = TimeSpan.FromMinutes(30);

    private readonly ApplicationDbContext _context;

    public MyTunoMatchmakingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Character?> FindOpponentAsync(Character attacker)
    {
        var recentOpponentIds = await _context.MyTunoBattles
            .Where(b => b.StartedAt >= DateTime.UtcNow.Subtract(CooldownWindow)
                        && (b.AttackerCharacterId == attacker.Id || b.DefenderCharacterId == attacker.Id))
            .Select(b => b.AttackerCharacterId == attacker.Id ? b.DefenderCharacterId : b.AttackerCharacterId)
            .Distinct()
            .ToListAsync();

        var attackerRating = CalculateRating(attacker);

        return await _context.Characters
            .Where(c => c.Id != attacker.Id && c.UserId != attacker.UserId)
            .Where(c => !recentOpponentIds.Contains(c.Id))
            .OrderBy(c => Math.Abs(CalculateRating(c) - attackerRating))
            .FirstOrDefaultAsync();
    }

    public static double CalculateRating(Character character)
    {
        return character.Hp * 1.0 + character.Power * 2.0 + character.Speed * 1.5;
    }
}

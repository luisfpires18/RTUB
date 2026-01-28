using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;

namespace RTUB.Application.Services.MyTuno;

public class MyTunoCharacterService
{
    private readonly ApplicationDbContext _context;

    public MyTunoCharacterService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Character> GetOrCreateAsync(string userId)
    {
        var character = await _context.Characters
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (character != null)
        {
            return character;
        }

        character = new Character
        {
            UserId = userId,
            Level = 1,
            Xp = 0,
            Hp = 10,
            Power = 5,
            Speed = 5
        };

        _context.Characters.Add(character);
        await _context.SaveChangesAsync();

        return character;
    }

    public async Task<Character?> GetByUserIdAsync(string userId)
    {
        return await _context.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }
}

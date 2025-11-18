using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing trophies earned at events
/// </summary>
public class TrophyService : ITrophyService
{
    private readonly ApplicationDbContext _context;

    public TrophyService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Trophy?> GetByIdAsync(int id)
    {
        return await _context.Trophies
            .AsNoTracking()
            .Include(t => t.Event)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Trophy>> GetAllAsync()
    {
        return await _context.Trophies
            .AsNoTracking()
            .Include(t => t.Event)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Trophy>> GetByEventIdAsync(int eventId)
    {
        return await _context.Trophies
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<Trophy> CreateAsync(Trophy trophy)
    {
        _context.Trophies.Add(trophy);
        await _context.SaveChangesAsync();
        return trophy;
    }

    public async Task UpdateAsync(Trophy trophy)
    {
        var existingTrophy = await _context.Trophies.FindAsync(trophy.Id);
        if (existingTrophy == null)
            throw new EntityNotFoundException(nameof(Trophy), trophy.Id);

        existingTrophy.Update(trophy.Name);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var trophy = await _context.Trophies.FindAsync(id);
        if (trophy != null)
        {
            _context.Trophies.Remove(trophy);
            await _context.SaveChangesAsync();
        }
    }
}

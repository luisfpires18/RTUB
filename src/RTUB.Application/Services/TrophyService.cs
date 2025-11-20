using Microsoft.EntityFrameworkCore;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing trophies earned at events using Repository pattern
/// Now depends on ITrophyRepository abstraction instead of concrete DbContext
/// </summary>
public class TrophyService : ITrophyService
{
    private readonly ITrophyRepository _trophyRepository;

    public TrophyService(ITrophyRepository trophyRepository)
    {
        _trophyRepository = trophyRepository;
    }

    public async Task<Trophy?> GetByIdAsync(int id)
    {
        return await _trophyRepository.Query()
            .AsNoTracking()
            .Include(t => t.Event)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Trophy>> GetAllAsync()
    {
        return await _trophyRepository.Query()
            .AsNoTracking()
            .Include(t => t.Event)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Trophy>> GetByEventIdAsync(int eventId)
    {
        return await _trophyRepository.Query()
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<Trophy> CreateAsync(Trophy trophy)
    {
        return await _trophyRepository.AddAsync(trophy);
    }

    public async Task UpdateAsync(Trophy trophy)
    {
        var existingTrophy = await _trophyRepository.GetByIdAsync(trophy.Id);
        if (existingTrophy == null)
            throw new EntityNotFoundException(nameof(Trophy), trophy.Id);

        existingTrophy.Update(trophy.Name);
        await _trophyRepository.UpdateAsync(existingTrophy);
    }

    public async Task DeleteAsync(int id)
    {
        var trophy = await _trophyRepository.GetByIdAsync(id);
        if (trophy != null)
        {
            await _trophyRepository.DeleteAsync(trophy);
        }
    }
}

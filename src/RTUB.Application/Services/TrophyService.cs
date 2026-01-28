using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
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

    /// <summary>
    /// Initializes a new instance of the TrophyService
    /// </summary>
    /// <param name="trophyRepository">Repository for trophy operations</param>
    public TrophyService(ITrophyRepository trophyRepository)
    {
        _trophyRepository = trophyRepository;
    }

    /// <summary>
    /// Gets a trophy by its ID with event information
    /// </summary>
    /// <param name="id">The ID of the trophy to retrieve</param>
    /// <returns>The trophy if found, null otherwise</returns>
    public async Task<Trophy?> GetByIdAsync(int id)
    {
        return await _trophyRepository.Query()
            .AsNoTracking()
            .Include(t => t.Event)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>
    /// Gets all trophies ordered by creation date descending
    /// </summary>
    /// <returns>Collection of all trophies with event information</returns>
    public async Task<IEnumerable<Trophy>> GetAllAsync()
    {
        return await _trophyRepository.Query()
            .AsNoTracking()
            .Include(t => t.Event)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all trophies for a specific event, ordered by name
    /// </summary>
    /// <param name="eventId">The ID of the event</param>
    /// <returns>Collection of trophies for the specified event</returns>
    public async Task<IEnumerable<Trophy>> GetByEventIdAsync(int eventId)
    {
        return await _trophyRepository.Query()
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Creates a new trophy
    /// </summary>
    /// <param name="trophy">The trophy entity to create</param>
    /// <returns>The created trophy</returns>
    public async Task<Trophy> CreateAsync(Trophy trophy)
    {
        return await _trophyRepository.AddAsync(trophy);
    }

    /// <summary>
    /// Updates an existing trophy
    /// </summary>
    /// <param name="trophy">The trophy entity with updated values</param>
    /// <exception cref="EntityNotFoundException">Thrown when the trophy is not found</exception>
    public async Task UpdateAsync(Trophy trophy)
    {
        var existingTrophy = await _trophyRepository.GetByIdOrThrowAsync(trophy.Id);

        existingTrophy.Update(trophy.Name);
        await _trophyRepository.UpdateAsync(existingTrophy);
    }

    /// <summary>
    /// Deletes a trophy if it exists
    /// </summary>
    /// <param name="id">The ID of the trophy to delete</param>
    public async Task DeleteAsync(int id)
    {
        var trophy = await _trophyRepository.GetByIdAsync(id);
        if (trophy != null)
        {
            await _trophyRepository.DeleteAsync(trophy);
        }
    }
}

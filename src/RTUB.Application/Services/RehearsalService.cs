using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;

namespace RTUB.Application.Services;

/// <summary>
/// Rehearsal service implementation
/// Contains business logic for rehearsal operations
/// </summary>
public class RehearsalService : IRehearsalService
{
    private readonly ApplicationDbContext _context;

    public RehearsalService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Rehearsal?> GetRehearsalByIdAsync(int id)
    {
        return await _context.Rehearsals
            .Include(r => r.Attendances)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Rehearsal?> GetRehearsalByDateAsync(DateTime date)
    {
        var normalizedDate = date.Date;
        return await _context.Rehearsals
            .Include(r => r.Attendances)
            .FirstOrDefaultAsync(r => r.Date == normalizedDate);
    }

    public async Task<IEnumerable<Rehearsal>> GetRehearsalsAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Rehearsals
            .Include(r => r.Attendances)
            .Where(r => r.Date >= startDate.Date && r.Date <= endDate.Date)
            .OrderBy(r => r.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Rehearsal>> GetUpcomingRehearsalsAsync(int count = 10)
    {
        var today = DateTime.Today;
        return await _context.Rehearsals
            .Include(r => r.Attendances)
            .Where(r => r.Date >= today && !r.IsCanceled)
            .OrderBy(r => r.Date)
            .Take(count)
            .ToListAsync();
    }

    public async Task<Rehearsal> CreateRehearsalAsync(DateTime date, string location, string? theme = null)
    {
        var rehearsal = Rehearsal.Create(date, location, theme);
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();
        return rehearsal;
    }

    public async Task UpdateRehearsalAsync(int id, string location, string? theme, string? notes)
    {
        var rehearsal = await _context.Rehearsals.FindAsync(id);
        if (rehearsal == null)
            throw new InvalidOperationException($"Rehearsal with ID {id} not found");

        rehearsal.UpdateDetails(location, theme, notes);
        _context.Rehearsals.Update(rehearsal);
        await _context.SaveChangesAsync();
    }

    public async Task CancelRehearsalAsync(int id)
    {
        var rehearsal = await _context.Rehearsals.FindAsync(id);
        if (rehearsal == null)
            throw new InvalidOperationException($"Rehearsal with ID {id} not found");

        rehearsal.Cancel();
        _context.Rehearsals.Update(rehearsal);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteRehearsalAsync(int id)
    {
        var rehearsal = await _context.Rehearsals.FindAsync(id);
        if (rehearsal == null)
            throw new InvalidOperationException($"Rehearsal with ID {id} not found");

        _context.Rehearsals.Remove(rehearsal);
        await _context.SaveChangesAsync();
    }
}

using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing instrument inventory
/// </summary>
public class InstrumentService : IInstrumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageStorageService _imageStorageService;

    public InstrumentService(ApplicationDbContext context, IImageStorageService imageStorageService)
    {
        _context = context;
        _imageStorageService = imageStorageService;
    }

    public async Task<Instrument?> GetByIdAsync(int id)
    {
        return await _context.Instruments
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<IEnumerable<Instrument>> GetAllAsync()
    {
        return await _context.Instruments
            .AsNoTracking()
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByCategoryAsync(string category)
    {
        return await _context.Instruments
            .AsNoTracking()
            .Where(i => i.Category == category)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByConditionAsync(InstrumentCondition condition)
    {
        return await _context.Instruments
            .AsNoTracking()
            .Where(i => i.Condition == condition)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByLocationAsync(string location)
    {
        return await _context.Instruments
            .AsNoTracking()
            .Where(i => i.Location == location)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<Instrument> CreateAsync(Instrument instrument)
    {
        _context.Instruments.Add(instrument);
        await _context.SaveChangesAsync();
        return instrument;
    }

    public async Task UpdateAsync(Instrument instrument)
    {
        var existingInstrument = await _context.Instruments.FindAsync(instrument.Id);
        if (existingInstrument == null)
            throw new EntityNotFoundException(nameof(Instrument), instrument.Id);

        existingInstrument.Update(instrument.Name, instrument.Condition, instrument.SerialNumber, 
                                  instrument.Brand, instrument.Location);
        existingInstrument.UpdateMaintenance(instrument.MaintenanceNotes, instrument.LastMaintenanceDate);
        
        await _context.SaveChangesAsync();
        
        // Invalidate the cached instrument image so the new image is served immediately
    }

    public async Task DeleteAsync(int id)
    {
        var instrument = await _context.Instruments.FindAsync(id);
        if (instrument != null)
        {
            // Delete associated image from R2 storage if it exists
            if (!string.IsNullOrEmpty(instrument.ImageUrl))
            {
                await _imageStorageService.DeleteImageAsync(instrument.ImageUrl);
            }

            _context.Instruments.Remove(instrument);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Dictionary<InstrumentCondition, int>> GetConditionStatsAsync()
    {
        return await _context.Instruments
            .AsNoTracking()
            .GroupBy(i => i.Condition)
            .Select(g => new { Condition = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Condition, x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetCategoryStatsAsync()
    {
        return await _context.Instruments
            .AsNoTracking()
            .GroupBy(i => i.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count);
    }
}

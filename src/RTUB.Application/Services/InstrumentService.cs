using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
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
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IImageStorageService _imageStorageService;

    public InstrumentService(IInstrumentRepository instrumentRepository, IImageStorageService imageStorageService)
    {
        _instrumentRepository = instrumentRepository;
        _imageStorageService = imageStorageService;
    }

    public async Task<Instrument?> GetByIdAsync(int id)
    {
        return await _instrumentRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Instrument>> GetAllAsync()
    {
        return await _instrumentRepository.GetAllOrderedAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByCategoryAsync(string category)
    {
        return await _instrumentRepository.GetByCategoryAsync(category);
    }

    public async Task<IEnumerable<Instrument>> GetByConditionAsync(InstrumentCondition condition)
    {
        return await _instrumentRepository.GetByConditionAsync(condition);
    }

    public async Task<IEnumerable<Instrument>> GetByLocationAsync(string location)
    {
        return await _instrumentRepository.GetByLocationAsync(location);
    }

    public async Task<Instrument> CreateAsync(Instrument instrument)
    {
        return await _instrumentRepository.AddAsync(instrument);
    }

    public async Task UpdateAsync(Instrument instrument)
    {
        var existingInstrument = await _instrumentRepository.GetByIdOrThrowAsync(instrument.Id);

        existingInstrument.Update(instrument.Name, instrument.Condition, instrument.SerialNumber,
                                  instrument.Brand, instrument.Location);
        existingInstrument.UpdateMaintenance(instrument.MaintenanceNotes, instrument.LastMaintenanceDate);

        // Update image URLs if they have changed
        if (existingInstrument.ImageUrl != instrument.ImageUrl)
        {
            existingInstrument.ImageUrl = instrument.ImageUrl;
        }
        if (existingInstrument.ThumbnailUrl != instrument.ThumbnailUrl)
        {
            existingInstrument.ThumbnailUrl = instrument.ThumbnailUrl;
        }

        await _instrumentRepository.UpdateAsync(existingInstrument);
    }

    public async Task DeleteAsync(int id)
    {
        var instrument = await _instrumentRepository.GetByIdAsync(id);
        if (instrument != null)
        {
            // Delete associated image from R2 storage if it exists
            if (!string.IsNullOrEmpty(instrument.ImageUrl))
            {
                await _imageStorageService.DeleteImageAsync(instrument.ImageUrl);
            }

            await _instrumentRepository.DeleteAsync(id);
        }
    }

    public async Task<Dictionary<InstrumentCondition, int>> GetConditionStatsAsync()
    {
        return await _instrumentRepository.QueryAsync(q => q
            .GroupBy(i => i.Condition)
            .Select(g => new { Condition = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Condition, x => x.Count));
    }

    public async Task<Dictionary<string, int>> GetCategoryStatsAsync()
    {
        return await _instrumentRepository.QueryAsync(q => q
            .GroupBy(i => i.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count));
    }
}

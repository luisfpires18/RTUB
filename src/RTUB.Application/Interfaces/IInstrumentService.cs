using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for instrument inventory management
/// </summary>
public interface IInstrumentService
{
    Task<Instrument?> GetByIdAsync(int id);
    Task<IEnumerable<Instrument>> GetAllAsync();
    Task<IEnumerable<Instrument>> GetByCategoryAsync(string category);
    Task<IEnumerable<Instrument>> GetByConditionAsync(InstrumentCondition condition);
    Task<IEnumerable<Instrument>> GetByLocationAsync(string location);
    Task<Instrument> CreateAsync(Instrument instrument);
    Task UpdateAsync(Instrument instrument);
    Task DeleteAsync(int id);
    Task SetInstrumentImageAsync(int id, byte[]? imageData, string? contentType);
    Task<Dictionary<InstrumentCondition, int>> GetConditionStatsAsync();
    Task<Dictionary<string, int>> GetCategoryStatsAsync();
}

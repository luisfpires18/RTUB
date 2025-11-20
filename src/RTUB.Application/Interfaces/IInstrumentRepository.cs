using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Instrument entity
/// </summary>
public interface IInstrumentRepository : IRepository<Instrument>
{
    /// <summary>
    /// Get all instruments ordered by name
    /// </summary>
    Task<IEnumerable<Instrument>> GetAllOrderedAsync();

    /// <summary>
    /// Get instruments by category
    /// </summary>
    Task<IEnumerable<Instrument>> GetByCategoryAsync(string category);

    /// <summary>
    /// Get instruments by condition
    /// </summary>
    Task<IEnumerable<Instrument>> GetByConditionAsync(InstrumentCondition condition);

    /// <summary>
    /// Get instruments by location
    /// </summary>
    Task<IEnumerable<Instrument>> GetByLocationAsync(string location);
}

using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository for NaipeTypeConfig entity operations
/// </summary>
public interface INaipeTypeConfigRepository : IRepository<NaipeTypeConfig>
{
    /// <summary>
    /// Get all configs ordered by SortOrder
    /// </summary>
    Task<List<NaipeTypeConfig>> GetAllOrderedAsync();

    /// <summary>
    /// Get visible configs ordered by SortOrder
    /// </summary>
    Task<List<NaipeTypeConfig>> GetVisibleOrderedAsync();

    /// <summary>
    /// Get config by InstrumentType
    /// </summary>
    Task<NaipeTypeConfig?> GetByInstrumentTypeAsync(InstrumentType type);
}

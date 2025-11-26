using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Label entity
/// Provides data access operations for labels
/// </summary>
public interface ILabelRepository : IRepository<Label>
{
    /// <summary>
    /// Gets label by reference
    /// </summary>
    Task<Label?> GetByReferenceAsync(string reference);

    /// <summary>
    /// Gets all active labels
    /// </summary>
    Task<IEnumerable<Label>> GetActiveLabelsAsync();
}

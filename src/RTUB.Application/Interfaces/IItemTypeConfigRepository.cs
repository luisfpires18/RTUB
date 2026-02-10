using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository for ItemTypeConfig entity operations.
/// </summary>
public interface IItemTypeConfigRepository : IRepository<ItemTypeConfig>
{
    /// <summary>
    /// Get all configs ordered by SortOrder.
    /// </summary>
    Task<List<ItemTypeConfig>> GetAllOrderedAsync();

    /// <summary>
    /// Get configs by category ordered by SortOrder.
    /// </summary>
    Task<List<ItemTypeConfig>> GetByCategoryOrderedAsync(string category);

    /// <summary>
    /// Get a config by its TypeKey.
    /// </summary>
    Task<ItemTypeConfig?> GetByTypeKeyAsync(string typeKey);

    /// <summary>
    /// Check if a config with the given TypeKey exists.
    /// </summary>
    Task<bool> ExistsByTypeKeyAsync(string typeKey);
}

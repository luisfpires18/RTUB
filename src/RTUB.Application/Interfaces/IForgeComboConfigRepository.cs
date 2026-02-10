using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository for ForgeComboConfig entity operations.
/// </summary>
public interface IForgeComboConfigRepository : IRepository<ForgeComboConfig>
{
    /// <summary>
    /// Get all forge combo configs ordered by ComboKey.
    /// </summary>
    Task<List<ForgeComboConfig>> GetAllOrderedAsync();

    /// <summary>
    /// Get a forge combo config by its ComboKey.
    /// </summary>
    Task<ForgeComboConfig?> GetByComboKeyAsync(string comboKey);

    /// <summary>
    /// Get all configs that have a picture URL set.
    /// </summary>
    Task<List<ForgeComboConfig>> GetAllWithPicturesAsync();
}

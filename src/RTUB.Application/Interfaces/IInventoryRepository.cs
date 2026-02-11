using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for InventoryItem entity
/// Provides specialized data access methods for inventory management
/// </summary>
public interface IInventoryRepository : IRepository<InventoryItem>
{
    /// <summary>
    /// Gets an inventory item by user ID and item type
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="type">The inventory item type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The inventory item if found, otherwise null</returns>
    Task<InventoryItem?> GetItemAsync(string userId, InventoryItemType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds quantity to an inventory item, creating it if it doesn't exist
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="type">The inventory item type</param>
    /// <param name="quantity">The quantity to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddItemAsync(string userId, InventoryItemType type, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes quantity from an inventory item
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="type">The inventory item type</param>
    /// <param name="quantity">The quantity to consume</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false if insufficient quantity or item doesn't exist</returns>
    Task<bool> ConsumeItemAsync(string userId, InventoryItemType type, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all inventory items for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of inventory items for the user</returns>
    Task<List<InventoryItem>> GetUserInventoryAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-adds multiple item types to inventory in a single DB round-trip.
    /// Loads all affected items at once, modifies in memory, and saves once.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="items">Dictionary of item type to quantity to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddItemsAsync(string userId, Dictionary<InventoryItemType, int> items, CancellationToken cancellationToken = default);
}

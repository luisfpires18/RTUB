using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for InventoryItem entity
/// Handles inventory data access operations
/// </summary>
public class InventoryRepository : Repository<InventoryItem>, IInventoryRepository
{
    public InventoryRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// Gets an inventory item by user ID and item type
    /// </summary>
    public async Task<InventoryItem?> GetItemAsync(string userId, InventoryItemType type, CancellationToken cancellationToken = default)
    {
        return await _context.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Type == type, cancellationToken);
    }

    /// <summary>
    /// Adds quantity to an inventory item, creating it if it doesn't exist
    /// </summary>
    public async Task AddItemAsync(string userId, InventoryItemType type, int quantity, CancellationToken cancellationToken = default)
    {
        // Find existing item (with tracking for update)
        var existingItem = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Type == type, cancellationToken);

        if (existingItem != null)
        {
            // Ensure we have the latest DB value (Blazor Server DbContext is long-lived)
            await _context.Entry(existingItem).ReloadAsync(cancellationToken);
            // Update existing item
            existingItem.AddQuantity(quantity);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Create new item
            var newItem = InventoryItem.Create(userId, type, quantity);
            await _context.InventoryItems.AddAsync(newItem, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Atomically consumes quantity from an inventory item using a single SQL UPDATE
    /// with a WHERE guard (Quantity >= requested). Prevents TOCTOU race conditions
    /// where two concurrent requests could both see sufficient stock and both consume.
    /// </summary>
    public async Task<bool> ConsumeItemAsync(string userId, InventoryItemType type, int quantity, CancellationToken cancellationToken = default)
    {
        // Atomic: decrement only if sufficient stock exists in a single DB round-trip.
        // The WHERE clause "Quantity >= {quantity}" makes the check-and-update atomic —
        // if two requests race, only one will match and decrement.
        var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE InventoryItems SET Quantity = Quantity - {quantity} WHERE UserId = {userId} AND Type = {(int)type} AND Quantity >= {quantity}",
            cancellationToken);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Gets all inventory items for a user
    /// </summary>
    public async Task<List<InventoryItem>> GetUserInventoryAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.InventoryItems
            .AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderBy(i => i.Type)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Batch-adds multiple item types to inventory in a single DB round-trip.
    /// Loads all affected items at once, modifies in memory, and saves once.
    /// Replaces N sequential AddItemAsync calls (each doing query + reload + save).
    /// </summary>
    public async Task AddItemsAsync(string userId, Dictionary<InventoryItemType, int> items, CancellationToken cancellationToken = default)
    {
        if (items == null || items.Count == 0) return;

        var itemTypes = items.Keys.ToList();

        // Single query: load all relevant items at once (with tracking for update)
        var existingItems = await _context.InventoryItems
            .Where(i => i.UserId == userId && itemTypes.Contains(i.Type))
            .ToListAsync(cancellationToken);

        // Reload all tracked entities to ensure fresh values (Blazor Server long-lived DbContext)
        foreach (var item in existingItems)
        {
            await _context.Entry(item).ReloadAsync(cancellationToken);
        }

        var existingDict = existingItems.ToDictionary(i => i.Type);

        foreach (var (type, quantity) in items)
        {
            if (quantity <= 0) continue;

            if (existingDict.TryGetValue(type, out var existing))
            {
                existing.AddQuantity(quantity);
            }
            else
            {
                var newItem = InventoryItem.Create(userId, type, quantity);
                await _context.InventoryItems.AddAsync(newItem, cancellationToken);
            }
        }

        // Single SaveChangesAsync for all modifications
        await _context.SaveChangesAsync(cancellationToken);
    }
}

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
    /// Consumes quantity from an inventory item
    /// </summary>
    public async Task<bool> ConsumeItemAsync(string userId, InventoryItemType type, int quantity, CancellationToken cancellationToken = default)
    {
        // Find existing item (with tracking for update)
        var existingItem = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Type == type, cancellationToken);

        if (existingItem == null)
            return false;

        // Try to consume quantity
        var success = existingItem.ConsumeQuantity(quantity);
        if (!success)
            return false;

        // Save changes
        await _context.SaveChangesAsync(cancellationToken);
        return true;
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
}

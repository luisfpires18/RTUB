using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an item in a user's inventory
/// Tracks quantities of different item types per user
/// </summary>
public class InventoryItem : BaseEntity
{
    /// <summary>
    /// Gets or sets the user ID who owns this inventory item
    /// </summary>
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of inventory item
    /// </summary>
    [Required]
    public InventoryItemType Type { get; set; }

    /// <summary>
    /// Gets or sets the quantity of this item
    /// </summary>
    [Required]
    public int Quantity { get; set; }

    /// <summary>
    /// Navigation property to the user who owns this item
    /// </summary>
    public virtual ApplicationUser User { get; set; } = null!;

    // Private constructor for EF Core
    private InventoryItem() { }

    /// <summary>
    /// Factory method to create a new inventory item
    /// </summary>
    /// <param name="userId">The user ID who owns the item</param>
    /// <param name="type">The type of inventory item</param>
    /// <param name="quantity">The initial quantity</param>
    /// <returns>A new InventoryItem instance</returns>
    /// <exception cref="ArgumentException">Thrown when userId is null or empty, or quantity is negative</exception>
    public static InventoryItem Create(string userId, InventoryItemType type, int quantity)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        if (quantity < 0)
            throw new ArgumentException("Quantity cannot be negative", nameof(quantity));

        return new InventoryItem
        {
            UserId = userId,
            Type = type,
            Quantity = quantity
        };
    }

    /// <summary>
    /// Adds quantity to the inventory item
    /// </summary>
    /// <param name="amount">The amount to add</param>
    /// <exception cref="ArgumentException">Thrown when amount is negative</exception>
    public void AddQuantity(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        Quantity += amount;
    }

    /// <summary>
    /// Removes quantity from the inventory item
    /// </summary>
    /// <param name="amount">The amount to remove</param>
    /// <returns>True if successful, false if insufficient quantity</returns>
    /// <exception cref="ArgumentException">Thrown when amount is negative</exception>
    public bool ConsumeQuantity(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        if (Quantity < amount)
            return false;

        Quantity -= amount;
        return true;
    }
}

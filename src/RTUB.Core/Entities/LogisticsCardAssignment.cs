using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Junction entity representing the assignment of a user to a logistics card.
/// Allows multiple users to be assigned to a single card.
/// </summary>
public class LogisticsCardAssignment : BaseEntity
{
    [Required]
    public int CardId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    // Navigation properties
    public virtual LogisticsCard Card { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;

    // Private constructor for EF Core
    public LogisticsCardAssignment() { }

    // Factory method
    public static LogisticsCardAssignment Create(int cardId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador não pode estar vazio", nameof(userId));

        return new LogisticsCardAssignment
        {
            CardId = cardId,
            UserId = userId
        };
    }
}

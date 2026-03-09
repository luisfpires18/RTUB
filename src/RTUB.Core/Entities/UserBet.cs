using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a user's bet on a specific option
/// Tracks the wager amount and potential winnings
/// </summary>
public class UserBet : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int BetId { get; set; }

    [Required]
    public int BetOptionId { get; set; }

    [Required(ErrorMessage = "O montante de Fidelis é obrigatório")]
    [Range(0.01, double.MaxValue, ErrorMessage = "O montante de Fidelis deve ser pelo menos 0.01")]
    public decimal FidelisAmount { get; set; }

    // Set after bet resolution - null means bet not yet resolved
    public bool? IsWon { get; set; }

    // Calculated after resolution - amount won (includes original wager + profit)
    public decimal FidelisWinnings { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Bet Bet { get; set; } = null!;
    public virtual BetOption BetOption { get; set; } = null!;

    // Private constructor for EF Core
    public UserBet() { }

    // Factory method - ensures valid entity creation
    public static UserBet Create(string userId, int betId, int betOptionId, decimal fidelisAmount)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador é obrigatório", nameof(userId));

        if (fidelisAmount < 0.01m)
            throw new ArgumentException("O montante de Fidelis deve ser pelo menos 0.01", nameof(fidelisAmount));

        return new UserBet
        {
            UserId = userId,
            BetId = betId,
            BetOptionId = betOptionId,
            FidelisAmount = fidelisAmount,
            FidelisWinnings = 0m
        };
    }

    // Business methods
    public void MarkAsWon(decimal odds)
    {
        if (odds < 1.01m)
            throw new ArgumentException("As odds devem ser pelo menos 1.01", nameof(odds));

        IsWon = true;
        // Calculate winnings: original amount * odds
        FidelisWinnings = FidelisAmount * odds;
    }

    public void MarkAsLost()
    {
        IsWon = false;
        FidelisWinnings = 0m;
    }

    public decimal GetProfit()
    {
        // Profit = winnings - original wager
        return FidelisWinnings - FidelisAmount;
    }

    public bool IsResolved()
    {
        return IsWon.HasValue;
    }
}

namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for displaying user bet information in bet modals
/// Used in Bets page for displaying user bets
/// </summary>
public class UserBetDisplayInfo
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string OptionTitle { get; set; } = string.Empty;
    public decimal FidelisAmount { get; set; }
    public decimal PotentialWinnings { get; set; }
}

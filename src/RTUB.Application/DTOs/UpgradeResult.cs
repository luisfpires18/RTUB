namespace RTUB.Application.DTOs;

/// <summary>
/// Result of an upgrade purchase operation
/// </summary>
public class UpgradeResult
{
    /// <summary>
    /// Whether the upgrade purchase was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the purchase failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// New Fidelis balance after the purchase
    /// </summary>
    public decimal NewFidelisBalance { get; set; }

    /// <summary>
    /// New upgrade count for the purchased stat
    /// </summary>
    public int NewUpgradeCount { get; set; }

    /// <summary>
    /// Creates a successful upgrade result
    /// </summary>
    public static UpgradeResult CreateSuccess(decimal newFidelisBalance, int newUpgradeCount)
    {
        return new UpgradeResult
        {
            Success = true,
            NewFidelisBalance = newFidelisBalance,
            NewUpgradeCount = newUpgradeCount
        };
    }

    /// <summary>
    /// Creates a failed upgrade result
    /// </summary>
    public static UpgradeResult CreateFailure(string errorMessage)
    {
        return new UpgradeResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

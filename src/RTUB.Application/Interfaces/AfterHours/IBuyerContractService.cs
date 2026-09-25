using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Interfaces.AfterHours;

/// <summary>A current buyer contract and whether this player has already delivered it.</summary>
public sealed record BuyerContractView(BuyerContract Contract, bool Completed);

public interface IBuyerContractService
{
    /// <summary>
    /// The active cycle's contracts for the current rotation window, creating that window's
    /// contracts on first access (once, whatever the concurrency). Empty when no cycle is active.
    /// </summary>
    Task<IReadOnlyList<BuyerContractView>> GetCurrentContractsAsync(string userId);
}

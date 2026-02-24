using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for the in-game shop (Forge & Shop section).
/// Handles FITAB-to-Leitão exchange and custom sprite auto-assignment.
/// </summary>
public interface IShopService
{
    /// <summary>
    /// Exchange 25 FITAB for 1 Leitão. Unlimited purchases.
    /// </summary>
    Task<(bool Success, string Message, int NewFitabBalance)> ExchangeFitabForLeitaoAsync(
        string userId, CancellationToken cancellationToken = default);
}

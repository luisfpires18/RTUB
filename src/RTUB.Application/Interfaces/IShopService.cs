using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for the in-game shop (Forge & Shop section).
/// Handles FITAB-to-Leitão exchange and custom skin purchases.
/// </summary>
public interface IShopService
{
    /// <summary>
    /// Exchange 25 FITAB for 1 Leitão. Unlimited purchases.
    /// </summary>
    /// <returns>Updated FITAB balance after the exchange.</returns>
    Task<(bool Success, string Message, int NewFitabBalance)> ExchangeFitabForLeitaoAsync(
        string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purchase a custom skin for 2 000 000 Fidelis.
    /// Searches /sprites/games/my-tuno/enemies/arena/ for boss_{username}.png.
    /// If found, sets Character.CustomSpritePath.
    /// </summary>
    Task<(bool Success, string Message)> PurchaseCustomSkinAsync(
        string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a boss_{username}.png sprite exists for the given user.
    /// </summary>
    Task<bool> HasCustomSpriteAvailableAsync(string userId, CancellationToken cancellationToken = default);
}

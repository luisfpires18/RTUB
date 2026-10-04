using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// The React /shop ("Loja RTUB", React track 021; was the Blazor Shop.razor): products and member reservations.
/// Not the MyTuno in-game shop (<see cref="IShopService"/>).
/// </summary>
public interface IProductShopService
{
    /// <summary><paramref name="fiscalYear"/>: null = the current year, "" = every year, or one of the listed years.</summary>
    Task<EventResult<ShopDto>> GetAsync(string? fiscalYear, string? search, string? type, ClaimsPrincipal user);

    Task<EventResult<ShopProductDetailDto>> GetProductAsync(int id, ClaimsPrincipal user);

    Task<EventResult<ShopCreatedDto>> CreateProductAsync(ShopProductInput input, ClaimsPrincipal user);

    Task<EventResult<ShopProductDetailDto>> UpdateProductAsync(int id, ShopProductInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteProductAsync(int id, ClaimsPrincipal user);

    Task<EventResult<ShopProductDetailDto>> SetImageAsync(int id, ShopImageUpload image, ClaimsPrincipal user);

    Task<EventResult<ShopReservationDto>> ReserveAsync(int productId, ShopReservationInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> CancelReservationAsync(int reservationId, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<ShopReservationDto>>> GetReservationsAsync(int productId, ClaimsPrincipal user);
}

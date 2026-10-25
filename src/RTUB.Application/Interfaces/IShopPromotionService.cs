using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>The "Promoções" bundle offers of the React /shop (docs/react-shop.md).</summary>
public interface IShopPromotionService
{
    Task<ShopPromotionsDto> GetAsync(ClaimsPrincipal user);

    Task<EventResult<ShopPromotionDto>> CreateAsync(ShopPromotionInput input, ClaimsPrincipal user);

    Task<EventResult<ShopPromotionDto>> UpdateAsync(int id, ShopPromotionInput input, ClaimsPrincipal user);

    Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user);
}

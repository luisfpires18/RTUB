using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// A shop bundle offer ("PROMOÇÃO"): some products, each in a quantity, sold together for <see cref="Price"/> instead of
/// the sum of their prices. The normal price is never stored: it is always read from the products' current prices.
/// </summary>
public class ShopPromotion : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public List<ShopPromotionItem> Items { get; set; } = new();
}

/// <summary>One product of a <see cref="ShopPromotion"/> and how many of it the bundle holds.</summary>
public class ShopPromotionItem : BaseEntity
{
    public int PromotionId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; } = 1;

    public ShopPromotion? Promotion { get; set; }

    public Product? Product { get; set; }
}

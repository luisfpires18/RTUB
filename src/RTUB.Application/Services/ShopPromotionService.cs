using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Bundle offers of the shop ("3 CDs por 20 € em vez de 30 €"). Mod, Admin and Owner create, edit and delete
/// (<see cref="ShopAuthorization.CanCreate"/>); visitors see only offers made entirely of public products, members all.
/// A bundle needs 1 to 10 distinct products, each 1 to 99 of them, and a price above 0 and below the sum of the
/// products' current prices (that sum is never stored). No reservation: the offer is information, like the catalogue.
/// </summary>
public sealed class ShopPromotionService : IShopPromotionService
{
    private const int MaxItems = 10;
    private const int MaxQuantity = 99;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;

    public ShopPromotionService(IDbContextFactory<ApplicationDbContext> contexts) => _contexts = contexts;

    public async Task<ShopPromotionsDto> GetAsync(ClaimsPrincipal user)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var all = await db.ShopPromotions.AsNoTracking().Include(p => p.Items).ThenInclude(i => i.Product).OrderBy(p => p.Name).ToListAsync();
        var member = ShopAuthorization.IsMember(user);
        var shown = all
            .Where(p => p.Items.Count > 0 && (member || p.Items.All(i => i.Product is { IsPublic: true })))
            .Select(Map)
            .ToList();
        return new ShopPromotionsDto(shown, ShopAuthorization.CanCreate(user));
    }

    public async Task<EventResult<ShopPromotionDto>> CreateAsync(ShopPromotionInput input, ClaimsPrincipal user)
    {
        if (Refusal<ShopPromotionDto>(user) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var promotion = new ShopPromotion { CreatedBy = user.Identity?.Name };
        if (await ApplyAsync(db, promotion, input) is { } errors)
        {
            return new EventResult<ShopPromotionDto>(EventResultStatus.Invalid, Errors: errors);
        }

        db.ShopPromotions.Add(promotion);
        await db.SaveChangesAsync();
        return EventResult<ShopPromotionDto>.Ok(await ReloadAsync(db, promotion.Id));
    }

    public async Task<EventResult<ShopPromotionDto>> UpdateAsync(int id, ShopPromotionInput input, ClaimsPrincipal user)
    {
        if (Refusal<ShopPromotionDto>(user) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.ShopPromotions.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id) is not { } promotion)
        {
            return EventResult<ShopPromotionDto>.Fail(EventResultStatus.NotFound);
        }

        promotion.UpdatedAt = DateTime.UtcNow;
        promotion.UpdatedBy = user.Identity?.Name;
        if (await ApplyAsync(db, promotion, input) is { } errors)
        {
            return new EventResult<ShopPromotionDto>(EventResultStatus.Invalid, Errors: errors);
        }

        await db.SaveChangesAsync();
        return EventResult<ShopPromotionDto>.Ok(await ReloadAsync(db, promotion.Id));
    }

    public async Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user)
    {
        if (Refusal<bool>(user) is { } refused)
        {
            return refused;
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.ShopPromotions.FirstOrDefaultAsync(p => p.Id == id) is not { } promotion)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        db.ShopPromotions.Remove(promotion);
        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        !ShopAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !ShopAuthorization.CanCreate(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

    /// <summary>Validates the input and copies it onto the promotion (items replaced); the errors, or null when it is valid.</summary>
    private static async Task<Dictionary<string, string[]>?> ApplyAsync(ApplicationDbContext db, ShopPromotion promotion, ShopPromotionInput input)
    {
        var errors = new Dictionary<string, string[]>();
        var name = input.Name?.Trim() ?? "";
        if (name == "" || name.Length > 100)
        {
            errors["name"] = new[] { "O nome é obrigatório (máx. 100 caracteres)." };
        }

        var items = input.Items ?? Array.Empty<ShopPromotionItemInput>();
        if (items.Count is 0 or > MaxItems)
        {
            errors["items"] = new[] { $"Escolha entre 1 e {MaxItems} produtos." };
        }
        else if (items.Select(i => i.ProductId).Distinct().Count() != items.Count)
        {
            errors["items"] = new[] { "Cada produto só pode aparecer uma vez (use a quantidade)." };
        }
        else if (items.Any(i => i.Quantity is < 1 or > MaxQuantity))
        {
            errors["items"] = new[] { $"A quantidade tem de estar entre 1 e {MaxQuantity}." };
        }

        decimal normal = 0;
        if (!errors.ContainsKey("items"))
        {
            var ids = items.Select(i => i.ProductId).ToList();
            var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync();
            if (products.Count != ids.Count)
            {
                errors["items"] = new[] { "Há produtos que já não existem." };
            }
            else
            {
                normal = items.Sum(i => products.First(p => p.Id == i.ProductId).Price * i.Quantity);
            }
        }

        if (input.Price <= 0)
        {
            errors["price"] = new[] { "O preço tem de ser maior que 0." };
        }
        else if (!errors.ContainsKey("items") && input.Price >= normal)
        {
            errors["price"] = new[] { $"O preço da promoção tem de ser inferior ao preço normal ({normal:0.00} €)." };
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        promotion.Name = name;
        promotion.Price = Math.Round(input.Price, 2);
        promotion.Items.Clear();
        foreach (var item in items)
        {
            promotion.Items.Add(new ShopPromotionItem { ProductId = item.ProductId, Quantity = item.Quantity });
        }

        return null;
    }

    private static async Task<ShopPromotionDto> ReloadAsync(ApplicationDbContext db, int id) =>
        Map(await db.ShopPromotions.AsNoTracking().Include(p => p.Items).ThenInclude(i => i.Product).FirstAsync(p => p.Id == id));

    private static ShopPromotionDto Map(ShopPromotion p)
    {
        var items = p.Items
            .Where(i => i.Product is not null)
            .OrderBy(i => i.Product!.Type).ThenBy(i => i.Product!.Name)
            .Select(i => new ShopPromotionItemDto(i.ProductId, i.Product!.Name, i.Product.Type, i.Quantity, i.Product.Price,
                string.IsNullOrEmpty(i.Product.ImageUrl) ? null : i.Product.ImageUrl))
            .ToList();
        return new ShopPromotionDto(p.Id, p.Name, p.Price, items.Sum(i => i.UnitPrice * i.Quantity),
            p.Items.All(i => i.Product is { } product && product.Stock >= i.Quantity), items);
    }
}

// Shop: product list, detail, create, update, delete, image.
using System.Security.Claims;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class ProductShopService
{
    public async Task<EventResult<ShopDto>> GetAsync(string? fiscalYear, string? search, string? type, ClaimsPrincipal user)
    {
        if (ShopAuthorization.UserId(user) is not { } me)
        {
            return EventResult<ShopDto>.Fail(EventResultStatus.SignInRequired);
        }

        var current = FiscalYearHelper.GetCurrentFiscalYearString();
        var years = (await _fiscalYears.GetAllFiscalYearsAsync())
            .Where(fy => fy.StartYear >= FiscalYearHelper.AppYearCreated)
            .Select(fy => fy.GetFiscalYearString())
            .OrderByDescending(y => y)
            .ToList();
        // No parameter: the current year, as the old page opened; "" (Todos os anos): every year.
        var selected = fiscalYear ?? current;
        if (selected != "" && selected != current && !years.Contains(selected))
        {
            return EventResult<ShopDto>.Invalid("fiscalYear", "Ano inválido.");
        }

        var all = (await _products.GetAllAsync()).OrderBy(p => p.Type).ThenBy(p => p.Name).ToList();
        var mine = (await _reservations.GetByUserIdAsync(me)).ToDictionary(r => r.ProductId);
        // Admin / Owner see how many reservations a delete would take with it (one grouped query, managers only).
        var counts = ShopAuthorization.CanManage(user) ? await _reservations.CountByProductAsync() : null;
        var q = search?.Trim() ?? "";
        var shown = all
            .Where(p => InFiscalYear(p, selected))
            .Where(p => q == "" || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Type.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Where(p => string.IsNullOrEmpty(type) || p.Type == type)
            .Select(p =>
            {
                var reservation = mine.GetValueOrDefault(p.Id);
                return new ShopProductDto(p.Id, p.Name, p.Type, p.Price, p.Stock, p.IsPublic, Image(p.ImageUrl),
                    reservation is null ? null : Reservation(reservation), reservation is null && Reservable(p), p.AvailableAllYears,
                    counts is null ? null : counts.GetValueOrDefault(p.Id));
            })
            .ToList();

        return EventResult<ShopDto>.Ok(new ShopDto(
            years.Select(y => new MemberOptionDto(y, y == current ? $"{y} (ATUAL)" : y)).ToList(),
            selected,
            all.Select(p => p.Type).Distinct().OrderBy(t => t).ToList(),
            shown,
            Sizes,
            ShopAuthorization.CanCreate(user),
            ShopAuthorization.CanManage(user),
            ShopAuthorization.CanManageReservations(user)));
    }

    public async Task<EventResult<PublicShopDto>> GetPublicAsync(string? fiscalYear, string? search, string? type)
    {
        var current = FiscalYearHelper.GetCurrentFiscalYearString();
        var years = (await _fiscalYears.GetAllFiscalYearsAsync())
            .Where(fy => fy.StartYear >= FiscalYearHelper.AppYearCreated)
            .Select(fy => fy.GetFiscalYearString())
            .OrderByDescending(y => y)
            .ToList();
        var selected = fiscalYear ?? current;
        if (selected != "" && selected != current && !years.Contains(selected))
        {
            return EventResult<PublicShopDto>.Invalid("fiscalYear", "Ano inválido.");
        }

        // Public users see only products explicitly intended for non-members. Keep sold-out products visible,
        // as the authenticated catalogue does, and apply the same fiscal-year/search/type ordering rules.
        var all = (await _products.GetAllAsync()).Where(p => p.IsPublic).OrderBy(p => p.Type).ThenBy(p => p.Name).ToList();
        var q = search?.Trim() ?? "";
        var shown = all
            .Where(p => InFiscalYear(p, selected))
            .Where(p => q == "" || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Type.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Where(p => string.IsNullOrEmpty(type) || p.Type == type)
            .Select(p => new PublicShopProductDto(p.Id, p.Name, p.Type, p.Price, p.Stock, Image(p.ImageUrl), p.Description, p.AvailableAllYears))
            .ToList();

        return EventResult<PublicShopDto>.Ok(new PublicShopDto(
            years.Select(y => new MemberOptionDto(y, y == current ? $"{y} (ATUAL)" : y)).ToList(),
            selected,
            all.Select(p => p.Type).Distinct().OrderBy(t => t).ToList(),
            shown));
    }

    public async Task<EventResult<ShopProductDetailDto>> GetProductAsync(int id, ClaimsPrincipal user)
    {
        if (!ShopAuthorization.IsMember(user))
        {
            return EventResult<ShopProductDetailDto>.Fail(EventResultStatus.SignInRequired);
        }

        return await _products.GetByIdAsync(id) is { } p
            ? EventResult<ShopProductDetailDto>.Ok(Detail(p))
            : EventResult<ShopProductDetailDto>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<ShopCreatedDto>> CreateProductAsync(ShopProductInput input, ClaimsPrincipal user)
    {
        if (!ShopAuthorization.IsMember(user))
        {
            return EventResult<ShopCreatedDto>.Fail(EventResultStatus.SignInRequired);
        }

        if (!ShopAuthorization.CanCreate(user))
        {
            return EventResult<ShopCreatedDto>.Fail(EventResultStatus.Forbidden);
        }

        if (Validate(input) is { Count: > 0 } errors)
        {
            return new EventResult<ShopCreatedDto>(EventResultStatus.Invalid, Errors: errors);
        }

        var product = Product.CreateEmpty();
        Apply(product, input);
        var created = await _products.CreateAsync(product);
        return EventResult<ShopCreatedDto>.Ok(new ShopCreatedDto(created.Id));
    }

    public async Task<EventResult<ShopProductDetailDto>> UpdateProductAsync(int id, ShopProductInput input, ClaimsPrincipal user)
    {
        if (!ShopAuthorization.IsMember(user))
        {
            return EventResult<ShopProductDetailDto>.Fail(EventResultStatus.SignInRequired);
        }

        if (!ShopAuthorization.CanCreate(user))
        {
            return EventResult<ShopProductDetailDto>.Fail(EventResultStatus.Forbidden);
        }

        if (await _products.GetByIdAsync(id) is not { } product)
        {
            return EventResult<ShopProductDetailDto>.Fail(EventResultStatus.NotFound);
        }

        if (Validate(input) is { Count: > 0 } errors)
        {
            return new EventResult<ShopProductDetailDto>(EventResultStatus.Invalid, Errors: errors);
        }

        Apply(product, input);
        await _products.UpdateAsync(product);
        return EventResult<ShopProductDetailDto>.Ok(Detail((await _products.GetByIdAsync(id))!));
    }

    public async Task<EventResult<bool>> DeleteProductAsync(int id, ClaimsPrincipal user)
    {
        if (Refusal<bool>(user) is { } refused)
        {
            return refused;
        }

        if (await _products.GetByIdAsync(id) is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        await _products.DeleteAsync(id);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<ShopProductDetailDto>> SetImageAsync(int id, ShopImageUpload image, ClaimsPrincipal user)
    {
        if (!ShopAuthorization.IsMember(user))
        {
            return EventResult<ShopProductDetailDto>.Fail(EventResultStatus.SignInRequired);
        }

        if (await _products.GetByIdAsync(id) is not { } product)
        {
            return EventResult<ShopProductDetailDto>.Fail(EventResultStatus.NotFound);
        }

        // Mod and above change any image (059; a Mod used to give only a new product its first one).
        if (!ShopAuthorization.CanCreate(user))
        {
            return EventResult<ShopProductDetailDto>.Fail(EventResultStatus.Forbidden);
        }

        if (image.Length <= 0 || !ImageTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase) || !LooksLikeImage(image.Content))
        {
            return EventResult<ShopProductDetailDto>.Invalid("image", "A imagem tem de ser WebP, JPEG ou PNG.");
        }

        if (image.Length > MaxImageBytes)
        {
            return EventResult<ShopProductDetailDto>.Invalid("image", "A imagem não pode exceder 5 MB.");
        }

        if (!string.IsNullOrEmpty(product.ImageUrl))
        {
            await _storage.DeleteImageAsync(product.ImageUrl);
        }

        product.ImageUrl = await _storage.UploadImageAsync(image.Content, "product-image.webp", image.ContentType, "products",
            S3KeyNormalizer.NormalizeForS3Key(product.Name));
        await _products.UpdateAsync(product);
        return EventResult<ShopProductDetailDto>.Ok(Detail((await _products.GetByIdAsync(id))!));
    }
}

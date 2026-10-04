using System.Security.Claims;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /shop ("Loja RTUB") behind the React /shop (React track 021, docs/react-shop.md). Same data and rules,
/// through <see cref="IProductService"/> and <see cref="IProductReservationService"/>, now enforced server-side
/// (<see cref="ShopAuthorization"/>; the old page only hid buttons):
/// - products: every product for any signed-in member, by type then name; the fiscal year of the product's creation
///   (the current one by default, or every year), search over name and type, type filter;
/// - create (Mod, Admin, Owner) and edit (Admin, Owner): name (≤ 200) and type (≤ 50) required, price above 0, stock 0
///   or more, description ≤ 1000, public flag; availability follows the stock; one cropped image (…/products), a new
///   one deleting the previous;
/// - delete (Admin, Owner): hard delete, image and reservations included (cascade), as before;
/// - reservations: members-only products in stock, one per member and product; the member says whether the product
///   has sizes (then a size from the old list is required) and an optional display name; reserving never changes the
///   stock; a member cancels their own, Admin and Owner see and delete every reservation of a product.
/// No notifications, no schema change.
/// </summary>
public sealed class ProductShopService : IProductShopService
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public static readonly IReadOnlyList<string> Sizes = new[] { "XS", "S", "M", "L", "XL", "XXL", "XXXL" };
    private static readonly string[] ImageTypes = { "image/webp", "image/jpeg", "image/png" };

    private readonly IProductService _products;
    private readonly IProductReservationService _reservations;
    private readonly IFiscalYearService _fiscalYears;
    private readonly IImageStorageService _storage;

    public ProductShopService(IProductService products, IProductReservationService reservations, IFiscalYearService fiscalYears, IImageStorageService storage)
    {
        _products = products;
        _reservations = reservations;
        _fiscalYears = fiscalYears;
        _storage = storage;
    }

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
        var q = search?.Trim() ?? "";
        var shown = all
            .Where(p => InFiscalYear(p, selected))
            .Where(p => q == "" || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Type.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Where(p => string.IsNullOrEmpty(type) || p.Type == type)
            .Select(p =>
            {
                var reservation = mine.GetValueOrDefault(p.Id);
                return new ShopProductDto(p.Id, p.Name, p.Type, p.Price, p.Stock, p.IsPublic, Image(p.ImageUrl),
                    reservation is null ? null : Reservation(reservation), reservation is null && Reservable(p));
            })
            .ToList();

        return EventResult<ShopDto>.Ok(new ShopDto(
            years.Select(y => new MemberOptionDto(y, y == current ? $"{y} (ATUAL)" : y)).ToList(),
            selected,
            all.Select(p => p.Type).Distinct().OrderBy(t => t).ToList(),
            shown,
            Sizes,
            ShopAuthorization.CanCreate(user),
            ShopAuthorization.CanManage(user)));
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
        if (Refusal<ShopProductDetailDto>(user) is { } refused)
        {
            return refused;
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

        // Admin and Owner change any image; a Mod only gives the product they are adding its first image (the old
        // create form had the image; editing was Admin only).
        if (!ShopAuthorization.CanManage(user) && !(ShopAuthorization.CanCreate(user) && string.IsNullOrEmpty(product.ImageUrl)))
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

    public async Task<EventResult<ShopReservationDto>> ReserveAsync(int productId, ShopReservationInput input, ClaimsPrincipal user)
    {
        if (ShopAuthorization.UserId(user) is not { } me)
        {
            return EventResult<ShopReservationDto>.Fail(EventResultStatus.SignInRequired);
        }

        if (await _products.GetByIdAsync(productId) is not { } product)
        {
            return EventResult<ShopReservationDto>.Fail(EventResultStatus.NotFound);
        }

        // The old page offered "Reservar" only on members-only products in stock.
        if (!Reservable(product))
        {
            return EventResult<ShopReservationDto>.Invalid("product",
                product.IsPublic ? "Este produto não se reserva aqui." : "Este produto está esgotado.");
        }

        var size = string.IsNullOrWhiteSpace(input.Size) ? null : input.Size.Trim();
        if (input.HasSizes && size is null)
        {
            return EventResult<ShopReservationDto>.Invalid("size", "O tamanho é obrigatório quando o produto tem tamanhos");
        }

        if (input.HasSizes && !Sizes.Contains(size))
        {
            return EventResult<ShopReservationDto>.Invalid("size", "Escolha um tamanho da lista.");
        }

        var displayName = string.IsNullOrWhiteSpace(input.DisplayName) ? null : input.DisplayName.Trim();
        if (displayName is { Length: > 200 })
        {
            return EventResult<ShopReservationDto>.Invalid("displayName", "O nome não pode exceder 200 caracteres.");
        }

        if (await _reservations.HasReservationAsync(productId, me))
        {
            return EventResult<ShopReservationDto>.Invalid("product", "Já existe uma reserva para este produto");
        }

        try
        {
            var created = await _reservations.CreateAsync(ProductReservation.Create(productId, me, user.Identity!.Name ?? "Unknown", input.HasSizes,
                input.HasSizes ? size : null, displayName));
            return EventResult<ShopReservationDto>.Ok(Reservation(created));
        }
        catch (InvalidOperationException ex)
        {
            return EventResult<ShopReservationDto>.Invalid("product", ex.Message);
        }
    }

    public async Task<EventResult<bool>> CancelReservationAsync(int reservationId, ClaimsPrincipal user)
    {
        if (ShopAuthorization.UserId(user) is not { } me)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        if (await _reservations.GetByIdAsync(reservationId) is not { } reservation)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (reservation.UserId != me && !ShopAuthorization.CanManage(user))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        await _reservations.DeleteAsync(reservationId);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<IReadOnlyList<ShopReservationDto>>> GetReservationsAsync(int productId, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<ShopReservationDto>>(user) is { } refused)
        {
            return refused;
        }

        if (await _products.GetByIdAsync(productId) is null)
        {
            return EventResult<IReadOnlyList<ShopReservationDto>>.Fail(EventResultStatus.NotFound);
        }

        return EventResult<IReadOnlyList<ShopReservationDto>>.Ok(
            (await _reservations.GetByProductIdAsync(productId)).Select(Reservation).ToList());
    }

    // ---------- helpers ----------

    private static EventResult<T>? Refusal<T>(ClaimsPrincipal user) =>
        !ShopAuthorization.IsMember(user) ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !ShopAuthorization.CanManage(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

    private static bool Reservable(Product p) => !p.IsPublic && p.Stock > 0;

    private static bool InFiscalYear(Product p, string fiscalYear) =>
        fiscalYear == "" || fiscalYear.Split('-') is not [var start, _] || !int.TryParse(start, out var year)
        || (p.CreatedAt >= new DateTime(year, 9, 1) && p.CreatedAt <= new DateTime(year + 1, 8, 31, 23, 59, 59));

    private static Dictionary<string, string[]> Validate(ShopProductInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            errors["name"] = new[] { "O nome é obrigatório" };
        }
        else if (input.Name.Length > 200)
        {
            errors["name"] = new[] { "O nome não pode exceder 200 caracteres" };
        }

        if (string.IsNullOrWhiteSpace(input.Type))
        {
            errors["type"] = new[] { "O tipo é obrigatório" };
        }
        else if (input.Type.Length > 50)
        {
            errors["type"] = new[] { "O tipo não pode exceder 50 caracteres" };
        }

        if (input.Price <= 0)
        {
            errors["price"] = new[] { "O preço deve ser maior que 0" };
        }

        if (input.Stock < 0)
        {
            errors["stock"] = new[] { "O stock não pode ser negativo" };
        }

        if (input.Description is { Length: > 1000 })
        {
            errors["description"] = new[] { "A descrição não pode exceder 1000 caracteres" };
        }

        return errors;
    }

    /// <summary>As the old save: availability follows the stock.</summary>
    private static void Apply(Product product, ShopProductInput input)
    {
        product.Name = input.Name!;
        product.Type = input.Type!;
        product.Price = input.Price;
        product.Stock = input.Stock;
        product.IsPublic = input.IsPublic;
        product.Description = string.IsNullOrEmpty(input.Description) ? null : input.Description;
        product.SetAvailability(input.Stock > 0);
    }

    private static string? Image(string? url) => string.IsNullOrEmpty(url) ? null : url;

    private static ShopProductDetailDto Detail(Product p) =>
        new(p.Id, p.Name, p.Type, p.Price, p.Stock, p.IsAvailable, p.IsPublic, p.Description, Image(p.ImageUrl));

    private static ShopReservationDto Reservation(ProductReservation r) => new(r.Id, r.UserNickname, r.DisplayName, r.Size, r.CreatedAt);

    /// <summary>The declared type is the browser's word; the first bytes must agree (WebP, JPEG or PNG).</summary>
    private static bool LooksLikeImage(Stream content)
    {
        if (!content.CanSeek)
        {
            return false;
        }

        var head = new byte[12];
        var read = content.Read(head, 0, head.Length);
        content.Position = 0;
        return read == 12 && (
            (head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P')
            || (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            || (head[0] == 0x89 && head[1] == 'P' && head[2] == 'N' && head[3] == 'G'));
    }
}

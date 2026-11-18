// Shop: reservations and the shared shop helpers.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class ProductShopService
{
    public async Task<EventResult<ShopReservationDto>> ReserveAsync(int productId, ShopReservationInput input, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return EventResult<ShopReservationDto>.Fail(EventResultStatus.SignInRequired);
        }

        if (await _products.GetByIdAsync(productId) is not { } product)
        {
            return EventResult<ShopReservationDto>.Fail(EventResultStatus.NotFound);
        }

        // "Reservar" only on members-only products in stock.
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
            return EventResult<ShopReservationDto>.Invalid("product", AlreadyReserved);
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
        catch (DbUpdateException)
        {
            // The member's other request saved first (one reservation per product and member in the database): this
            // one gets the answer of a second click. Any other write failure is still an error.
            if (!await _reservations.HasReservationAsync(productId, me))
            {
                throw;
            }

            return EventResult<ShopReservationDto>.Invalid("product", AlreadyReserved);
        }
    }

    private const string AlreadyReserved = "Já existe uma reserva para este produto";

    public async Task<EventResult<bool>> CancelReservationAsync(int reservationId, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        if (await _reservations.GetByIdAsync(reservationId) is not { } reservation)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (reservation.UserId != me && !ShopAuthorization.CanManageReservations(user))
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        await _reservations.DeleteAsync(reservationId);
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<IReadOnlyList<ShopReservationDto>>> GetReservationsAsync(int productId, ClaimsPrincipal user)
    {
        if (!user.IsMember())
        {
            return EventResult<IReadOnlyList<ShopReservationDto>>.Fail(EventResultStatus.SignInRequired);
        }

        if (!ShopAuthorization.CanManageReservations(user))
        {
            return EventResult<IReadOnlyList<ShopReservationDto>>.Fail(EventResultStatus.Forbidden);
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
        !user.IsMember() ? EventResult<T>.Fail(EventResultStatus.SignInRequired)
        : !ShopAuthorization.CanManage(user) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
        : null;

    private static bool Reservable(Product p) => !p.IsPublic && p.Stock > 0;

    /// <summary>
    /// The one fiscal-year rule of the shop: "" (Todos os anos) shows every product; a year shows the products
    /// available in all years and those created in it (on Portugal's calendar). Stock, availability and visibility
    /// rules are separate.
    /// </summary>
    private static bool InFiscalYear(Product p, string fiscalYear) =>
        fiscalYear == "" || p.AvailableAllYears || fiscalYear.Split('-') is not [var start, _] || !int.TryParse(start, out var year)
        || FiscalYearHelper.GetFiscalYearStartYearOfUtc(p.CreatedAt) == year;

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

    /// <summary>Availability follows the stock.</summary>
    private static void Apply(Product product, ShopProductInput input)
    {
        product.Name = input.Name!;
        product.Type = input.Type!;
        product.Price = input.Price;
        product.Stock = input.Stock;
        product.IsPublic = input.IsPublic;
        product.Description = string.IsNullOrEmpty(input.Description) ? null : input.Description;
        // Absent keeps the current value (false on a new product: year-specific).
        product.AvailableAllYears = input.AvailableAllYears ?? product.AvailableAllYears;
        product.SetAvailability(input.Stock > 0);
    }

    private static string? Image(string? url) => string.IsNullOrEmpty(url) ? null : url;

    private static ShopProductDetailDto Detail(Product p)
    {
        var year = FiscalYearHelper.GetFiscalYearStartYearOfUtc(p.CreatedAt);
        return new(p.Id, p.Name, p.Type, p.Price, p.Stock, p.IsAvailable, p.IsPublic, p.Description, Image(p.ImageUrl),
            p.AvailableAllYears, $"{year}-{year + 1}");
    }

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

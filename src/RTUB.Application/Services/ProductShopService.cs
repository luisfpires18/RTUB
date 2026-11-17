using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// The React /shop (React track 021, docs/react-shop.md). Public reads use a
/// restricted catalogue DTO; signed-in data and actions retain the same rules through <see cref="IProductService"/> and
/// <see cref="IProductReservationService"/>, enforced server-side
/// (<see cref="ShopAuthorization"/>; the old page only hid buttons):
/// - products: every product for any signed-in member, by type then name; the fiscal year of the product's creation
///   (the current one by default, or every year) - or every year for a product available in all years (#221) -, search
///   over name and type, type filter;
/// - create (Mod, Admin, Owner) and edit (Admin, Owner): name (≤ 200) and type (≤ 50) required, price above 0, stock 0
///   or more, description ≤ 1000, public flag; availability follows the stock; one cropped image (…/products), a new
///   one deleting the previous;
/// - delete (Admin, Owner): hard delete, image and reservations included (cascade), as before;
/// - reservations: members-only products in stock, one per member and product; the member says whether the product
///   has sizes (then a size from the old list is required) and an optional display name; reserving never changes the
///   stock; a member cancels their own, Admin and Owner see and delete every reservation of a product.
/// No notifications. #221 added Products.AvailableAllYears (docs/react-shop.md).
/// </summary>
public sealed partial class ProductShopService : IProductShopService
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
}

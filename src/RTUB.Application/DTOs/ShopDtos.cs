namespace RTUB.Application.DTOs;

// Contracts of the React /shop ("Loja RTUB", React track 021; was the Blazor Shop.razor). Signed-in members only, as
// before. A reservation's member id never leaves the server; Admin/Owner see the username the old page showed.

/// <summary>
/// <c>FiscalYear</c>: the year shown ("" = every year; the current one by default, as the old page), by the product's
/// creation date. <c>CanCreate</c>: Mod, Admin, Owner; <c>CanManage</c>: Admin, Owner (edit, delete, reservations).
/// </summary>
public sealed record ShopDto(
    IReadOnlyList<MemberOptionDto> FiscalYears,
    string FiscalYear,
    IReadOnlyList<string> Types,
    IReadOnlyList<ShopProductDto> Products,
    IReadOnlyList<string> Sizes,
    bool CanCreate,
    bool CanManage);

/// <summary>A product card. <c>CanReserve</c>: members-only, in stock and not yet reserved by the caller.</summary>
public sealed record ShopProductDto(
    int Id,
    string Name,
    string Type,
    decimal Price,
    int Stock,
    bool IsPublic,
    string? ImageUrl,
    ShopReservationDto? MyReservation,
    bool CanReserve);

/// <summary>"Detalhes do Produto".</summary>
public sealed record ShopProductDetailDto(
    int Id,
    string Name,
    string Type,
    decimal Price,
    int Stock,
    bool IsAvailable,
    bool IsPublic,
    string? Description,
    string? ImageUrl);

/// <summary><c>Username</c> is the reserving member's username, as stored with the reservation.</summary>
public sealed record ShopReservationDto(int Id, string Username, string? DisplayName, string? Size, DateTime CreatedAt);

public sealed record ShopProductInput(string? Name, string? Type, decimal Price, int Stock, bool IsPublic, string? Description);

public sealed record ShopReservationInput(bool HasSizes, string? Size, string? DisplayName);

public sealed record ShopCreatedDto(int Id);

/// <summary>The cropped product image as the browser sends it.</summary>
public sealed record ShopImageUpload(Stream Content, string FileName, string ContentType, long Length);

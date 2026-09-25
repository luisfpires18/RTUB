using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Helpers.AfterHours;

/// <summary>Cargo names and base fence prices (Game Manual v2). Server-owned; clients never send a price.</summary>
public static class CargoCatalogue
{
    public static long FencePrice(CargoType cargo) => cargo switch
    {
        CargoType.Phone => 15,
        CargoType.Electronics => 30,
        CargoType.TicketBundle => 20,
        CargoType.Spirits => 25,
        CargoType.ArtPiece => 80,
        _ => throw new ArgumentOutOfRangeException(nameof(cargo), cargo, "Unknown cargo")
    };

    public static string Name(CargoType cargo, int quantity = 1) => (cargo, quantity == 1) switch
    {
        (CargoType.Phone, true) => "phone",
        (CargoType.Phone, false) => "phones",
        (CargoType.Electronics, _) => "electronics",
        (CargoType.TicketBundle, true) => "ticket bundle",
        (CargoType.TicketBundle, false) => "ticket bundles",
        (CargoType.Spirits, _) => "spirits",
        (CargoType.ArtPiece, true) => "art piece",
        (CargoType.ArtPiece, false) => "art pieces",
        _ => cargo.ToString()
    };

    public static IReadOnlyList<CargoType> All { get; } = Enum.GetValues<CargoType>();
}

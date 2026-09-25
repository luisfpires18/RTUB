using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Helpers.AfterHours;

/// <summary>A buyer contract template. Cash always beats selling the same cargo at the base fence.</summary>
public sealed record BuyerContractTemplate(string Key, string BuyerName, CargoType Cargo, int Quantity, long Cash, long Xp);

/// <summary>
/// Buyer contract rotation. <b>AH-004 implementation defaults, not Game Manual v2</b> (the manual
/// only says contracts rotate): 12-hour UTC windows aligned to midnight and noon UTC, 3 contracts
/// per window, each expiring at the end of its window. Templates and rewards are AH-004 choices too,
/// except The Midnight Collector, which is the manual's own example.
/// </summary>
public static class BuyerContractRules
{
    public static readonly TimeSpan RotationInterval = TimeSpan.FromHours(12);
    public const int ContractsPerRotation = 3;

    public static readonly IReadOnlyList<BuyerContractTemplate> Templates =
    [
        new("T01", "The Midnight Collector", CargoType.ArtPiece, 4, 460, 80), // manual example; fence 320
        new("T02", "Pawnshop Pedro", CargoType.Phone, 3, 60, 15),               // fence 45
        new("T03", "The Repair Stall", CargoType.Phone, 5, 100, 25),            // fence 75
        new("T04", "Night Market Vendor", CargoType.Electronics, 4, 155, 30),   // fence 120
        new("T05", "The Crypto Kid", CargoType.Electronics, 8, 310, 55),        // fence 240
        new("T06", "The Ticket Tout", CargoType.TicketBundle, 3, 80, 20),       // fence 60
        new("T07", "Festival Promoter", CargoType.TicketBundle, 6, 155, 35),    // fence 120
        new("T08", "The Bar Owner", CargoType.Spirits, 4, 130, 25),             // fence 100
        new("T09", "Wedding Caterer", CargoType.Spirits, 8, 255, 45),           // fence 200
        new("T10", "The Gallery Fixer", CargoType.ArtPiece, 2, 210, 40),        // fence 160
    ];

    /// <summary>Start of the window containing <paramref name="utcNow"/>: a multiple of 12 h since the Unix epoch.</summary>
    public static DateTime WindowStart(DateTime utcNow)
    {
        var ticks = utcNow.Ticks - DateTime.UnixEpoch.Ticks;
        return new DateTime(DateTime.UnixEpoch.Ticks + ticks - ticks % RotationInterval.Ticks, DateTimeKind.Utc);
    }

    /// <summary>
    /// The template for a slot of a window: three consecutive templates, moving on by three each
    /// window, so all ten come round in turn. Deterministic; no randomness.
    /// </summary>
    public static BuyerContractTemplate TemplateFor(DateTime windowStartUtc, int slot)
    {
        var window = (windowStartUtc - DateTime.UnixEpoch).Ticks / RotationInterval.Ticks;
        return Templates[(int)((window * ContractsPerRotation + slot) % Templates.Count)];
    }

    public static BuyerContract Create(int gameCycleId, DateTime windowStartUtc, int slot)
    {
        var template = TemplateFor(windowStartUtc, slot);
        return new BuyerContract
        {
            GameCycleId = gameCycleId,
            RotationStartUtc = windowStartUtc,
            Slot = slot,
            TemplateKey = template.Key,
            BuyerName = template.BuyerName,
            CargoType = template.Cargo,
            Quantity = template.Quantity,
            CashReward = template.Cash,
            XpReward = template.Xp,
            AvailableFromUtc = windowStartUtc,
            ExpiresAtUtc = windowStartUtc + RotationInterval
        };
    }
}

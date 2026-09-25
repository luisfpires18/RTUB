using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Helpers.AfterHours;

/// <summary>One server-owned crime. Clients send only <see cref="Id"/> and an approach.</summary>
public sealed record CrimeDefinition(
    string Id,
    string Name,
    int RequiredLevel,
    int EnergyCost,
    int BaseChance,
    long Cash,
    long Xp,
    int Heat,
    PlayerSkill Skill,
    string SuccessText,
    CargoType? Cargo = null,
    int CargoQuantity = 0);

/// <summary>
/// The year-one crime catalogue (Game Manual v2), including the cargo each success pays.
/// </summary>
public static class CrimeCatalogue
{
    public static readonly IReadOnlyList<CrimeDefinition> All =
    [
        new("C01", "Lift a phone outside the bar", 1, 10, 82, 35, 20, 3, PlayerSkill.Stealth,
            "You slipped the phone before the owner noticed.", CargoType.Phone, 1),
        new("C02", "Collect a debt after rehearsal", 2, 12, 78, 50, 26, 4, PlayerSkill.Toughness,
            "The debt got paid, with interest."),
        new("C03", "Fake a venue invoice", 3, 14, 76, 60, 34, 4, PlayerSkill.Smarts,
            "The venue paid the invoice without a second look.", CargoType.Electronics, 1),
        new("C04", "Sell counterfeit event tickets", 4, 14, 74, 70, 36, 4, PlayerSkill.Charisma,
            "Every fake ticket was gone before the doors opened.", CargoType.TicketBundle, 1),
        new("C05", "Crack a storage locker", 5, 16, 72, 90, 44, 5, PlayerSkill.Smarts,
            "The locker gave up its contents."),
        new("C06", "Move bottles through the back door", 6, 18, 70, 95, 52, 5, PlayerSkill.Smarts,
            "The crates went out the back unseen.", CargoType.Spirits, 2),
        new("C07", "Strip parts from a parked van", 7, 18, 68, 110, 56, 6, PlayerSkill.Stealth,
            "You stripped the van and walked away clean.", CargoType.Electronics, 2),
        new("C08", "Run a phishing racket", 9, 20, 66, 140, 66, 6, PlayerSkill.Smarts,
            "Enough marks clicked the link.", CargoType.Electronics, 3),
        new("C09", "Shake down a rival crew", 11, 20, 64, 175, 74, 7, PlayerSkill.Toughness,
            "The rival crew paid up.", CargoType.Spirits, 3),
        new("C10", "Rig a private table", 13, 22, 62, 200, 86, 7, PlayerSkill.Charisma,
            "The table paid out exactly as planned.", CargoType.TicketBundle, 3),
        new("C11", "Hijack a delivery van", 15, 24, 60, 240, 100, 8, PlayerSkill.Toughness,
            "The van and its load changed hands.", CargoType.Electronics, 4),
        new("C12", "Steal from a collector's flat", 17, 25, 58, 280, 110, 8, PlayerSkill.Smarts,
            "The flat was empty, and now so is the collection.", CargoType.ArtPiece, 2),
    ];

    public static CrimeDefinition? Find(string? id) =>
        All.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
}

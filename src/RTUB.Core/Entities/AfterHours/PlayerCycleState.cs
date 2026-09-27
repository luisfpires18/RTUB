using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Core.Entities.AfterHours;

/// <summary>
/// A player's After Hours gameplay state for one <see cref="GameCycle"/>. Everything here is
/// annual power and resets with a new cycle; identity and history live elsewhere. At most one
/// row per cycle and user (unique index).
///
/// Energy and heat are stored as "value as of timestamp": regeneration and decay are computed
/// from the elapsed time since <see cref="EnergyUpdatedAtUtc"/> / <see cref="HeatUpdatedAtUtc"/>
/// and written back only when the value is changed, so no background job has to tick them.
/// </summary>
public class PlayerCycleState : BaseEntity
{
    public const int StartingLevel = 1;
    public const long StartingWalletCash = 400;
    public const int StartingMaxEnergy = 240;
    public const int StartingSkillRank = 4;

    public int GameCycleId { get; set; }
    public GameCycle? GameCycle { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int Level { get; set; }
    public long XP { get; set; }

    /// <summary>After Hours cash only. Never RTUB money or Fidelis.</summary>
    public long WalletCash { get; set; }
    public long BankCash { get; set; }

    /// <summary>Energy as of <see cref="EnergyUpdatedAtUtc"/>.</summary>
    public int Energy { get; set; }
    public int MaxEnergy { get; set; }
    public DateTime EnergyUpdatedAtUtc { get; set; }

    /// <summary>Heat as of <see cref="HeatUpdatedAtUtc"/>.</summary>
    public int Heat { get; set; }
    public DateTime HeatUpdatedAtUtc { get; set; }

    public int Toughness { get; set; }
    public int Stealth { get; set; }
    public int Smarts { get; set; }
    public int Charisma { get; set; }

    /// <summary>Jailed while this is in the future. Null or past means free; no job clears it.</summary>
    public DateTime? JailUntilUtc { get; set; }

    /// <summary>UTC date of the last cover job taken below the high-heat threshold.</summary>
    public DateOnly? CoverJobDailyUsedOn { get; set; }

    /// <summary>Cargo held this cycle, one row per type. Load it with the state before changing it.</summary>
    public List<PlayerCargo> Cargo { get; set; } = [];

    /// <summary>Stored training points (0..3) as of <see cref="TrainingPointsDay"/>.</summary>
    public int TrainingPoints { get; set; }

    /// <summary>
    /// Lisbon calendar day the points were last brought up to. A new state starts with that day's
    /// single point, anchored to its creation day. Null only on states created before AH-005: their
    /// first reconciliation grants one point, never a backlog.
    /// </summary>
    public DateOnly? TrainingPointsDay { get; set; }

    /// <summary>Gear bought this cycle. Load it with the state before changing it.</summary>
    public List<PlayerGear> Gear { get; set; } = [];

    // Equipped gear, one column per slot: at most one equipped item per slot by construction.
    public string? EquippedWeaponKey { get; set; }
    public string? EquippedOutfitKey { get; set; }
    public string? EquippedVehicleToolKey { get; set; }

    public static readonly TimeSpan EnergyRegenInterval = TimeSpan.FromMinutes(6);
    public static readonly TimeSpan HeatDecayInterval = TimeSpan.FromMinutes(10);

    // For EF Core
    public PlayerCycleState() { }

    public bool IsJailedAt(DateTime utcNow) => JailUntilUtc > utcNow;

    /// <summary>Brings energy and heat up to <paramref name="utcNow"/>. Deterministic and repeatable.</summary>
    public void Reconcile(DateTime utcNow)
    {
        ReconcileEnergy(utcNow);
        ReconcileHeat(utcNow);
        ReconcileTraining(utcNow);
    }

    /// <summary>
    /// +1 training point per Lisbon calendar day since <see cref="TrainingPointsDay"/>, stored up to
    /// <see cref="TrainingRules.MaxStoredPoints"/>. Never more than one point per day.
    /// </summary>
    public void ReconcileTraining(DateTime utcNow)
    {
        var today = LisbonCalendar.DateOf(utcNow);
        if (TrainingPointsDay is not { } day)
        {
            TrainingPoints = Math.Max(TrainingPoints, 1);
            TrainingPointsDay = today;
            return;
        }

        if (today <= day) return;
        TrainingPoints = Math.Min(TrainingRules.MaxStoredPoints, TrainingPoints + (today.DayNumber - day.DayNumber));
        TrainingPointsDay = today;
    }

    public string? EquippedKey(GearSlot slot) => slot switch
    {
        GearSlot.Weapon => EquippedWeaponKey,
        GearSlot.Outfit => EquippedOutfitKey,
        GearSlot.VehicleTool => EquippedVehicleToolKey,
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown slot")
    };

    public void SetEquipped(GearSlot slot, string? itemKey)
    {
        switch (slot)
        {
            case GearSlot.Weapon: EquippedWeaponKey = itemKey; break;
            case GearSlot.Outfit: EquippedOutfitKey = itemKey; break;
            case GearSlot.VehicleTool: EquippedVehicleToolKey = itemKey; break;
            default: throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown slot");
        }
    }

    public bool OwnsGear(string itemKey) => Gear.Any(g => g.ItemKey == itemKey);

    /// <summary>
    /// +1 energy per whole <see cref="EnergyRegenInterval"/>. The timestamp advances only by the
    /// whole intervals used, so a partial interval carries over. At max energy the clock is pinned
    /// to now: a full bar banks no hidden regeneration.
    /// </summary>
    public void ReconcileEnergy(DateTime utcNow)
    {
        if (Energy >= MaxEnergy)
        {
            Energy = MaxEnergy;
            if (utcNow > EnergyUpdatedAtUtc) EnergyUpdatedAtUtc = utcNow;
            return;
        }

        var points = WholeIntervals(EnergyUpdatedAtUtc, utcNow, EnergyRegenInterval);
        if (points == 0) return;

        if (Energy + points >= MaxEnergy)
        {
            Energy = MaxEnergy;
            EnergyUpdatedAtUtc = utcNow;
        }
        else
        {
            Energy += (int)points;
            EnergyUpdatedAtUtc += EnergyRegenInterval * points;
        }
    }

    /// <summary>-1 heat per whole <see cref="HeatDecayInterval"/>, same carry-over rule as energy; at 0 the clock is pinned to now.</summary>
    public void ReconcileHeat(DateTime utcNow)
    {
        if (Heat <= 0)
        {
            Heat = 0;
            if (utcNow > HeatUpdatedAtUtc) HeatUpdatedAtUtc = utcNow;
            return;
        }

        var points = WholeIntervals(HeatUpdatedAtUtc, utcNow, HeatDecayInterval);
        if (points == 0) return;

        if (Heat - points <= 0)
        {
            Heat = 0;
            HeatUpdatedAtUtc = utcNow;
        }
        else
        {
            Heat -= (int)points;
            HeatUpdatedAtUtc += HeatDecayInterval * points;
        }
    }

    /// <summary>Adds (or removes) heat on a reconciled state; never below 0.</summary>
    public void AddHeat(int delta, DateTime utcNow)
    {
        Heat = Math.Max(0, Heat + delta);
        if (Heat == 0) HeatUpdatedAtUtc = utcNow;
    }

    /// <summary>Adds XP and derives the level from the cumulative total.</summary>
    public void AddXp(long xp)
    {
        XP += xp;
        Level = AfterHoursLevels.LevelForXp(XP);
    }

    public int CargoQuantity(CargoType cargo) =>
        Cargo.FirstOrDefault(c => c.CargoType == cargo)?.Quantity ?? 0;

    /// <summary>Adds (or, with a negative amount, removes) cargo. Callers check the quantity first.</summary>
    public void AddCargo(CargoType cargo, int quantity)
    {
        var row = Cargo.FirstOrDefault(c => c.CargoType == cargo);
        if (row is null)
        {
            row = new PlayerCargo { PlayerCycleStateId = Id, CargoType = cargo };
            Cargo.Add(row);
        }

        if (row.Quantity + quantity < 0)
            throw new InvalidOperationException($"Cargo {cargo} would go negative.");
        row.Quantity += quantity;
    }

    private static long WholeIntervals(DateTime from, DateTime to, TimeSpan interval) =>
        to > from ? (to - from).Ticks / interval.Ticks : 0;

    /// <summary>
    /// The fixed starting state. Values are server-defined; nothing here comes from a client.
    /// </summary>
    public static PlayerCycleState CreateInitial(int gameCycleId, string userId, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User is required", nameof(userId));
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC", nameof(utcNow));

        return new PlayerCycleState
        {
            GameCycleId = gameCycleId,
            UserId = userId,
            Level = StartingLevel,
            XP = 0,
            WalletCash = StartingWalletCash,
            BankCash = 0,
            Energy = StartingMaxEnergy,
            MaxEnergy = StartingMaxEnergy,
            EnergyUpdatedAtUtc = utcNow,
            Heat = 0,
            HeatUpdatedAtUtc = utcNow,
            TrainingPoints = 1,
            TrainingPointsDay = LisbonCalendar.DateOf(utcNow),
            Toughness = StartingSkillRank,
            Stealth = StartingSkillRank,
            Smarts = StartingSkillRank,
            Charisma = StartingSkillRank
        };
    }
}

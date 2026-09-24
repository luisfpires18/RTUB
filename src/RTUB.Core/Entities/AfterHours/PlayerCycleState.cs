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

    // For EF Core
    public PlayerCycleState() { }

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
            Toughness = StartingSkillRank,
            Stealth = StartingSkillRank,
            Smarts = StartingSkillRank,
            Charisma = StartingSkillRank
        };
    }
}

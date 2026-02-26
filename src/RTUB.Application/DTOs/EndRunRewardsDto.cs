using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// Packages all accumulated rewards from a stage run for atomic end-of-run processing.
/// Used by EndRunAsync to apply rewards and return to checkpoint in a single operation.
/// </summary>
public class EndRunRewardsDto
{
    /// <summary>XP earned during the run</summary>
    public int Xp { get; init; }

    /// <summary>Fidelis earned during the run</summary>
    public decimal Fidelis { get; init; }

    /// <summary>Number of Finos dropped</summary>
    public int Finos { get; init; }

    /// <summary>Number of Canecas dropped</summary>
    public int Canecas { get; init; }

    /// <summary>Number of Cigarros dropped</summary>
    public int Cigarros { get; init; }

    /// <summary>Number of Canhãos dropped</summary>
    public int Canhaos { get; init; }

    /// <summary>Number of Shots dropped</summary>
    public int Shots { get; init; }

    /// <summary>Number of Penalties dropped</summary>
    public int Penalties { get; init; }

    /// <summary>FITAB earned during the run</summary>
    public int Fitab { get; init; }

    /// <summary>HP to restore after the run (null = full HP)</summary>
    public long? RestoreHp { get; init; }

    /// <summary>Instrument parts dropped, keyed by InventoryItemType</summary>
    public Dictionary<InventoryItemType, int>? InstrumentParts { get; init; }

    /// <summary>Whether to expire the penalty buff at run end</summary>
    public bool ExpirePenaltyBuff { get; init; } = true;

    /// <summary>Stage number where the run started (for logging)</summary>
    public int StartStage { get; init; }

    /// <summary>Stage number where the run ended (for logging)</summary>
    public int EndStage { get; init; }

    /// <summary>Rare set pieces dropped during the run</summary>
    public List<InventoryItemType>? RareSetPieces { get; init; }

    /// <summary>Number of Leitão dropped</summary>
    public int Leitao { get; init; }

    // ── Stage progress state (advanced in-memory during the run) ─────────

    /// <summary>Highest stage reached (in-memory value from stageProgress.HighestStage)</summary>
    public int HighestStage { get; init; }

    /// <summary>Last checkpoint reached (in-memory value from stageProgress.LastCheckpoint)</summary>
    public int LastCheckpoint { get; init; }

    /// <summary>Total stages cleared (in-memory value from stageProgress.TotalStagesCleared)</summary>
    public int TotalStagesCleared { get; init; }

    /// <summary>Total bosses defeated (in-memory value from stageProgress.TotalBossesDefeated)</summary>
    public int TotalBossesDefeated { get; init; }

    /// <summary>Whether endless mode was unlocked during this run</summary>
    public bool EndlessModeUnlocked { get; init; }
}

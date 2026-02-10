using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// Result of a boss mode battle including outcome, rewards, and replay data.
/// Boss mode battles are not persisted to DB - only progress is tracked.
/// </summary>
public class BossModeBattleResult
{
    public Guid BattleId { get; set; }
    public int CharacterId { get; set; }
    public int BossStage { get; set; }
    public string BossName { get; set; } = string.Empty;
    public int Seed { get; set; }
    public BattleOutcome Outcome { get; set; }
    public int XPReward { get; set; }
    public decimal FidelisReward { get; set; }
    public int FinosDropped { get; set; }
    public int CanecasDropped { get; set; }
    public int CigarrosDropped { get; set; }
    public int CanhaosDropped { get; set; }
    public int ShotsDropped { get; set; }
    public int PenaltiesDropped { get; set; }
    public List<InventoryItemType> InstrumentPartsDropped { get; set; } = new();
    public List<InventoryItemType> EquipmentDropped { get; set; } = new();
    public string ReplayJson { get; set; } = string.Empty;
    public int PlayerFinalHP { get; set; }
}

using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

public class MyTunoBattle : BaseEntity
{
    public MyTunoBattleMode Mode { get; set; }
    public MyTunoBattleOutcome Outcome { get; set; }
    public long Seed { get; set; }
    public string ReplayJson { get; set; } = string.Empty;

    public int AttackerCharacterId { get; set; }
    public Character? AttackerCharacter { get; set; }

    public int DefenderCharacterId { get; set; }
    public Character? DefenderCharacter { get; set; }

    public int AttackerXpDelta { get; set; }
    public int DefenderXpDelta { get; set; }
    public decimal AttackerFidelisDelta { get; set; }
    public decimal DefenderFidelisDelta { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}

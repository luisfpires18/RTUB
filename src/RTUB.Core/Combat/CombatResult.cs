namespace RTUB.Core.Combat;

public record CombatResult(
    string WinnerId,
    string LoserId,
    int AttackerFinalHp,
    int DefenderFinalHp,
    IReadOnlyList<CombatEvent> Events);

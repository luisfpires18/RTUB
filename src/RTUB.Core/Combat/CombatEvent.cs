namespace RTUB.Core.Combat;

public record CombatEvent(
    CombatEventType Type,
    int Round,
    string? AttackerId = null,
    string? DefenderId = null,
    int? Damage = null,
    int? HpAfter = null,
    string? WinnerId = null,
    string? LoserId = null);

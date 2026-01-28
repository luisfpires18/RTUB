namespace RTUB.Core.Combat;

public record CombatReplay(int Version, long Seed, IReadOnlyList<CombatEvent> Events);

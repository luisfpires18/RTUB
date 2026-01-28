namespace RTUB.Core.Combat;

public interface ICombatEngine
{
    CombatResult Simulate(CombatantStats attacker, CombatantStats defender, long seed);
}

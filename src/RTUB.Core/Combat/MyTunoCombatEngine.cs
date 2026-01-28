namespace RTUB.Core.Combat;

public sealed class MyTunoCombatEngine : ICombatEngine
{
    private const int MaxRounds = 30;

    public CombatResult Simulate(CombatantStats attacker, CombatantStats defender, long seed)
    {
        var rng = new DeterministicRandom(seed);
        var events = new List<CombatEvent>();

        var attackerHp = attacker.Hp;
        var defenderHp = defender.Hp;

        for (var round = 1; round <= MaxRounds; round++)
        {
            events.Add(new CombatEvent(CombatEventType.RoundStart, round));

            var attackerInitiative = attacker.Speed + rng.NextIntInclusive(0, 2);
            var defenderInitiative = defender.Speed + rng.NextIntInclusive(0, 2);

            if (attackerInitiative >= defenderInitiative)
            {
                if (ExecuteAttack(attacker, defender, round, rng, events, ref attackerHp, ref defenderHp))
                {
                    return BuildResult(attacker, defender, attackerHp, defenderHp, events);
                }
            }
            else
            {
                if (ExecuteAttack(defender, attacker, round, rng, events, ref defenderHp, ref attackerHp))
                {
                    return BuildResult(attacker, defender, attackerHp, defenderHp, events);
                }
            }

            events.Add(new CombatEvent(CombatEventType.RoundEnd, round));
        }

        return BuildResult(attacker, defender, attackerHp, defenderHp, events);
    }

    private static bool ExecuteAttack(
        CombatantStats attacker,
        CombatantStats defender,
        int round,
        DeterministicRandom rng,
        ICollection<CombatEvent> events,
        ref int attackerHp,
        ref int defenderHp)
    {
        var damage = Math.Max(1, attacker.Power + rng.NextIntInclusive(-2, 2));
        events.Add(new CombatEvent(CombatEventType.Attack, round, attacker.Id, defender.Id, damage));

        defenderHp -= damage;
        events.Add(new CombatEvent(CombatEventType.HpChange, round, DefenderId: defender.Id, HpAfter: Math.Max(defenderHp, 0)));

        if (defenderHp <= 0)
        {
            events.Add(new CombatEvent(CombatEventType.Ko, round, WinnerId: attacker.Id, LoserId: defender.Id));
            return true;
        }

        // Defender retaliates if still alive
        damage = Math.Max(1, defender.Power + rng.NextIntInclusive(-2, 2));
        events.Add(new CombatEvent(CombatEventType.Attack, round, defender.Id, attacker.Id, damage));

        attackerHp -= damage;
        events.Add(new CombatEvent(CombatEventType.HpChange, round, DefenderId: attacker.Id, HpAfter: Math.Max(attackerHp, 0)));

        if (attackerHp <= 0)
        {
            events.Add(new CombatEvent(CombatEventType.Ko, round, WinnerId: defender.Id, LoserId: attacker.Id));
            return true;
        }

        return false;
    }

    private static CombatResult BuildResult(
        CombatantStats attacker,
        CombatantStats defender,
        int attackerHp,
        int defenderHp,
        IReadOnlyList<CombatEvent> events)
    {
        if (attackerHp == defenderHp)
        {
            return new CombatResult(attacker.Id, defender.Id, attackerHp, defenderHp, events);
        }

        var attackerWins = attackerHp > defenderHp;
        return new CombatResult(attackerWins ? attacker.Id : defender.Id, attackerWins ? defender.Id : attacker.Id, attackerHp, defenderHp, events);
    }
}

using FluentAssertions;
using RTUB.Core.Combat;

namespace RTUB.Core.Tests.Combat;

public class MyTunoCombatEngineTests
{
    [Fact]
    public void Simulate_WithSameSeed_IsDeterministic()
    {
        var engine = new MyTunoCombatEngine();
        var attacker = new CombatantStats("A", 20, 6, 5);
        var defender = new CombatantStats("B", 18, 5, 4);
        const long seed = 123456;

        var first = engine.Simulate(attacker, defender, seed);
        var second = engine.Simulate(attacker, defender, seed);

        first.Should().BeEquivalentTo(second);
    }
}

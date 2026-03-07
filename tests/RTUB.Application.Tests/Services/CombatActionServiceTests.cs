using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for CombatActionService — anti-exploit rate limiting and BattleSpeed scaling.
/// </summary>
public class CombatActionServiceTests
{
    private readonly CombatActionService _sut;

    public CombatActionServiceTests()
    {
        var inventoryRepo = new Mock<IInventoryRepository>();
        _sut = new CombatActionService(inventoryRepo.Object);
    }

    /// <summary>
    /// Creates a minimal CombatSession for testing auto-attack rate limiting.
    /// </summary>
    private CombatSession CreateTestSession(double playerActionTime = 2.5, double battleSpeed = 1.0)
    {
        var player = Character.Create("testUser");
        player.HP = 10_000;
        player.Power = 5_000;
        player.Speed = 10;

        var enemy = Character.Create("enemyUser");
        enemy.HP = 10_000;
        enemy.Power = 100;
        enemy.Speed = 10;

        var session = _sut.CreateSession(player, new List<Character> { enemy }, seed: 42, mode: "boss");
        session.BattleSpeed = battleSpeed;

        // Override action times to deterministic test values
        session.Player.ActionTimeSeconds = playerActionTime;
        session.Enemies[0].ActionTimeSeconds = playerActionTime;

        return session;
    }

    #region BattleSpeed Tolerance Scaling

    [Fact]
    public void ProcessPlayerAutoAttack_AtSpeed1_RejectsAttackBeforeTolerance()
    {
        // Arrange — tolerance = 2.5 * 0.3 = 0.75s  
        var session = CreateTestSession(playerActionTime: 2.5, battleSpeed: 1.0);
        // Set last action to just 0.1s ago (well under 0.75s tolerance)
        session.LastPlayerActionAt = DateTime.UtcNow.AddSeconds(-0.1);

        // Act
        var result = _sut.ProcessPlayerAutoAttack(session);

        // Assert — should be silently ignored (empty result, no events)
        result.Events.Should().BeEmpty();
        result.BattleOver.Should().BeFalse();
    }

    [Fact]
    public void ProcessPlayerAutoAttack_AtSpeed1_AcceptsAttackAfterTolerance()
    {
        // Arrange — tolerance = 2.5 * 0.3 = 0.75s
        var session = CreateTestSession(playerActionTime: 2.5, battleSpeed: 1.0);
        // Set last action to 1s ago (above 0.75s tolerance)
        session.LastPlayerActionAt = DateTime.UtcNow.AddSeconds(-1.0);

        // Act
        var result = _sut.ProcessPlayerAutoAttack(session);

        // Assert — should have attack events
        result.Events.Should().NotBeEmpty();
    }

    [Fact]
    public void ProcessPlayerAutoAttack_AtSpeed100_AcceptsRapidAttacks()
    {
        // Arrange — effective tolerance = 2.5 * (0.3 / 100) = 0.0075s
        var session = CreateTestSession(playerActionTime: 2.5, battleSpeed: 100.0);
        // Set last action to 0.01s ago — above 0.0075s but below the 1x tolerance of 0.75s
        session.LastPlayerActionAt = DateTime.UtcNow.AddSeconds(-0.01);

        // Act
        var result = _sut.ProcessPlayerAutoAttack(session);

        // Assert — should accept the attack at high speed
        result.Events.Should().NotBeEmpty();
    }

    [Fact]
    public void ProcessPlayerAutoAttack_AtSpeed100_StillRejectsExtremeSpam()
    {
        // Arrange — effective tolerance = 2.5 * (0.3 / 100) = 0.0075s
        var session = CreateTestSession(playerActionTime: 2.5, battleSpeed: 100.0);
        // Set last action to just now (0s elapsed — below even the high-speed tolerance)
        session.LastPlayerActionAt = DateTime.UtcNow;

        // Act
        var result = _sut.ProcessPlayerAutoAttack(session);

        // Assert — should be rejected
        result.Events.Should().BeEmpty();
        result.BattleOver.Should().BeFalse();
    }

    [Fact]
    public void ProcessEnemyAttack_AtSpeed100_AcceptsRapidAttacks()
    {
        // Arrange — effective tolerance = 2.5 * (0.3 / 100) = 0.0075s
        var session = CreateTestSession(playerActionTime: 2.5, battleSpeed: 100.0);
        // Set last enemy action to 0.01s ago
        session.LastEnemyActionAt[0] = DateTime.UtcNow.AddSeconds(-0.01);

        // Act
        var result = _sut.ProcessEnemyAttack(session, 0);

        // Assert — should accept the attack at high speed
        result.Events.Should().NotBeEmpty();
    }

    [Fact]
    public void ProcessEnemyAttack_AtSpeed1_RejectsAttackBeforeTolerance()
    {
        // Arrange — tolerance = 2.5 * 0.3 = 0.75s
        var session = CreateTestSession(playerActionTime: 2.5, battleSpeed: 1.0);
        // Set last enemy action to 0.1s ago
        session.LastEnemyActionAt[0] = DateTime.UtcNow.AddSeconds(-0.1);

        // Act
        var result = _sut.ProcessEnemyAttack(session, 0);

        // Assert — should be silently ignored
        result.Events.Should().BeEmpty();
        result.BattleOver.Should().BeFalse();
    }

    #endregion

    #region BattleSpeed Default

    [Fact]
    public void CombatSession_DefaultBattleSpeed_IsOne()
    {
        // Arrange & Act
        var session = new CombatSession();

        // Assert
        session.BattleSpeed.Should().Be(1.0);
    }

    [Fact]
    public void CreateSession_BattleSpeed_DefaultsToOne()
    {
        // Arrange
        var player = Character.Create("testUser");
        player.HP = 100;
        player.Power = 10;
        player.Speed = 10;

        var enemy = Character.Create("enemyUser");
        enemy.HP = 100;
        enemy.Power = 10;
        enemy.Speed = 10;

        // Act
        var session = _sut.CreateSession(player, new List<Character> { enemy }, seed: 42);

        // Assert
        session.BattleSpeed.Should().Be(1.0);
    }

    #endregion

    #region BattleSpeed Clamping

    [Fact]
    public void ProcessPlayerAutoAttack_NegativeBattleSpeed_TreatedAsSpeed1()
    {
        // Arrange — negative speed should be clamped to 1.0 internally
        var session = CreateTestSession(playerActionTime: 2.5, battleSpeed: -5.0);
        // Set last action to 0.5s ago — under 0.75s tolerance at speed 1, above at high speed
        session.LastPlayerActionAt = DateTime.UtcNow.AddSeconds(-0.5);

        // Act
        var result = _sut.ProcessPlayerAutoAttack(session);

        // Assert — should be rejected (effective speed is 1.0, tolerance is 0.75s)
        result.Events.Should().BeEmpty();
    }

    #endregion
}

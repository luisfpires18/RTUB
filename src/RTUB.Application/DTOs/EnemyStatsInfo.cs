namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for enemy stats used in stage and boss mode battles.
/// </summary>
public record EnemyStatsInfo(string Name, long HP, long? MaxHP, long Power, long Defense, double ActionTime);

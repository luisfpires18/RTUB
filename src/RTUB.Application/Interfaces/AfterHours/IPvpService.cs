using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Interfaces.AfterHours;

/// <summary>
/// A possible target as other players may see it: no bank, wallet or cargo figures.
/// <see cref="BlockReason"/> is null when this player could attack it now (target side only).
/// </summary>
public sealed record PvpTargetView(int StateId, string DisplayName, int Level, int EffectivePower, string? BlockReason);

/// <summary>A persisted battle with the two players' display names.</summary>
public sealed record PvpBattleView(PvpBattle Battle, string AttackerName, string DefenderName);

/// <summary>PvP reads. Every write goes through <see cref="IAfterHoursActionService"/>.</summary>
public interface IPvpService
{
    /// <summary>Other players in the active cycle. Empty when there is no cycle or no state yet.</summary>
    Task<IReadOnlyList<PvpTargetView>> GetTargetsAsync(string userId);

    /// <summary>The battle, with rounds and cargo, only if this user was its attacker or defender.</summary>
    Task<PvpBattleView?> GetBattleAsync(string userId, int battleId);

    /// <summary>This user's latest incoming and outgoing battles, newest first.</summary>
    Task<IReadOnlyList<PvpBattleView>> GetRecentBattlesAsync(string userId, int take = 10);
}

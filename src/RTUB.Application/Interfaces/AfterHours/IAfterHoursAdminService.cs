using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Interfaces.AfterHours;

/// <summary>A cycle as the admin page shows it.</summary>
public sealed record AdminCycleView(
    int Id, GameCycleKind Kind, GameCycleStatus Status, int FiscalYearId, string FiscalYearLabel,
    DateTime StartUtc, DateTime EndUtc, bool PlayableNow, int Players, int Families, int? ArchiveId);

/// <summary>What an annual Live rollover of the active cycle would do, or why it cannot yet.</summary>
public sealed record LiveRolloverPreview(int SourceCycleId, DateTime SourceEndUtc, NextLiveCycle? Next, string? Blocker, bool AvailableNow);

/// <summary>One readiness check. Non-blocking checks are warnings: they never turn READY into BLOCKED.</summary>
public sealed record ReadinessCheck(string Name, bool Passed, string Detail, bool Blocking = true);

public sealed record FiscalYearOption(int Id, string Label, int StartYear);

public sealed record AfterHoursAdminStatus(
    bool FeatureEnabled,
    string EnvironmentName,
    AdminCycleView? Active,
    IReadOnlyList<AdminCycleView> Cycles,
    AfterHoursTuning Tuning,
    int OverrideCount,
    IReadOnlyList<InvalidTuningRow> InvalidTuning,
    LiveRolloverPreview? LiveRollover,
    string? ExpectedNextFiscalYear,
    bool NextFiscalYearExists,
    IReadOnlyList<FiscalYearOption> FiscalYears,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ReadinessCheck> Readiness)
{
    /// <summary>Technically ready for private pilot testing: every blocking check passed. Not a PROD launch sign-off.</summary>
    public bool Ready => Readiness.All(c => c.Passed || !c.Blocking);
}

/// <summary>One recent battle with deterministic review flags. Flags are hints for a human, not proof.</summary>
public sealed record SuspiciousPvpRow(
    int BattleId, DateTime AcceptedAtUtc, string AttackerName, string DefenderName, bool AttackerWon,
    int AttackerPower, int DefenderPower, decimal PowerRatio, decimal LootMultiplier, long WalletStolen, long CargoValueStolen,
    bool SameFamilyAtBattle, int PairCount, int PairWins, IReadOnlyList<string> Flags);

public sealed record CosmeticAwardView(AfterHoursCosmeticAward Award, string? GrantedByName);

public sealed record AwardRecipientOption(string UserId, string Name);

/// <summary>
/// Owner-only After Hours operations (AH-010). Every method checks that <c>actorUserId</c> holds the RTUB
/// <c>Owner</c> role and throws <see cref="UnauthorizedAccessException"/> otherwise, so hiding the page is not the
/// only protection. Refusals throw <see cref="InvalidOperationException"/> or <see cref="ArgumentException"/>.
/// Rollovers go through <see cref="IAfterHoursRolloverService"/>; nothing here re-implements them.
/// </summary>
public interface IAfterHoursAdminService
{
    Task<AfterHoursAdminStatus> GetStatusAsync(string actorUserId);

    /// <summary>Validates, then stores the override (one row per key). Returns the stored, normalized value.</summary>
    Task<string> SetTuningAsync(string actorUserId, string key, string value);

    /// <summary>Removes the override; the default applies again.</summary>
    Task ResetTuningAsync(string actorUserId, string key);

    /// <summary>The emergency switch: stores <c>PvpEnabled</c>.</summary>
    Task SetPvpEnabledAsync(string actorUserId, bool enabled);

    /// <summary>
    /// Creates a Pilot and makes it Active in one write transaction. The end must be after the start and in the
    /// future; a length outside 7–14 days needs <paramref name="acknowledgeUnusualLength"/>. Refused while
    /// another cycle is Active. Creates no player state.
    /// </summary>
    Task<AdminCycleView> CreatePilotAsync(string actorUserId, int fiscalYearId, DateTime startUtc, DateTime endUtc, bool acknowledgeUnusualLength);

    Task<RolloverResult> TransitionPilotToLiveAsync(string actorUserId, int pilotCycleId, int fiscalYearId, DateTime startUtc, DateTime endUtc);

    /// <summary>"Force rollover": a manual call of the same safe annual rollover. Bypasses scheduling only.</summary>
    Task<RolloverResult> RolloverLiveAsync(string actorUserId, int sourceCycleId);

    /// <summary>Battles accepted in the last <paramref name="days"/> days, flagged ones first.</summary>
    Task<IReadOnlyList<SuspiciousPvpRow>> GetRecentPvpAsync(string actorUserId, int days = 7);

    Task<IReadOnlyList<CosmeticAwardView>> GetAwardsAsync(string actorUserId);

    Task<IReadOnlyList<AwardRecipientOption>> GetAwardRecipientsAsync(string actorUserId);

    Task<AfterHoursCosmeticAward> GrantAwardAsync(string actorUserId, string recipientUserId, string title, string? description, int? fiscalYearId, int? cycleArchiveId);

    Task RevokeAwardAsync(string actorUserId, int awardId);
}
